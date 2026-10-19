using System.Linq;
using System.Xml.Linq;
using Game;

namespace SurvivalcraftRuins.Mod;

/// <summary>
/// The button that opens <see cref="RuinsSettingsScreen"/> when it has been
/// added to the game's Settings screen from outside. It watches itself rather
/// than relying on that screen's Update, which only knows about the buttons the
/// game shipped.
/// </summary>
public sealed class SettingsEntryButton : BevelledButtonWidget
{
    public override void Update()
    {
        base.Update();
        if (IsClicked)
        {
            RuinsScreens.Open();
        }
    }
}

/// <summary>Registers the settings screen and its way in.</summary>
public static class RuinsScreens
{
    public const string Name = "RuinsSettings";
    private const string ButtonName = "RuinsSettingsButton";

    /// <summary>
    /// Registers the screen if it is not there. Best-effort: a UI that fails
    /// to install must never stop the mod from generating structures.
    /// </summary>
    public static bool EnsureScreen()
    {
        try
        {
            if (ScreensManager.FindScreen<RuinsSettingsScreen>(Name) == null)
            {
                ScreensManager.AddScreen(Name, new RuinsSettingsScreen());
            }
            return true;
        }
        catch (Exception exception)
        {
            Engine.Log.Warning($"[Ruins] Settings screen unavailable: {exception.Message}");
            return false;
        }
    }

    /// <summary>Opens the screen, registering it first if something dropped it.</summary>
    public static void Open()
    {
        if (EnsureScreen())
        {
            ScreensManager.SwitchScreen(Name);
        }
    }

    /// <summary>
    /// A button in the game's settings style. <paramref name="selfOpening"/>
    /// is for buttons nobody else watches; one handed to the plugin runtime's
    /// settings hook must not open the screen a second time.
    /// </summary>
    public static ButtonWidget CreateButton(bool selfOpening)
    {
        ButtonWidget button = selfOpening ? new SettingsEntryButton() : new BevelledButtonWidget();
        // Style first: it loads a whole contents block over the widget.
        button.Style = ContentManager.Get<XElement>("Styles/ButtonStyle_310x60");
        button.Name = ButtonName;
        button.Text = RuinsText.Button;
        button.HorizontalAlignment = WidgetAlignment.Center;
        button.VerticalAlignment = WidgetAlignment.Center;
        button.Margin = new Engine.Vector2(0f, 5f);
        return button;
    }

    /// <summary>
    /// Hangs a button off one of the Settings screen's button columns, for the
    /// runtime that has no settings hook (Net 2.4).
    /// </summary>
    public static void InjectButton()
    {
        try
        {
            var settings = ScreensManager.FindScreen<SettingsScreen>("Settings");
            if (settings == null)
            {
                Engine.Log.Warning("[Ruins] No Settings screen to add a button to.");
                return;
            }
            if (settings.Children.Find<Widget>(ButtonName, throwIfNotFound: false) != null)
            {
                return;
            }
            var column = FindButtonColumn(settings);
            if (column == null)
            {
                Engine.Log.Warning("[Ruins] Settings screen has no button column to add to.");
                return;
            }
            column.Children.Add(CreateButton(selfOpening: true));
            Engine.Log.Information("[Ruins] Settings button installed.");
        }
        catch (Exception exception)
        {
            Engine.Log.Warning($"[Ruins] Could not add the settings button: {exception.Message}");
        }
    }

    /// <summary>
    /// The game's own settings buttons, any one of which sits directly in a
    /// column. Naming the columns would be simpler, but the stock screen names
    /// neither of its two, and the fork that does uses names of its own.
    /// </summary>
    private static readonly string[] s_anchors =
    {
        "Performance", "Graphics", "Ui", "Audio", "Controls", "Compatibility",
    };

    /// <summary>
    /// Finds a column of buttons to join. Anchoring on a known button matters:
    /// the top bar is also a vertical stack with a button in it, so "any stack
    /// holding buttons" lands the entry in the corner next to Back.
    /// </summary>
    private static StackPanelWidget? FindButtonColumn(SettingsScreen settings)
    {
        foreach (string anchor in s_anchors)
        {
            if (settings.Children.Find<Widget>(anchor, throwIfNotFound: false)?.ParentWidget
                is StackPanelWidget column && column.Direction == LayoutDirection.Vertical)
            {
                // Join the shorter of the two columns so the screen stays even.
                return Shortest(column);
            }
        }
        return null;
    }

    private static StackPanelWidget Shortest(StackPanelWidget column)
    {
        var siblings = column.ParentWidget;
        if (siblings == null)
        {
            return column;
        }
        var best = column;
        int bestCount = column.Children.OfType<ButtonWidget>().Count();
        foreach (var widget in siblings.Children)
        {
            if (widget is StackPanelWidget candidate
                && candidate.Direction == LayoutDirection.Vertical)
            {
                int buttons = candidate.Children.OfType<ButtonWidget>().Count();
                if (buttons > 0 && buttons < bestCount)
                {
                    best = candidate;
                    bestCount = buttons;
                }
            }
        }
        return best;
    }
}
