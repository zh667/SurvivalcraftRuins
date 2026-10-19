using Game;
using SurvivalcraftRuins.Core;

namespace SurvivalcraftRuins.Mod;

public sealed class RuinsModLoader : ModLoader
{
    public override void __ModInitialize()
    {
        ModsManager.RegisterHook("OnTerrainContentsGenerated", this);
        ModsManager.RegisterHook("SubsystemUpdate", this);
        try
        {
            RuinsRuntime.LoadAssets(Entity);
        }
        catch (Exception exception)
        {
            Engine.Log.Error($"[Ruins] Asset load failed: {exception.Message}");
        }
    }

    public override void OnTerrainContentsGenerated(TerrainChunk chunk)
    {
        try
        {
            RuinsRuntime.OnChunkGenerated(chunk);
        }
        catch (Exception exception)
        {
            Engine.Log.Error($"[Ruins] Chunk generation hook failed: {exception}");
        }
    }

    public override void SubsystemUpdate(float dt)
    {
        try
        {
            RuinsRuntime.Update(dt);
        }
        catch (Exception exception)
        {
            Engine.Log.Error($"[Ruins] Update hook failed: {exception}");
        }
    }

    public override void OnLoadingFinished(List<Action> actions)
    {
#if RUINS_SCAPI
        // Mod Settings values are loaded before this runs.
        RuinsModSettings.Pull();
#else
        // Net 2.4 has no Mod Settings page: the mod brings its own screen and
        // hangs a button for it off the game's Settings screen, which is
        // already registered by now.
        RuinsScreens.EnsureScreen();
        RuinsScreens.InjectButton();
#endif
    }

#if RUINS_SCAPI
    public override void OnModSettingChanged(string[] idPath, object value)
    {
        RuinsModSettings.Apply(idPath, value);
    }
#else
    public override void SaveSettings(System.Xml.Linq.XElement xElement)
    {
        RuinsSettings.Save(xElement);
    }

    public override void LoadSettings(System.Xml.Linq.XElement xElement)
    {
        RuinsSettings.Load(xElement);
    }
#endif
}
