using System.Globalization;
using System.Xml.Linq;
using Game;
using SurvivalcraftRuins.Core;

namespace SurvivalcraftRuins.Mod;

/// <summary>
/// The two knobs, on a screen players can actually reach. Both settings existed
/// from the first release but lived only in ModSettings.xml, which on a phone
/// means they did not exist at all.
/// </summary>
public sealed class RuinsSettingsScreen : Screen
{
    private const string Layout = @"
<Screen xmlns='runtime-namespace:Game'>
  <PanoramaWidget />
  <StackPanelWidget Direction='Horizontal'>
    <CanvasWidget Style='{Widgets/TopBarContents}'>
      <LabelWidget Name='TopBar.Label' Text='TITLE' />
    </CanvasWidget>
    <CanvasWidget Margin='10, 20'>
      <BevelledRectangleWidget Style='{Styles/Area}' />
      <ScrollPanelWidget Direction='Vertical' HorizontalAlignment='Center' VerticalAlignment='Center' Margin='3, 3'>
        <StackPanelWidget Direction='Vertical' VerticalAlignment='Center' HorizontalAlignment='Center'>
          <UniformSpacingPanelWidget Direction='Horizontal' Margin='0, 3'>
            <LabelWidget Text='DENSITY' HorizontalAlignment='Far' VerticalAlignment='Center' Margin='20, 0' />
            <StackPanelWidget Direction='Vertical' VerticalAlignment='Center' Margin='20, 0'>
              <SliderWidget Name='DensitySlider' Size='Infinity, 60' Granularity='1' />
              <LabelWidget Name='DensityHint' TextAnchor='HorizontalCenter' Color='128, 128, 128' HorizontalAlignment='Center' />
            </StackPanelWidget>
          </UniformSpacingPanelWidget>
          <UniformSpacingPanelWidget Direction='Horizontal' Margin='0, 3'>
            <LabelWidget Text='GUARDS' HorizontalAlignment='Far' VerticalAlignment='Center' Margin='20, 0' />
            <StackPanelWidget Direction='Vertical' VerticalAlignment='Center' Margin='20, 0'>
              <SliderWidget Name='GuardsSlider' Size='Infinity, 60' Granularity='0.25' />
              <LabelWidget Name='GuardsHint' TextAnchor='HorizontalCenter' Color='128, 128, 128' HorizontalAlignment='Center' />
            </StackPanelWidget>
          </UniformSpacingPanelWidget>
          <LabelWidget Name='Note' TextAnchor='HorizontalCenter' Color='128, 128, 128' HorizontalAlignment='Center' Margin='20, 12' />
        </StackPanelWidget>
      </ScrollPanelWidget>
    </CanvasWidget>
  </StackPanelWidget>
</Screen>";

    private readonly SliderWidget m_densitySlider;
    private readonly SliderWidget m_guardsSlider;
    private readonly LabelWidget m_densityHint;
    private readonly LabelWidget m_guardsHint;

    public RuinsSettingsScreen()
    {
        string xml = Layout
            .Replace("TITLE", RuinsText.Title)
            .Replace("DENSITY", RuinsText.Density)
            .Replace("GUARDS", RuinsText.Guards);
        LoadContents(this, XElement.Parse(xml));

        m_densitySlider = Children.Find<SliderWidget>("DensitySlider");
        m_guardsSlider = Children.Find<SliderWidget>("GuardsSlider");
        m_densityHint = Children.Find<LabelWidget>("DensityHint");
        m_guardsHint = Children.Find<LabelWidget>("GuardsHint");

        m_densitySlider.MinValue = 0f;
        m_densitySlider.MaxValue = RuinsSettings.DensityTiers.Length - 1;
        m_guardsSlider.MinValue = RuinsSettings.MinScale;
        m_guardsSlider.MaxValue = RuinsSettings.MaxScale;

        var note = Children.Find<LabelWidget>("Note", throwIfNotFound: false);
        if (note != null)
        {
            note.Text = RuinsText.Note;
        }
    }

    public override void Update()
    {
        if (GameManager.Project != null)
        {
            GameManager.UpdateProject();
        }
        if (m_densitySlider.IsSliding)
        {
            int tier = Math.Clamp((int)MathF.Round(m_densitySlider.Value), 0, RuinsSettings.DensityTiers.Length - 1);
            RuinsSettings.Density = RuinsSettings.DensityTiers[tier];
        }
        if (m_guardsSlider.IsSliding)
        {
            RuinsSettings.Guards = m_guardsSlider.Value;
        }

        int shown = RuinsSettings.NearestDensityTier(RuinsSettings.Density);
        m_densitySlider.Value = shown;
        m_densitySlider.Text = shown == 0
            ? RuinsText.DensityTier(0)
            : $"{RuinsText.DensityTier(shown)} {Describe(RuinsSettings.Density)}";
        m_densityHint.Text = RuinsText.DensityHint;
        m_guardsSlider.Value = RuinsSettings.Guards;
        m_guardsSlider.Text = RuinsSettings.Guards <= 0f ? RuinsText.GuardsOff : Describe(RuinsSettings.Guards);
        m_guardsHint.Text = RuinsText.GuardsHint;

        var back = Children.Find<ButtonWidget>("TopBar.Back", throwIfNotFound: false);
        if (!Input.Back && !Input.Cancel && (back == null || !back.IsClicked))
        {
            return;
        }
        try
        {
            SettingsManager.SaveSettings();
        }
        catch (Exception exception)
        {
            Engine.Log.Warning($"[Ruins] Saving settings failed: {exception.Message}");
        }
        ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
    }

    private static string Describe(float scale)
    {
        return scale <= 0f
            ? RuinsText.Off
            : "x" + scale.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
