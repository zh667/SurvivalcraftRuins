using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;
using Random = System.Random;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// Static orchestrator bridging ModLoader hooks to per-world state.
/// </summary>
public static class RuinsRuntime
{
    private const int ChestsPerFrame = 8;
    private const double SaveIntervalSeconds = 5.0;

    private static readonly object s_bindLock = new();
    private static WorldState? s_state;
    private static double s_time;

    private static IReadOnlyDictionary<string, StructureData>? s_templates;
    private static IReadOnlyDictionary<string, LootPool>? s_lootPools;

    /// <summary>Loads templates and loot pools from the mod package. Idempotent.</summary>
    public static void LoadAssets(ModEntity entity)
    {
        if (s_templates != null)
        {
            return;
        }
        var templates = new Dictionary<string, StructureData>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, LootPool>? pools = null;
        entity.GetFiles(".json", (name, stream) =>
        {
            string normalized = name.Replace('\\', '/');
            if (normalized.EndsWith("modinfo.json", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            if (normalized.Contains("Assets/Structures/", StringComparison.OrdinalIgnoreCase))
            {
                var data = StructureTemplate.Parse(json);
                templates[data.Name] = data;
            }
            else if (normalized.Contains("Assets/Loot/", StringComparison.OrdinalIgnoreCase))
            {
                pools = LootConfig.Parse(json);
            }
        });
        s_templates = templates;
        s_lootPools = pools ?? new Dictionary<string, LootPool>();
        Log.Information($"[Ruins] Loaded {templates.Count} structure templates, {s_lootPools.Count} loot pools.");
    }

    public static void OnChunkGenerated(TerrainChunk chunk)
    {
        if (RuntimeCompat.IsClient)
        {
            return;
        }
        var state = BindIfNeeded();
        state?.WriteChunk(chunk);
    }

    public static void Update(float dt)
    {
        if (RuntimeCompat.IsClient)
        {
            return;
        }
        var state = BindIfNeeded();
        if (state == null)
        {
            return;
        }
        s_time += dt;

        DrainIncoming(state);
        ProcessPendingChests(state);
        ProcessAmbushes(state);

        if (state.Dirty && s_time - state.LastSaveTime > SaveIntervalSeconds)
        {
            state.LastSaveTime = s_time;
            state.SavePersistence();
        }
    }

    private static WorldState? BindIfNeeded()
    {
        var project = GameManager.Project;
        var current = s_state;
        if (current != null && ReferenceEquals(current.Project, project))
        {
            return current;
        }
        lock (s_bindLock)
        {
            current = s_state;
            if (current != null && ReferenceEquals(current.Project, project))
            {
                return current;
            }
            if (current != null)
            {
                if (current.Dirty)
                {
                    current.SavePersistence();
                }
                s_state = null;
            }
            if (project == null || s_templates == null || s_lootPools == null)
            {
                return null;
            }
            try
            {
                var terrain = project.FindSubsystem<SubsystemTerrain>();
                var gameInfo = project.FindSubsystem<SubsystemGameInfo>();
                if (terrain?.TerrainContentsGenerator == null || gameInfo == null)
                {
                    return null;
                }
                BlockResolver.EnsureInitialized();
                var defs = StructureRegistry.CreateDefs(s_templates);
                var sampler = new GeneratorSampler(terrain.TerrainContentsGenerator);
                var state = new WorldState
                {
                    Project = project,
                    Terrain = terrain,
                    GameInfo = gameInfo,
                    BlockEntities = project.FindSubsystem<SubsystemBlockEntities>(throwOnError: true),
                    Players = project.FindSubsystem<SubsystemPlayers>(throwOnError: true),
                    CreatureSpawn = project.FindSubsystem<SubsystemCreatureSpawn>(throwOnError: true),
                    Generator = terrain.TerrainContentsGenerator,
                    Placement = new PlacementEngine(gameInfo.WorldSeed, sampler, defs),
                    LootPools = s_lootPools,
                };
                state.LoadPersistence();
                s_state = state;
                Log.Information($"[Ruins] Bound to world '{gameInfo.DirectoryName}', seed {gameInfo.WorldSeed}.");
                return state;
            }
            catch (Exception exception)
            {
                Log.Warning($"[Ruins] World bind failed: {exception.Message}");
                return null;
            }
        }
    }

    private static void DrainIncoming(WorldState state)
    {
        bool changed = false;
        while (state.IncomingChests.TryDequeue(out var chest))
        {
            bool exists = false;
            foreach (var existing in state.PendingChests)
            {
                if (existing.X == chest.X && existing.Y == chest.Y && existing.Z == chest.Z)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                state.PendingChests.Add(chest);
                changed = true;
            }
        }
        while (state.IncomingAmbushes.TryDequeue(out var ambush))
        {
            if (!state.Ambushes.ContainsKey(ambush.Id))
            {
                state.Ambushes.Add(ambush.Id, ambush);
                changed = true;
            }
        }
        if (changed)
        {
            state.Dirty = true;
        }
    }

    private static void ProcessPendingChests(WorldState state)
    {
        int processed = 0;
        for (int i = state.PendingChests.Count - 1; i >= 0 && processed < ChestsPerFrame; i--)
        {
            var pending = state.PendingChests[i];
            var chunk = state.Terrain.Terrain.GetChunkAtCell(pending.X, pending.Z);
            if (chunk == null || chunk.State < TerrainChunkState.InvalidVertices1)
            {
                continue;
            }
            processed++;
            int contents = state.Terrain.Terrain.GetCellContents(pending.X, pending.Y, pending.Z);
            bool stillChest = BlockResolver.TryGetIndex("ChestBlock", out int chestIndex) && contents == chestIndex;
            if (stillChest)
            {
                try
                {
                    FillChest(state, pending);
                }
                catch (Exception exception)
                {
                    Log.Warning($"[Ruins] Chest fill at {pending.X},{pending.Y},{pending.Z} failed: {exception.Message}");
                }
            }
            state.PendingChests.RemoveAt(i);
            state.Dirty = true;
        }
    }

    private static void FillChest(WorldState state, PendingChest pending)
    {
        var point = new Point3(pending.X, pending.Y, pending.Z);
        var blockEntity = state.BlockEntities.GetBlockEntity(pending.X, pending.Y, pending.Z);
        if (blockEntity == null)
        {
            // Replicates SubsystemBlockEntities.CreateBlockEntityLogic without
            // the owner (which dereferences a null miner during worldgen).
            var databaseObject = state.Project.GameDatabase.Database.FindDatabaseObject(
                "Chest", state.Project.GameDatabase.EntityTemplateType, throwIfNotFound: true);
            var valuesDictionary = new ValuesDictionary();
            valuesDictionary.PopulateFromDatabaseObject(databaseObject);
            valuesDictionary.GetValue<ValuesDictionary>("BlockEntity").SetValue("Coordinates", point);
            var entity = state.Project.CreateEntity(valuesDictionary);
            state.Project.AddEntity(entity);
            blockEntity = state.BlockEntities.GetBlockEntity(pending.X, pending.Y, pending.Z);
            if (blockEntity == null)
            {
                return;
            }
        }
        var chest = blockEntity.Entity.FindComponent<ComponentChest>();
        if (chest == null)
        {
            return;
        }
        if (!state.LootPools.TryGetValue(pending.Pool, out var pool))
        {
            Log.Warning($"[Ruins] Unknown loot pool '{pending.Pool}'.");
            return;
        }
        var random = new Random(pending.Seed);
        foreach (var item in pool.Roll(random))
        {
            int value = BlockResolver.MakeValue(item.BlockName, item.Data);
            if (value != 0)
            {
                ComponentInventoryBase.AcquireItems(chest, value, item.Count);
            }
        }
    }

    private static void ProcessAmbushes(WorldState state)
    {
        if (state.Ambushes.Count == 0 || state.Players.ComponentPlayers.Count == 0)
        {
            return;
        }
        List<string>? triggeredIds = null;
        foreach (var ambush in state.Ambushes.Values)
        {
            if (ambush.Triggered)
            {
                continue;
            }
            bool playerNear = false;
            foreach (var player in state.Players.ComponentPlayers)
            {
                var position = player.ComponentBody.Position;
                float dx = position.X - ambush.X;
                float dy = position.Y - ambush.Y;
                float dz = position.Z - ambush.Z;
                if (dx * dx + dy * dy + dz * dz <= ambush.Radius * ambush.Radius)
                {
                    playerNear = true;
                    break;
                }
            }
            if (!playerNear)
            {
                continue;
            }
            var chunk = state.Terrain.Terrain.GetChunkAtCell(ambush.X, ambush.Z);
            if (chunk == null || chunk.State < TerrainChunkState.Valid)
            {
                continue;
            }
            SpawnAmbush(state, ambush);
            (triggeredIds ??= new List<string>()).Add(ambush.Id);
        }
        if (triggeredIds != null)
        {
            foreach (var id in triggeredIds)
            {
                state.Ambushes[id].Triggered = true;
            }
            state.Dirty = true;
        }
    }

    private static void SpawnAmbush(WorldState state, AmbushState ambush)
    {
        var random = new Random(ambush.Seed);
        int count = random.Next(ambush.Min, ambush.Max + 1);
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(random.NextDouble() * Math.PI * 2.0);
            float distance = 1.5f + (float)random.NextDouble() * 3f;
            int x = ambush.X + (int)MathF.Round(MathF.Cos(angle) * distance);
            int z = ambush.Z + (int)MathF.Round(MathF.Sin(angle) * distance);
            // Prefer the marker height (inside the structure); creatures placed
            // at the terrain top would appear on the roof and wander off.
            int y = FindSpawnY(state, x, ambush.Y, z);
            try
            {
                // Portable across SCAPI and Net2.4 (CreateEntity is Net-only).
                var entity = DatabaseManager.CreateEntity(state.Project, ambush.Creature, throwIfNotFound: true);
                var frame = entity.FindComponent<ComponentFrame>(throwOnError: true);
                frame.Position = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                frame.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY,
                    (float)(random.NextDouble() * Math.PI * 2.0));
                entity.FindComponent<ComponentSpawn>(throwOnError: true).SpawnDuration = 0f;
                state.Project.AddEntity(entity);
            }
            catch (Exception exception)
            {
                Log.Warning($"[Ruins] Ambush spawn '{ambush.Creature}' failed: {exception.Message}");
            }
        }
        Log.Information($"[Ruins] Ambush sprung at {ambush.X},{ambush.Y},{ambush.Z}: {count}x {ambush.Creature}.");
    }

    /// <summary>
    /// Picks a spawn height: the marker height when there is standing room
    /// there (inside a den), otherwise the first air gap scanning upward, and
    /// as a last resort above the terrain.
    /// </summary>
    private static int FindSpawnY(WorldState state, int x, int markerY, int z)
    {
        var terrain = state.Terrain.Terrain;
        if (terrain.GetChunkAtCell(x, z) == null)
        {
            return Math.Clamp(markerY, 1, 254);
        }
        for (int y = markerY; y <= Math.Min(markerY + 6, 253); y++)
        {
            if (terrain.GetCellContents(x, y, z) == 0 && terrain.GetCellContents(x, y + 1, z) == 0)
            {
                return y;
            }
        }
        return Math.Min(terrain.CalculateTopmostCellHeight(x, z) + 1, 254);
    }
}
