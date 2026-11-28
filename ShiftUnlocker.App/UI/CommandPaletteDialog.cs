using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;

namespace ShiftUnlocker.App.UI;

public record PaletteCommand(string Title, string Category, Action Execute);

public class CommandPaletteDialog : Form
{
    private readonly TextBox _txtSearch;
    private readonly ListBox _lbResults;
    private readonly List<PaletteCommand> _allCommands;
    private List<PaletteCommand> _filtered = new();

    public CommandPaletteDialog(IEnumerable<PaletteCommand> commands)
    {
        _allCommands = commands.ToList();

        Text = LangManager.CurrentLanguage == AppLanguage.Ru
            ? "Палитра команд (Ctrl+K)"
            : "Command Palette (Ctrl+K)";
        Size = new Size(540, 380);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;

        _txtSearch = new TextBox
        {
            Dock = DockStyle.Top,
            Font = ThemeManager.LargeBoldFont,
            Height = 32
        };

        _lbResults = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = ThemeManager.StandardFont,
            ItemHeight = 24,
            DrawMode = DrawMode.OwnerDrawFixed
        };
        _lbResults.DrawItem += LbResults_DrawItem;

        var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        pnl.Controls.Add(_lbResults);
        pnl.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8 });
        pnl.Controls.Add(_txtSearch);

        Controls.Add(pnl);

        _txtSearch.TextChanged += (s, e) => Filter();
        _lbResults.DoubleClick += (s, e) => RunSelected();

        _txtSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Down && _lbResults.Items.Count > 0)
            {
                _lbResults.SelectedIndex = Math.Min(_lbResults.Items.Count - 1, _lbResults.SelectedIndex + 1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up && _lbResults.Items.Count > 0)
            {
                _lbResults.SelectedIndex = Math.Max(0, _lbResults.SelectedIndex - 1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                RunSelected();
            }
        };

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
        };

        Filter();
        ThemeManager.ApplyTheme(this);
        _txtSearch.Focus();
    }

    private void LbResults_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _filtered.Count) return;

        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        Color bg = selected ? Color.FromArgb(24, 24, 28) : ThemeManager.SurfaceColor;
        Color fg = selected ? Color.White : ThemeManager.TextColor;
        Color muted = selected ? Color.FromArgb(190, 190, 200) : ThemeManager.TextMutedColor;

        using (var br = new SolidBrush(bg)) e.Graphics.FillRectangle(br, e.Bounds);

        var cmd = _filtered[e.Index];
        TextRenderer.DrawText(e.Graphics, cmd.Title, ThemeManager.BoldFont,
            new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 150, e.Bounds.Height),
            fg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        TextRenderer.DrawText(e.Graphics, cmd.Category, ThemeManager.StandardFont,
            new Rectangle(e.Bounds.Right - 140, e.Bounds.Y, 132, e.Bounds.Height),
            muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
    }

    private void Filter()
    {
        string q = _txtSearch.Text.Trim();
        _filtered = string.IsNullOrEmpty(q)
            ? _allCommands.ToList()
            : _allCommands.Where(c =>
                c.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        _lbResults.Items.Clear();
        foreach (var c in _filtered) _lbResults.Items.Add(c.Title);
        if (_lbResults.Items.Count > 0) _lbResults.SelectedIndex = 0;
    }

    private void RunSelected()
    {
        if (_lbResults.SelectedIndex < 0 || _lbResults.SelectedIndex >= _filtered.Count) return;
        var cmd = _filtered[_lbResults.SelectedIndex];
        DialogResult = DialogResult.OK;
        Close();
        cmd.Execute();
    }
}
