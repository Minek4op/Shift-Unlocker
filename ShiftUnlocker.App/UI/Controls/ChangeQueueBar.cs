using System.Windows.Forms;
using ShiftUnlocker.Core.Common;

namespace ShiftUnlocker.App.UI.Controls;

public record QueuedChange(string Key, string Title, string BeforeAfterPreview, Func<Result> Apply);

public class ChangeQueueBar : RoundedPanel
{
    private readonly Label _lblSummary;
    private readonly RoundedButton _btnPreview;
    private readonly RoundedButton _btnApplyAll;
    private readonly RoundedButton _btnClear;
    private readonly List<QueuedChange> _queue = new();

    public Action? OnApplied { get; set; }

    public int Count => _queue.Count;

    public ChangeQueueBar()
    {
        Dock = DockStyle.Bottom;
        Height = 46;
        Padding = new Padding(10, 7, 10, 7);
        Margin = new Padding(0, 6, 0, 0);
        CornerRadius = 8;
        Visible = false;

        _lblSummary = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Font = ThemeManager.BoldFont,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var pnlBtns = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _btnPreview = new RoundedButton { Text = "Список...", Size = new Size(85, 30), Margin = new Padding(4, 0, 4, 0) };
        _btnApplyAll = new RoundedButton { Text = LangManager.T("queue_apply"), Size = new Size(125, 30), Font = ThemeManager.BoldFont, IsPrimary = true, Margin = new Padding(4, 0, 4, 0) };
        _btnClear = new RoundedButton { Text = LangManager.T("queue_clear"), Size = new Size(85, 30), Margin = new Padding(4, 0, 0, 0) };

        pnlBtns.Controls.AddRange(new Control[] { _btnPreview, _btnApplyAll, _btnClear });
        Controls.Add(_lblSummary);
        Controls.Add(pnlBtns);

        _btnPreview.Click += (s, e) => ShowPreviewDialog();
        _btnApplyAll.Click += (s, e) => ApplyAll();
        _btnClear.Click += (s, e) => Clear();

        LangManager.LanguageChanged += RefreshText;
    }

    public void Enqueue(QueuedChange change)
    {
        if (_queue.Any(x => string.Equals(x.Key, change.Key, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        _queue.Add(change);
        RefreshText();
    }

    public void Clear()
    {
        _queue.Clear();
        RefreshText();
    }

    private void RefreshText()
    {
        Visible = _queue.Count > 0;
        if (_queue.Count == 0) return;

        _btnApplyAll.Text = LangManager.T("queue_apply");
        _btnClear.Text = LangManager.T("queue_clear");
        string preview = string.Join(" | ", _queue.Take(2).Select(q => q.BeforeAfterPreview));
        if (_queue.Count > 2) preview += $" (+{_queue.Count - 2})";
        _lblSummary.Text = $"{LangManager.T("queue_title")} ({_queue.Count}): {preview}";
    }

    private void ShowPreviewDialog()
    {
        using var dlg = new Form
        {
            Text = $"{LangManager.T("queue_title")} ({_queue.Count})",
            Size = new Size(640, 360),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var tb = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = ThemeManager.MonoFont,
            BorderStyle = BorderStyle.None,
            Text = string.Join("\r\n", _queue.Select((q, i) => $"{i + 1}. {q.Title}\r\n   {q.BeforeAfterPreview}"))
        };

        var pnl = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
        var btnOk = new RoundedButton { Text = "OK", Size = new Size(85, 28), DialogResult = DialogResult.OK, IsPrimary = true, Dock = DockStyle.Right };
        pnl.Controls.Add(btnOk);

        dlg.Controls.Add(tb);
        dlg.Controls.Add(pnl);
        dlg.AcceptButton = btnOk;
        ThemeManager.ApplyTheme(dlg);
        dlg.ShowDialog(this);
    }

    private void ApplyAll()
    {
        if (_queue.Count == 0) return;

        int ok = 0;
        var errors = new List<string>();
        foreach (var item in _queue)
        {
            var res = item.Apply();
            if (res.Success) ok++;
            else if (!string.IsNullOrWhiteSpace(res.ErrorMessage)) errors.Add($"{item.Title}: {res.ErrorMessage}");
        }

        _queue.Clear();
        RefreshText();
        OnApplied?.Invoke();

        string msg = $"Применено изменений: {ok}.";
        if (errors.Count > 0) msg += "\nОшибки:\n" + string.Join("\n", errors.Take(5));
        MessageBox.Show(msg, "Очередь изменений", MessageBoxButtons.OK, errors.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }
}
