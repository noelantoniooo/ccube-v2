// Theme.cs
// -----------------------------------------------------------------------------
// One place for the app's colours and fonts. The app has TWO palettes - a light
// one (default, the industrial HMI look) and a dark one - chosen by the
// Theme.Dark flag. That flag is set ONCE at start-up (from settings) before any
// window is built, so the whole app is drawn in the chosen palette. Flipping the
// toggle saves the choice and restarts, so everything redraws cleanly.
//
// Every colour below returns the light or dark value via the little C() helper.
// Fonts are the same in both themes.
// -----------------------------------------------------------------------------

namespace CCube.Forms;

public static class Theme
{
    /// <summary>true = dark palette. Set at start-up from settings.</summary>
    public static bool Dark { get; set; }

    // Pick light or dark for a colour.
    private static Color C(Color light, Color dark) => Dark ? dark : light;
    private static Color Rgb(int r, int g, int b) => Color.FromArgb(r, g, b);

    // Grounds & surfaces
    public static Color Background => C(Rgb(237, 241, 244), Rgb(18, 21, 27));
    public static Color Surface => C(Rgb(255, 255, 255), Rgb(27, 31, 39));
    public static Color SurfaceAlt => C(Rgb(243, 246, 248), Rgb(35, 40, 51));
    public static Color InputBg => C(Rgb(244, 247, 249), Rgb(35, 40, 51));
    public static Color Border => C(Rgb(214, 221, 227), Rgb(49, 56, 69));
    public static Color Panel => C(Rgb(255, 255, 255), Rgb(27, 31, 39));
    public static Color PanelDark => C(Rgb(244, 247, 249), Rgb(35, 40, 51));

    // Text
    public static Color Text => C(Rgb(30, 42, 54), Rgb(230, 233, 238));
    public static Color Muted => C(Rgb(106, 118, 129), Rgb(151, 160, 172));

    // Brand green (the header stays green in both themes)
    public static Color HeaderGreen => Rgb(20, 101, 58);
    public static Color HeaderGreenDark => Rgb(15, 82, 47);
    public static Color HeaderText => Rgb(238, 245, 240);
    public static Color Accent => C(Rgb(27, 127, 69), Rgb(63, 181, 110));

    // Verdicts
    public static Color Pass => C(Rgb(30, 142, 62), Rgb(46, 160, 67));
    public static Color PassBg => C(Rgb(231, 244, 234), Rgb(18, 39, 26));
    public static Color Reject => C(Rgb(206, 58, 58), Rgb(229, 83, 75));
    public static Color RejectBg => C(Rgb(251, 234, 234), Rgb(42, 21, 22));
    public static Color Idle => C(Rgb(229, 234, 238), Rgb(42, 48, 56));  // neutral "READY" panel
    public static Color Amber => C(Rgb(184, 118, 10), Rgb(224, 161, 38));
    public static Color AmberBg => C(Rgb(249, 240, 221), Rgb(42, 35, 18));

    // Status lights
    public static Color LedGreen => C(Rgb(34, 162, 78), Rgb(63, 185, 80));
    public static Color LedRed => C(Rgb(226, 76, 76), Rgb(242, 86, 77));
    public static Color LedGrey => C(Rgb(155, 166, 176), Rgb(120, 128, 138));
    public static Color LedAmber => Rgb(224, 161, 38);

    // Picks the first installed font from the list (else falls back to Segoe UI),
    // using its Bold style when the family has one.
    private static Font PickFont(float size, params string[] names)
    {
        FontFamily[] installed = new System.Drawing.Text.InstalledFontCollection().Families;
        foreach (string name in names)
        {
            FontFamily? family = Array.Find(installed,
                f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (family is not null)
            {
                FontStyle style = family.IsStyleAvailable(FontStyle.Bold)
                    ? FontStyle.Bold : FontStyle.Regular;
                return new Font(family, size, style);
            }
        }
        return new Font("Segoe UI", size, FontStyle.Bold);
    }

    // Fonts (same in both themes; kept compact so everything fits on one screen)
    // Brand uses Bahnschrift (an industrial DIN-style face) when available.
    public static readonly Font BrandFont = PickFont(22F, "Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Semibold");
    public static readonly Font BrandSubFont = new("Segoe UI", 8.5F, FontStyle.Regular);
    public static readonly Font TitleFont = new("Segoe UI", 20F, FontStyle.Bold);
    public static readonly Font VerdictFont = new("Segoe UI Semibold", 40F, FontStyle.Bold);
    public static readonly Font SectionFont = new("Segoe UI", 9F, FontStyle.Bold);
    public static readonly Font DetailFont = new("Segoe UI", 10.5F, FontStyle.Regular);
    public static readonly Font NormalFont = new("Segoe UI", 9.5F, FontStyle.Regular);
    public static readonly Font BoldFont = new("Segoe UI", 9.5F, FontStyle.Bold);
    public static readonly Font SmallFont = new("Segoe UI", 8F, FontStyle.Regular);
    public static readonly Font CounterFont = new("Segoe UI", 20F, FontStyle.Bold);
    public static readonly Font CardStateFont = new("Segoe UI", 12F, FontStyle.Bold);
}
