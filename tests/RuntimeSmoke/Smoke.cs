using System.Text.Json;
using Engine;
using Game;
#if RUINS_SCAPI
using HarmonyLib;
#else
using Game.NetWork;
#endif

namespace RuinsRuntimeSmoke;

/// <summary>
/// Runs inside an isolated copy of the game (tools/run_isolated.py), never shipped.
/// It does not reference the mod, so the same test also runs against an old
/// package as a control.
///
/// Creates a world, flies the player around until it finds ruin chests the mod
/// has filled, then checks that a chest is never stocked twice:
///   1. one chest left as found, one emptied by "the player";
///   2. the player flies far away until those chunks are unloaded, then comes
///      back, twice. Nobody changed a block there, so the game does not save
///      those chunks and regenerates them from the seed: exactly what made 2.0.0
///      add another set of loot every time;
///   3. the world is saved and loaded again, and the chests checked once more.
///
/// smoke-mode.txt next to the exe: "local" (single player) or, on the
/// multiplayer runtime, "server" (the host's side of a multiplayer game, which
/// is what a player of that runtime always runs: the play screen starts a
/// server. The socket is not opened, so no firewall prompt).
/// </summary>
public class Smoke : ModLoader
{
    const string WorldName = "RuinsSmoke";
    static string mode = "local";
    static int stage;
    static double began, step, menuSince = double.MaxValue;
    static WorldInfo? world;
    static PlayerData? data;
    static readonly List<string> checks = new();
    static readonly List<object> log = new();
    static readonly List<Vector3> searchSpots = new();
    static int searchIndex;
    static Point3 kept, emptied;
    static Dictionary<int, int> keptContents = new();
    static int roundTrip, retries;
    static readonly HashSet<Point3> rejected = new();
    static Vector3 home;

    public override void __ModInitialize()
    {
#if RUINS_SCAPI
        new Harmony("local.ruinssmoke").PatchAll(typeof(Smoke).Assembly);
#endif
        var m = Path.Combine(AppContext.BaseDirectory, "smoke-mode.txt");
        if (File.Exists(m))
        {
            mode = File.ReadAllText(m).Trim();
        }
    }

    static string Output => Path.Combine(AppContext.BaseDirectory, "smoke-result.json");

    public override void OnLoadingFinished(List<Action> actions)
    {
        actions.Add(() => { began = Time.RealTime; Window.Frame += Frame; });
    }

    static void Check(bool ok, string description)
    {
        if (!ok)
        {
            throw new Exception("FAILED: " + description);
        }
        checks.Add(description);
    }

    static ContainerWidget Games => ScreensManager.FindScreen<GameScreen>("Game").Children.Find<ContainerWidget>("GamesWidget");
    static SubsystemTerrain Terrain_ => GameManager.Project.FindSubsystem<SubsystemTerrain>(true);
    static SubsystemBlockEntities Entities => GameManager.Project.FindSubsystem<SubsystemBlockEntities>(true);
    static ComponentPlayer Player => data!.ComponentPlayer;

    static void Finish(object result)
    {
        File.WriteAllText(Output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Window.Frame -= Frame;
        Window.Close();
    }

    static string WorkTypeName()
    {
#if RUINS_SCAPI
        return "plugin";
#else
        return CommonLib.WorkType.ToString();
#endif
    }

    static void Frame()
    {
        try
        {
            if (Time.RealTime - began > 900)
            {
                throw new TimeoutException("stage " + stage);
            }
            Step();
        }
        catch (Exception e)
        {
            Finish(new { passed = false, mode, runtime = WorkTypeName(), stage, checks, log, error = e.ToString() });
        }
        // The hidden test window still counts as active; a hidden mouse would
        // put it in raw mode and clip the user's cursor to the window.
        Engine.Input.Mouse.IsMouseVisible = true;
    }

    static void Step()
    {
        switch (stage)
        {
            case 0:
                // Let the main menu finish its entry animation: switching screens
                // in the middle of it throws inside ScreensManager and lands back
                // on the menu. Also close the multiplayer runtime's update notice.
                if (ScreensManager.CurrentScreen is not MainMenuScreen)
                {
                    menuSince = Time.RealTime;
                    return;
                }
                if (menuSince == double.MaxValue)
                {
                    menuSince = Time.RealTime;   // already on the menu when the test's first frame runs
                }
                if (Time.RealTime - menuSince < 3)
                {
                    return;
                }
                DialogsManager.HideAllDialogs();
                SettingsManager.VisibilityRange = 64;
                SettingsManager.SoundsVolume = 0;
                SettingsManager.MusicVolume = 0;
                world = WorldsManager.CreateWorld(new WorldSettings
                {
                    Name = WorldName,
                    Seed = "ruins-smoke",
                    GameMode = GameMode.Creative,
                    AreWeatherEffectsEnabled = false,
                    TimeOfDayMode = TimeOfDayMode.Day,
                });
                StartSession();
                GameManager.LoadProject(world, Games);
                data = new PlayerData(GameManager.Project) { Name = "Smoke", InputDevice = WidgetInputDevice.None };
#if !RUINS_SCAPI
                data.SetMain();
#endif
                GameManager.Project.FindSubsystem<SubsystemPlayers>(true).AddPlayerData(data);
                ScreensManager.SwitchScreen("Game");
                step = Time.RealTime;
                stage = 1;
                return;
            case 1:
                if (!data!.IsReadyForPlaying)
                {
                    if (ScreensManager.CurrentScreen is MainMenuScreen && Time.RealTime - step > 3)
                    {
                        DialogsManager.HideAllDialogs();
                        ScreensManager.SwitchScreen("Game");
                        step = Time.RealTime;
                        log.Add(new { retriedEnteringGame = true });
                    }
                    return;
                }
#if RUINS_SCAPI
                string expected = "plugin";
#else
                string expected = mode == "server" ? "Server" : "Local";
#endif
                Check(WorkTypeName() == expected, $"runs as {WorkTypeName()} (expected {expected})");
                Player.ComponentLocomotion.IsCreativeFlyEnabled = true;
                home = Player.ComponentBody.Position;
                for (int ring = 0; ring <= 6; ring++)
                {
                    for (int i = 0; i < Math.Max(1, ring * 6); i++)
                    {
                        double a = i * Math.PI * 2 / Math.Max(1, ring * 6);
                        searchSpots.Add(new Vector3(home.X + (float)(Math.Cos(a) * ring * 110), 140f, home.Z + (float)(Math.Sin(a) * ring * 110)));
                    }
                }
                Fly(searchSpots[0]);
                stage = 2;
                return;
            case 2:
                // Searching: wait for the area to generate and its chests to fill.
                Hover();
                if (Time.RealTime - step < 6)
                {
                    return;
                }
                var found = StockedChests();
                log.Add(new { search = searchIndex, at = searchSpots[searchIndex].ToString(), chests = found.Count });
                if (found.Count >= 2)
                {
                    kept = found[0];
                    emptied = found[1];
                    keptContents = Contents(kept);
                    var inventory = Inventory(emptied)!;
                    for (int s = 0; s < inventory.SlotsCount; s++)
                    {
                        inventory.RemoveSlotItems(s, inventory.GetSlotCount(s));
                    }
                    Check(keptContents.Values.Sum() > 0, $"found filled ruin chests at {kept} and {emptied}");
                    Check(Total(emptied) == 0, "emptied the second chest, as a player looting it would");
                    log.Add(new { kept = kept.ToString(), contents = Describe(keptContents), emptied = emptied.ToString() });
                    home = new Vector3(kept.X + 0.5f, 140f, kept.Z + 0.5f);
                    Fly(home + new Vector3(3000, 0, 0));
                    stage = 3;
                    return;
                }
                if (++searchIndex >= searchSpots.Count)
                {
                    throw new Exception("no two filled ruin chests within 660 blocks of spawn");
                }
                Fly(searchSpots[searchIndex]);
                return;
            case 3:
                // Far away: wait until both chests' chunks are gone from memory.
                Hover();
                if (Time.RealTime - step < 3 || Chunk(kept) != null || Chunk(emptied) != null)
                {
                    if (Time.RealTime - step > 90)
                    {
                        throw new Exception("the chests' chunks were never unloaded");
                    }
                    return;
                }
                log.Add(new { trip = roundTrip + 1, unloadedAfter = Math.Round(Time.RealTime - step, 1) });
                Fly(home);
                stage = 4;
                return;
            case 4:
                Hover();
                var chunk = Chunk(kept);
                if (chunk == null || chunk.State < TerrainChunkState.Valid || Time.RealTime - step < 6)
                {
                    if (Time.RealTime - step > 90)
                    {
                        throw new Exception("the chests' chunks never came back");
                    }
                    return;
                }
                roundTrip++;
                bool regenerated = !chunk.IsLoaded && Chunk(emptied) is { IsLoaded: false };
                log.Add(new { trip = roundTrip, regeneratedFromSeed = regenerated, kept = Describe(Contents(kept)), emptiedTotal = Total(emptied) });
                if (!regenerated)
                {
                    // Something changed a block in one of these chunks, so the game
                    // saved it and read it back: the bug path was never taken.
                    // Inconclusive, not passed; try other chests.
                    if (++retries > 4)
                    {
                        throw new Exception("the chests' chunks kept being saved; regeneration never tested");
                    }
                    rejected.Add(kept);
                    rejected.Add(emptied);
                    roundTrip = 0;
                    Fly(searchSpots[searchIndex]);
                    stage = 2;
                    return;
                }
                Check(Terrain_.Terrain.GetCellContents(kept.X, kept.Y, kept.Z) == ChestBlock.Index, $"trip {roundTrip}: the chest is still there");
                Check(SameContents(Contents(kept), keptContents),
                    $"trip {roundTrip}: the untouched chest holds what it held ({Describe(keptContents)}), now {Describe(Contents(kept))}");
                Check(Total(emptied) == 0, $"trip {roundTrip}: the emptied chest stays empty, now {Total(emptied)} items");
                if (roundTrip < 2)
                {
                    Fly(home + new Vector3(0, 0, 3000));
                    stage = 3;
                    return;
                }
                GameManager.SaveProject(true, false);
                GameManager.LoadProject(world!, Games);
                data = GameManager.Project.FindSubsystem<SubsystemPlayers>(true).PlayersData[0];
                ScreensManager.SwitchScreen("Game");
                step = Time.RealTime;
                stage = 5;
                return;
            case 5:
                if (!data!.IsReadyForPlaying || Time.RealTime - step < 2)
                {
                    return;
                }
                Player.ComponentLocomotion.IsCreativeFlyEnabled = true;
                Fly(home);
                stage = 6;
                return;
            case 6:
                Hover();
                var again = Chunk(kept);
                if (again == null || again.State < TerrainChunkState.Valid || Time.RealTime - step < 6)
                {
                    if (Time.RealTime - step > 90)
                    {
                        throw new Exception("the chests' chunks never came back after reloading");
                    }
                    return;
                }
                log.Add(new { reloaded = true, regeneratedFromSeed = !again.IsLoaded, kept = Describe(Contents(kept)), emptiedTotal = Total(emptied) });
                Check(SameContents(Contents(kept), keptContents),
                    $"after saving and reloading the world: the untouched chest is unchanged, now {Describe(Contents(kept))}");
                Check(Total(emptied) == 0, $"after saving and reloading the world: the emptied chest stays empty, now {Total(emptied)} items");
                Finish(new { passed = true, mode, runtime = WorkTypeName(), checks, log });
                return;
        }
    }

    static void StartSession()
    {
#if !RUINS_SCAPI
        // Normally done by the loading screen, which this test skips.
        CommonLib.Net.StartLocal();
        if (mode == "server")
        {
            CommonLib.Net.WorkType = WorkType.Server;
            CommonLib.Net.CurrentStage = NetNode.Stage.Connected;
            CommonLib.Net.Server = CommonLib.Net.Self;
        }
#endif
    }

    static void Fly(Vector3 to)
    {
        step = Time.RealTime;
        Hover(to);
    }

    static Vector3 s_target;

    static void Hover(Vector3? to = null)
    {
        if (to.HasValue)
        {
            s_target = to.Value;
        }
        var body = Player.ComponentBody;
        body.Position = s_target;
        body.Velocity = Vector3.Zero;
    }

    static TerrainChunk? Chunk(Point3 p) => Terrain_.Terrain.GetChunkAtCell(p.X, p.Z);

    static ComponentInventoryBase? Inventory(Point3 p) =>
        Entities.GetBlockEntity(p.X, p.Y, p.Z)?.Entity.FindComponent<ComponentInventoryBase>();

    static List<Point3> StockedChests()
    {
        var result = new List<Point3>();
        foreach (var chunk in Terrain_.Terrain.AllocatedChunks)
        {
            // Only chunks nothing has changed: the game does not save those, so
            // leaving and coming back regenerates them from the seed.
            if (chunk.State < TerrainChunkState.Valid || chunk.ModificationCounter > 0)
            {
                continue;
            }
            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    for (int y = 1; y < 255; y++)
                    {
                        if (Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z)) != ChestBlock.Index)
                        {
                            continue;
                        }
                        var p = new Point3(chunk.Origin.X + x, y, chunk.Origin.Y + z);
                        if (Total(p) > 0 && !rejected.Contains(p))
                        {
                            result.Add(p);
                        }
                    }
                }
            }
        }
        // Prefer chests far apart, so emptying one cannot disturb the other.
        result.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
        return result.Count >= 2 ? new List<Point3> { result[0], result[result.Count - 1] } : result;
    }

    static Dictionary<int, int> Contents(Point3 p)
    {
        var d = new Dictionary<int, int>();
        var inventory = Inventory(p);
        if (inventory == null)
        {
            return d;
        }
        for (int s = 0; s < inventory.SlotsCount; s++)
        {
            int count = inventory.GetSlotCount(s);
            if (count > 0)
            {
                int value = inventory.GetSlotValue(s);
                d[value] = d.GetValueOrDefault(value) + count;
            }
        }
        return d;
    }

    static int Total(Point3 p) => Contents(p).Values.Sum();

    static bool SameContents(Dictionary<int, int> a, Dictionary<int, int> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out int n) && n == kv.Value);

    static string Describe(Dictionary<int, int> d) =>
        d.Count == 0 ? "empty" : string.Join(", ", d.OrderBy(kv => kv.Key).Select(kv =>
            $"{BlocksManager.Blocks[Terrain.ExtractContents(kv.Key)].GetType().Name}x{kv.Value}"));
}

#if RUINS_SCAPI
// See Smoke.Frame: both engine hooks must see a visible mouse (MechDefense b204a64).
[HarmonyPatch(typeof(Engine.Input.Mouse), "BeforeFrame")]
public static class KeepCursorFree
{
    public static void Prefix() => Engine.Input.Mouse.IsMouseVisible = true;
}

[HarmonyPatch(typeof(Engine.Input.Mouse), "AfterFrame")]
public static class KeepCursorFreeAfter
{
    public static void Prefix() => Engine.Input.Mouse.IsMouseVisible = true;
}
#endif
