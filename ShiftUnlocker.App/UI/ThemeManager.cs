using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;

namespace ShiftUnlocker.App.UI;

public static class ThemeManager
{
    // светлая тема по умолчанию
    public static bool IsDarkTheme => false;

    public static Color BackgroundColor => Color.FromArgb(246, 246, 249);
    public static Color SurfaceColor => Color.FromArgb(255, 255, 255);
    public static Color TextColor => Color.FromArgb(24, 24, 28);
    public static Color TextMutedColor => Color.FromArgb(105, 105, 115);
    public static Color BorderColor => Color.FromArgb(218, 218, 226);
    public static Color ButtonBackColor => Color.FromArgb(238, 238, 244);
    public static Color ButtonHoverColor => Color.FromArgb(226, 226, 234);

    public static Font StandardFont { get; } = GetPreferredFont(new[] { "Segoe UI", "Tahoma", "Microsoft Sans Serif" }, 9.25f);
    public static Font BoldFont { get; } = GetPreferredFont(new[] { "Segoe UI", "Tahoma", "Microsoft Sans Serif" }, 9.25f, FontStyle.Bold);
    public static Font LargeBoldFont { get; } = GetPreferredFont(new[] { "Segoe UI", "Tahoma", "Microsoft Sans Serif" }, 11.5f, FontStyle.Bold);
    public static Font MonoFont { get; } = GetPreferredFont(new[] { "Consolas", "Courier New", "Lucida Console" }, 9.25f);

    private static Font GetPreferredFont(string[] familyNames, float size, FontStyle style = FontStyle.Regular)
    {
        foreach (var name in familyNames)
        {
            try
            {
                using var test = new FontFamily(name);
                return new Font(name, size, style);
            }
            catch { }
        }
        return new Font(FontFamily.GenericSansSerif, size, style);
    }

    public static void EnableDoubleBuffering(Control control)
    {
        typeof(Control).InvokeMember(
            "DoubleBuffered",
            BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            control,
            new object[] { true });
    }

    public static void ApplyTheme(Control control)
    {
        control.BackColor = BackgroundColor;
        control.ForeColor = TextColor;
        control.Font = StandardFont;

        foreach (Control c in control.Controls)
        {
            ApplyThemeToChild(c);
        }
    }

    private static void ApplyThemeToChild(Control c)
    {
        // кастомные контролы не трогаем, шрифт у них свой
        if (c is RoundedTabControl rtc)
        {
            rtc.UpdateTheme();
        }
        else if (c is RoundedTabButton rtb)
        {
            rtb.Invalidate();
        }
        else if (c is RoundedNavButton rnb)
        {
            rnb.Invalidate();
        }
        else if (c is RoundedButton rb)
        {
            rb.Invalidate();
        }
        else if (c is Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = BorderColor;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = ButtonHoverColor;
            btn.BackColor = ButtonBackColor;
            btn.ForeColor = TextColor;
            btn.Cursor = Cursors.Hand;
        }
        else if (c is ListView lv)
        {
            EnableDoubleBuffering(lv);
            lv.BackColor = SurfaceColor;
            lv.ForeColor = TextColor;
            lv.BorderStyle = BorderStyle.FixedSingle;
            lv.Font = StandardFont;
        }
        else if (c is TreeView tv)
        {
            EnableDoubleBuffering(tv);
            tv.BackColor = SurfaceColor;
            tv.ForeColor = TextColor;
            tv.BorderStyle = BorderStyle.FixedSingle;
            tv.Font = StandardFont;
        }
        else if (c is ListBox lb)
        {
            EnableDoubleBuffering(lb);
            lb.BackColor = SurfaceColor;
            lb.ForeColor = TextColor;
            lb.BorderStyle = BorderStyle.FixedSingle;
            lb.Font = StandardFont;
        }
        else if (c is TextBox tb)
        {
            tb.BackColor = SurfaceColor;
            tb.ForeColor = TextColor;
            tb.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (c is ComboBox cb)
        {
            cb.BackColor = SurfaceColor;
            cb.ForeColor = TextColor;
            cb.FlatStyle = FlatStyle.Flat;
        }
        else if (c is StatusStrip ss)
        {
            ss.BackColor = SurfaceColor;
            ss.ForeColor = TextColor;
        }
        else if (c is RoundedTabPage rtp)
        {
            rtp.BackColor = BackgroundColor;
            rtp.ForeColor = TextColor;
        }
        else if (c is TabPage tp)
        {
            tp.UseVisualStyleBackColor = false;
            tp.BackColor = BackgroundColor;
            tp.ForeColor = TextColor;
        }
        else if (c is TabControl tc)
        {
            tc.DrawMode = TabDrawMode.OwnerDrawFixed;
            foreach (TabPage page in tc.TabPages)
            {
                page.UseVisualStyleBackColor = false;
                page.BackColor = BackgroundColor;
                page.ForeColor = TextColor;
            }
        }
        else if (c is RoundedFlowPanel rfp)
        {
            rfp.Invalidate();
        }
        else if (c is RoundedPanel rp)
        {
            rp.Invalidate();
        }
        else if (c is Panel p)
        {
            p.BackColor = BackgroundColor;
            p.ForeColor = TextColor;
        }

        foreach (Control child in c.Controls)
        {
            ApplyThemeToChild(child);
        }
    }
}
