using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ShiftUnlocker.App.UI.Controls;

public static class GraphicsPathHelper
{
    public static Color GetEffectiveBackColor(Control? control)
    {
        Control? cur = control;
        while (cur != null)
        {
            if (cur.BackColor != Color.Transparent && cur.BackColor.A == 255)
            {
                return cur.BackColor;
            }
            cur = cur.Parent;
        }
        return ThemeManager.BackgroundColor;
    }

    public static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;

        if (d > bounds.Width) d = bounds.Width;
        if (d > bounds.Height) d = bounds.Height;
        if (d <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new Rectangle(bounds.X, bounds.Y, d, d);

        // верхний левый угол
        path.AddArc(arc, 180, 90);

        // верхний правый угол
        arc.X = bounds.Right - d;
        path.AddArc(arc, 270, 90);

        // нижний правый угол
        arc.Y = bounds.Bottom - d;
        path.AddArc(arc, 0, 90);

        // нижний левый угол
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }
}

public class RoundedButton : Button
{
    private bool _isHovered;
    private bool _isPressed;

    public int CornerRadius { get; set; } = 6;
    public bool IsPrimary { get; set; } = false;

    public RoundedButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint, true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        Size = new Size(120, 32);
        BackColor = Color.Transparent;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        _isPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        _isPressed = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _isPressed = false;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;

        Color parentBg = GraphicsPathHelper.GetEffectiveBackColor(Parent);
        g.Clear(parentBg);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Default;

        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        using var path = GraphicsPathHelper.CreateRoundedRectangle(bounds, CornerRadius);

        Color backColor;
        Color borderColor;
        Color foreColor;

        if (!Enabled)
        {
            backColor = Color.FromArgb(238, 238, 242);
            borderColor = Color.FromArgb(215, 215, 222);
            foreColor = Color.FromArgb(155, 155, 165);
        }
        else if (ThemeManager.IsDarkTheme)
        {
            if (IsPrimary)
            {
                backColor = _isPressed ? Color.FromArgb(185, 185, 195) :
                            _isHovered ? Color.FromArgb(235, 235, 242) : Color.FromArgb(215, 215, 222);
                borderColor = Color.FromArgb(240, 240, 245);
                foreColor = Color.FromArgb(18, 18, 22);
            }
            else
            {
                backColor = _isPressed ? Color.FromArgb(30, 30, 36) :
                            _isHovered ? Color.FromArgb(50, 50, 58) : Color.FromArgb(38, 38, 44);
                borderColor = _isHovered ? Color.FromArgb(85, 85, 96) : Color.FromArgb(58, 58, 66);
                foreColor = Color.FromArgb(235, 235, 240);
            }
        }
        else
        {
            if (IsPrimary)
            {
                backColor = _isPressed ? Color.FromArgb(35, 35, 40) :
                            _isHovered ? Color.FromArgb(15, 15, 20) : Color.FromArgb(25, 25, 30);
                borderColor = Color.FromArgb(15, 15, 20);
                foreColor = Color.FromArgb(250, 250, 250);
            }
            else
            {
                backColor = _isPressed ? Color.FromArgb(215, 215, 222) :
                            _isHovered ? Color.FromArgb(232, 232, 238) : Color.FromArgb(242, 242, 246);
                borderColor = _isHovered ? Color.FromArgb(165, 165, 175) : Color.FromArgb(205, 205, 215);
                foreColor = Color.FromArgb(25, 25, 30);
            }
        }

        using (var brush = new SolidBrush(backColor))
        {
            g.FillPath(brush, path);
        }

        using (var pen = new Pen(borderColor, 1f))
        {
            g.DrawPath(pen, path);
        }

        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        using var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };
        using var textBrush = new SolidBrush(foreColor);
        g.DrawString(Text, Font, textBrush, new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), sf);
    }
}

public class RoundedNavButton : Button
{
    private bool _isHovered;
    public bool IsActive { get; set; }
    public int CornerRadius { get; set; } = 6;

    public RoundedNavButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint, true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        Height = 44;
        Dock = DockStyle.Top;
        Margin = new Padding(2, 2, 2, 2);
        BackColor = Color.Transparent;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;

        Color parentBg = GraphicsPathHelper.GetEffectiveBackColor(Parent);
        g.Clear(parentBg);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Default;

        var bounds = new Rectangle(2, 2, Width - 5, Height - 5);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        using var path = GraphicsPathHelper.CreateRoundedRectangle(bounds, CornerRadius);

        Color backColor;
        Color borderColor;
        Color foreColor;

        if (!Enabled)
        {
            backColor = Color.FromArgb(238, 238, 242);
            borderColor = Color.FromArgb(215, 215, 222);
            foreColor = Color.FromArgb(155, 155, 165);
        }
        else if (ThemeManager.IsDarkTheme)
        {
            if (IsActive)
            {
                backColor = Color.FromArgb(46, 46, 56);
                borderColor = Color.FromArgb(70, 70, 84);
                foreColor = Color.FromArgb(255, 255, 255);
            }
            else
            {
                backColor = _isHovered ? Color.FromArgb(32, 32, 38) : parentBg;
                borderColor = _isHovered ? Color.FromArgb(50, 50, 60) : Color.Transparent;
                foreColor = _isHovered ? Color.FromArgb(240, 240, 245) : Color.FromArgb(160, 160, 170);
            }
        }
        else
        {
            if (IsActive)
            {
                backColor = Color.FromArgb(220, 220, 228);
                borderColor = Color.FromArgb(175, 175, 190);
                foreColor = Color.FromArgb(15, 15, 20);
            }
            else
            {
                backColor = _isHovered ? Color.FromArgb(235, 235, 242) : parentBg;
                borderColor = _isHovered ? Color.FromArgb(205, 205, 215) : Color.Transparent;
                foreColor = _isHovered ? Color.FromArgb(20, 20, 25) : Color.FromArgb(90, 90, 100);
            }
        }

        using (var brush = new SolidBrush(backColor))
        {
            g.FillPath(brush, path);
        }

        if (borderColor != Color.Transparent)
        {
            using var pen = new Pen(borderColor, 1f);
            g.DrawPath(pen, path);
        }

        // полоска активного пункта слева
        if (IsActive)
        {
            var indicatorRect = new Rectangle(bounds.X + 4, bounds.Y + (bounds.Height - 18) / 2, 3, 18);
            using var indBrush = new SolidBrush(ThemeManager.IsDarkTheme ? Color.FromArgb(240, 240, 245) : Color.FromArgb(30, 30, 35));
            g.FillRectangle(indBrush, indicatorRect);
        }

        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var textBounds = new RectangleF(bounds.X + 16, bounds.Y, bounds.Width - 20, bounds.Height);
        using var sf = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };
        using var textBrush = new SolidBrush(foreColor);
        Font textFont = IsActive ? ThemeManager.BoldFont : Font;
        g.DrawString(Text, textFont, textBrush, textBounds, sf);
    }
}

public class RoundedTabButton : Button
{
    private bool _isHovered;
    public bool IsActive { get; set; }
    public int CornerRadius { get; set; } = 6;

    public RoundedTabButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint, true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        Height = 32;
        Width = 160;
        Margin = new Padding(0, 0, 8, 0);
        BackColor = Color.Transparent;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;

        Color parentBg = GraphicsPathHelper.GetEffectiveBackColor(Parent);
        g.Clear(parentBg);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Default;

        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        using var path = GraphicsPathHelper.CreateRoundedRectangle(bounds, CornerRadius);

        Color backColor;
        Color borderColor;
        Color foreColor;

        if (!Enabled)
        {
            backColor = Color.FromArgb(238, 238, 242);
            borderColor = Color.FromArgb(215, 215, 222);
            foreColor = Color.FromArgb(155, 155, 165);
        }
        else if (ThemeManager.IsDarkTheme)
        {
            if (IsActive)
            {
                backColor = Color.FromArgb(48, 48, 58);
                borderColor = Color.FromArgb(75, 75, 90);
                foreColor = Color.FromArgb(255, 255, 255);
            }
            else
            {
                backColor = _isHovered ? Color.FromArgb(34, 34, 40) : Color.FromArgb(26, 26, 30);
                borderColor = _isHovered ? Color.FromArgb(55, 55, 66) : Color.FromArgb(38, 38, 44);
                foreColor = _isHovered ? Color.FromArgb(240, 240, 245) : Color.FromArgb(160, 160, 172);
            }
        }
        else
        {
            if (IsActive)
            {
                backColor = Color.FromArgb(255, 255, 255);
                borderColor = Color.FromArgb(215, 215, 224);
                foreColor = Color.FromArgb(20, 20, 25);
            }
            else
            {
                backColor = _isHovered ? Color.FromArgb(235, 235, 242) : Color.FromArgb(244, 244, 248);
                borderColor = _isHovered ? Color.FromArgb(215, 215, 224) : Color.FromArgb(230, 230, 236);
                foreColor = _isHovered ? Color.FromArgb(20, 20, 25) : Color.FromArgb(100, 100, 110);
            }
        }

        using (var brush = new SolidBrush(backColor))
        {
            g.FillPath(brush, path);
        }

        if (borderColor != Color.Transparent)
        {
            using var pen = new Pen(borderColor, 1f);
            g.DrawPath(pen, path);
        }

        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        using var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };
        using var textBrush = new SolidBrush(foreColor);
        Font fontToUse = IsActive ? ThemeManager.BoldFont : Font;
        g.DrawString(Text, fontToUse, textBrush, new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), sf);
    }
}

public class RoundedTabPage : Panel
{
    public string Title { get; set; } = string.Empty;

    public RoundedTabPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.Transparent;
        Padding = new Padding(2);
    }

    public RoundedTabPage(string title) : this()
    {
        Title = title;
        Text = title;
    }
}

public class RoundedTabControl : UserControl
{
    private readonly FlowLayoutPanel _pnlHeader;
    private readonly Panel _pnlBody;
    private readonly List<RoundedTabPage> _tabPages = new();
    private readonly List<RoundedTabButton> _tabButtons = new();
    private int _selectedIndex = -1;

    public event EventHandler? SelectedIndexChanged;

    public class TabPageCollection : IEnumerable<RoundedTabPage>
    {
        private readonly RoundedTabControl _owner;

        public TabPageCollection(RoundedTabControl owner)
        {
            _owner = owner;
        }

        public int Count => _owner._tabPages.Count;

        public RoundedTabPage this[int index] => _owner._tabPages[index];

        public void Add(RoundedTabPage page) => _owner.AddTabPage(page);

        public void AddRange(IEnumerable<RoundedTabPage> pages)
        {
            foreach (var p in pages)
            {
                _owner.AddTabPage(p);
            }
        }

        public void Remove(RoundedTabPage page) => _owner.RemoveTabPage(page);

        public void Clear() => _owner.ClearTabPages();

        public IEnumerator<RoundedTabPage> GetEnumerator() => _owner._tabPages.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public TabPageCollection TabPages { get; }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value >= 0 && value < _tabPages.Count && value != _selectedIndex)
            {
                _selectedIndex = value;
                UpdateSelectedTab();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public RoundedTabPage? SelectedTab
    {
        get => _selectedIndex >= 0 && _selectedIndex < _tabPages.Count ? _tabPages[_selectedIndex] : null;
        set
        {
            if (value != null)
            {
                int idx = _tabPages.IndexOf(value);
                if (idx >= 0) SelectedIndex = idx;
            }
        }
    }

    public RoundedTabControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint, true);

        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        BackColor = Color.Transparent;

        TabPages = new TabPageCollection(this);

        _pnlHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(2, 2, 2, 4),
            Margin = new Padding(0, 0, 0, 4),
            BackColor = ThemeManager.BackgroundColor,
            WrapContents = false,
            AutoScroll = false
        };

        _pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.BackgroundColor,
            Padding = Padding.Empty
        };

        Controls.Add(_pnlBody);
        Controls.Add(_pnlHeader);
    }

    internal void AddTabPage(RoundedTabPage page)
    {
        page.BackColor = ThemeManager.BackgroundColor;
        page.ForeColor = ThemeManager.TextColor;
        page.Dock = DockStyle.Fill;

        _tabPages.Add(page);

        var btn = new RoundedTabButton
        {
            Text = string.IsNullOrEmpty(page.Title) ? page.Text : page.Title,
            Font = ThemeManager.StandardFont
        };
        btn.Click += (s, e) =>
        {
            if (s is RoundedTabButton tb)
            {
                int curIdx = _tabButtons.IndexOf(tb);
                if (curIdx >= 0) SelectedIndex = curIdx;
            }
        };

        _tabButtons.Add(btn);
        _pnlHeader.Controls.Add(btn);

        if (_selectedIndex == -1)
        {
            SelectedIndex = 0;
        }
    }

    internal void RemoveTabPage(RoundedTabPage page)
    {
        int idx = _tabPages.IndexOf(page);
        if (idx >= 0)
        {
            _tabPages.RemoveAt(idx);
            _pnlHeader.Controls.Remove(_tabButtons[idx]);
            _tabButtons.RemoveAt(idx);
            if (_selectedIndex >= _tabPages.Count)
            {
                _selectedIndex = _tabPages.Count - 1;
            }
            UpdateSelectedTab();
        }
    }

    internal void ClearTabPages()
    {
        _tabPages.Clear();
        _tabButtons.Clear();
        _pnlHeader.Controls.Clear();
        _pnlBody.Controls.Clear();
        _selectedIndex = -1;
    }

    private void UpdateSelectedTab()
    {
        _pnlBody.SuspendLayout();
        _pnlBody.Controls.Clear();

        for (int i = 0; i < _tabButtons.Count; i++)
        {
            bool active = (i == _selectedIndex);
            _tabButtons[i].IsActive = active;
            _tabButtons[i].Invalidate();
        }

        if (_selectedIndex >= 0 && _selectedIndex < _tabPages.Count)
        {
            var page = _tabPages[_selectedIndex];
            page.Dock = DockStyle.Fill;
            page.BackColor = ThemeManager.BackgroundColor;
            page.ForeColor = ThemeManager.TextColor;
            _pnlBody.Controls.Add(page);
        }

        _pnlBody.ResumeLayout(true);
    }

    public void UpdateTheme()
    {
        BackColor = ThemeManager.BackgroundColor;
        _pnlHeader.BackColor = ThemeManager.BackgroundColor;
        _pnlBody.BackColor = ThemeManager.BackgroundColor;

        for (int i = 0; i < _tabButtons.Count; i++)
        {
            _tabButtons[i].Invalidate();
        }

        foreach (var p in _tabPages)
        {
            p.BackColor = ThemeManager.BackgroundColor;
            p.ForeColor = ThemeManager.TextColor;
            ThemeManager.ApplyTheme(p);
        }
    }
}

public class RoundedPanel : Panel
{
    public int CornerRadius { get; set; } = 8;
    public bool DrawBorder { get; set; } = true;

    public RoundedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint, true);

        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;

        Color parentBg = GraphicsPathHelper.GetEffectiveBackColor(Parent);
        g.Clear(parentBg);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Default;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        using var path = GraphicsPathHelper.CreateRoundedRectangle(bounds, CornerRadius);

        Color backColor = ThemeManager.SurfaceColor;
        Color borderColor = ThemeManager.BorderColor;

        using (var brush = new SolidBrush(backColor))
        {
            g.FillPath(brush, path);
        }

        if (DrawBorder)
        {
            using var pen = new Pen(borderColor, 1f);
            g.DrawPath(pen, path);
        }
    }
}

public class RoundedFlowPanel : FlowLayoutPanel
{
    public int CornerRadius { get; set; } = 8;
    public bool DrawBorder { get; set; } = true;

    public RoundedFlowPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint, true);

        DoubleBuffered = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        WrapContents = true;
        Padding = new Padding(8, 8, 8, 8);
        Margin = new Padding(0, 0, 0, 8);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;

        Color parentBg = GraphicsPathHelper.GetEffectiveBackColor(Parent);
        g.Clear(parentBg);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Default;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        using var path = GraphicsPathHelper.CreateRoundedRectangle(bounds, CornerRadius);

        Color backColor = ThemeManager.SurfaceColor;
        Color borderColor = ThemeManager.BorderColor;

        using (var brush = new SolidBrush(backColor))
        {
            g.FillPath(brush, path);
        }

        if (DrawBorder)
        {
            using var pen = new Pen(borderColor, 1f);
            g.DrawPath(pen, path);
        }
    }
}
