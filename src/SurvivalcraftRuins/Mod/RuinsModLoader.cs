using Game;
using SurvivalcraftRuins.Core;

namespace SurvivalcraftRuins.Mod;

public sealed class RuinsModLoader : ModLoader
{
    public override void __ModInitialize()
    {
        ModsManager.RegisterHook("OnTerrainContentsGenerated", this);
        ModsManager.RegisterHook("SubsystemUpdate", this);
#if RUINS_SCAPI
        ModsManager.RegisterHook("OnSettingsScreenCreated", this);
#endif
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
        RuinsScreens.EnsureScreen();
#if !RUINS_SCAPI
        // Net 2.4 has no settings hook. Screens are registered earlier in the
        // loading queue, so the game's Settings screen is already there to
        // hang a button off.
        RuinsScreens.InjectButton();
#endif
    }

#if RUINS_SCAPI
    /// <summary>
    /// The plugin runtime's own way in for mod settings: the Settings screen
    /// asks every mod for its buttons each time it is built, including when a
    /// language change rebuilds every screen, which would silently drop a
    /// button added from outside.
    /// </summary>
    public override void OnSettingsScreenCreated(SettingsScreen settingsScreen,
        out Dictionary<ButtonWidget, Action> buttonsToAdd)
    {
        buttonsToAdd = new Dictionary<ButtonWidget, Action>();
        try
        {
            buttonsToAdd.Add(RuinsScreens.CreateButton(selfOpening: false), RuinsScreens.Open);
        }
        catch (Exception exception)
        {
            Engine.Log.Warning($"[Ruins] Could not offer a settings button: {exception.Message}");
        }
    }
#endif

    public override void SaveSettings(System.Xml.Linq.XElement xElement)
    {
        RuinsSettings.Save(xElement);
    }

    public override void LoadSettings(System.Xml.Linq.XElement xElement)
    {
        RuinsSettings.Load(xElement);
    }
}
