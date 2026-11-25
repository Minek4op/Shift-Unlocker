using System.Windows.Forms;
using Microsoft.Win32;
using ShiftUnlocker.App.UI.Controls;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.RegEditor;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.App.UI.Views;

public class RegEditorView : UserControl
{
    private readonly TextBox _txtAddress;
    private readonly RoundedButton _btnGo;
    private readonly ComboBox _cbBookmarks;
    private readonly TreeView _tvKeys;
    private readonly ListView _lvValues;

    private readonly RoundedButton _btnSetValue;
    private readonly RoundedButton _btnDeleteValue;
    private readonly RoundedButton _btnExportReg;
    private readonly RoundedButton _btnImportReg;
    private readonly RoundedButton _btnTakeOwnership;

    private readonly RegistryEditorService _editorService = new();

    public Func<ITargetContext>? GetContext { get; set; }
    public Func<BackupManager>? GetBackupManager { get; set; }
    public Action? OnStateChanged { get; set; }

    public RegEditorView()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        var topPanel = new RoundedFlowPanel { Dock = DockStyle.Top };

        var lblAddr = new Label { Text = "Путь:", AutoSize = true, Margin = new Padding(2, 7, 4, 4) };
        _txtAddress = new TextBox { Size = new Size(360, 24), Margin = new Padding(2, 4, 6, 4) };
        _btnGo = new RoundedButton { Text = "Перейти", Size = new Size(90, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnTakeOwnership = new RoundedButton { Text = "Права (Take Ownership)", Size = new Size(185, 28), Margin = new Padding(2, 2, 6, 4) };

        var lblBook = new Label { Text = "Закладки вирусов:", AutoSize = true, Margin = new Padding(4, 7, 4, 4) };
        _cbBookmarks = new ComboBox { Size = new Size(240, 24), DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(2, 4, 6, 4) };
        foreach (var b in RegistryEditorService.MalwareBookmarks)
        {
            _cbBookmarks.Items.Add(b.Name);
        }

        _btnSetValue = new RoundedButton { Text = "Записать", Size = new Size(85, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnDeleteValue = new RoundedButton { Text = "Удалить", Size = new Size(85, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnExportReg = new RoundedButton { Text = ".reg экспорт", Size = new Size(105, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnImportReg = new RoundedButton { Text = ".reg импорт", Size = new Size(105, 28), Margin = new Padding(2, 2, 6, 4) };

        topPanel.Controls.AddRange(new Control[]
        {
            lblAddr, _txtAddress, _btnGo, _btnTakeOwnership,
            lblBook, _cbBookmarks, _btnSetValue, _btnDeleteValue, _btnExportReg, _btnImportReg
        });

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 300
        };

        _tvKeys = new TreeView { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };
        ThemeManager.EnableDoubleBuffering(_tvKeys);

        _lvValues = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None
        };
        ThemeManager.EnableDoubleBuffering(_lvValues);

        _lvValues.Columns.Add("Имя значения", 180);
        _lvValues.Columns.Add("Тип", 110);
        _lvValues.Columns.Add("Данные", 380);

        split.Panel1.Controls.Add(_tvKeys);
        split.Panel2.Controls.Add(_lvValues);

        var centerPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        centerPanel.Controls.Add(split);

        Controls.Add(centerPanel);
        Controls.Add(topPanel);

        InitRootNodes();

        _tvKeys.BeforeExpand += (s, e) => LazyLoadSubKeys(e.Node);
        _tvKeys.AfterSelect += (s, e) => OnKeySelected(e.Node);
        _cbBookmarks.SelectedIndexChanged += (s, e) => JumpToBookmark();
        _btnGo.Click += (s, e) => NavigateToAddress(_txtAddress.Text);
        _btnSetValue.Click += (s, e) => EditOrCreateValue();
        _btnDeleteValue.Click += (s, e) => DeleteSelectedValue();
        _btnExportReg.Click += (s, e) => ExportRegFile();
        _btnImportReg.Click += (s, e) => ImportRegFile();
        _btnTakeOwnership.Click += (s, e) => TakeOwnershipCurrentKey();

        bool isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        _btnTakeOwnership.Enabled = isAdmin;
        _btnSetValue.Enabled = isAdmin;
        _btnDeleteValue.Enabled = isAdmin;
        _btnImportReg.Enabled = isAdmin;
    }

    private void InitRootNodes()
    {
        _tvKeys.Nodes.Clear();
        string[] rootHives = ["HKLM", "HKCU", "HKCR", "HKU", "HKCC"];

        foreach (var h in rootHives)
        {
            var node = new TreeNode(h) { Tag = h };
            node.Nodes.Add(new TreeNode("...") { Tag = "dummy" });
            _tvKeys.Nodes.Add(node);
        }
    }

    private void LazyLoadSubKeys(TreeNode? parent)
    {
        if (parent == null || parent.Nodes.Count != 1 || parent.Nodes[0].Tag?.ToString() != "dummy") return;

        parent.Nodes.Clear();
        var ctx = GetContext?.Invoke();
        if (ctx == null) return;

        string fullPath = parent.FullPath;
        int slash = fullPath.IndexOf('\\');
        string hive = slash > 0 ? fullPath[..slash] : fullPath;
        string subKey = slash > 0 ? fullPath[(slash + 1)..] : "";

        try
        {
            string[] subKeys = _editorService.GetSubKeys(ctx, hive, subKey);
            foreach (var k in subKeys)
            {
                var child = new TreeNode(k) { Tag = k };
                child.Nodes.Add(new TreeNode("...") { Tag = "dummy" });
                parent.Nodes.Add(child);
            }
        }
        catch { }
    }

    private void OnKeySelected(TreeNode? node)
    {
        if (node == null) return;
        _txtAddress.Text = node.FullPath;

        var ctx = GetContext?.Invoke();
        if (ctx == null) return;

        _lvValues.Items.Clear();

        string fullPath = node.FullPath;
        int slash = fullPath.IndexOf('\\');
        string hive = slash > 0 ? fullPath[..slash] : fullPath;
        string subKey = slash > 0 ? fullPath[(slash + 1)..] : "";

        var values = _editorService.GetValues(ctx, hive, subKey);
        foreach (var v in values)
        {
            var item = new ListViewItem(new[] { v.Name, v.KindName, v.FormattedData })
            {
                Tag = v
            };
            _lvValues.Items.Add(item);
        }
    }

    private void JumpToBookmark()
    {
        if (_cbBookmarks.SelectedIndex < 0 || _cbBookmarks.SelectedIndex >= RegistryEditorService.MalwareBookmarks.Length) return;
        var bm = RegistryEditorService.MalwareBookmarks[_cbBookmarks.SelectedIndex];
        string path = $"{bm.Hive}\\{bm.SubKey}";
        NavigateToAddress(path);
    }

    private void NavigateToAddress(string fullPath)
    {
        _txtAddress.Text = fullPath;
        var ctx = GetContext?.Invoke();
        if (ctx == null) return;

        _lvValues.Items.Clear();
        int slash = fullPath.IndexOf('\\');
        string hive = slash > 0 ? fullPath[..slash] : fullPath;
        string subKey = slash > 0 ? fullPath[(slash + 1)..] : "";

        var values = _editorService.GetValues(ctx, hive, subKey);
        foreach (var v in values)
        {
            var item = new ListViewItem(new[] { v.Name, v.KindName, v.FormattedData })
            {
                Tag = v
            };
            _lvValues.Items.Add(item);
        }
    }

    private void EditOrCreateValue()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        string path = _txtAddress.Text.Trim();
        int slash = path.IndexOf('\\');
        if (slash <= 0) return;

        string hive = path[..slash];
        string subKey = path[(slash + 1)..];

        string defaultName = _lvValues.SelectedItems.Count > 0 && _lvValues.SelectedItems[0].Tag is RegistryValueDisplay d ? d.Name : "";
        string valName = PromptInput("Имя значения (оставьте пустым для 'По умолчанию'):", defaultName);
        string valData = PromptInput("Данные значения (строка или число):", "");

        if (int.TryParse(valData, out int dwordVal))
        {
            _editorService.SetValue(ctx, bm, hive, subKey, valName, dwordVal, RegistryValueKind.DWord);
        }
        else
        {
            _editorService.SetValue(ctx, bm, hive, subKey, valName, valData, RegistryValueKind.String);
        }

        OnStateChanged?.Invoke();
        NavigateToAddress(path);
    }

    private void DeleteSelectedValue()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null || _lvValues.SelectedItems.Count == 0) return;

        string path = _txtAddress.Text.Trim();
        int slash = path.IndexOf('\\');
        if (slash <= 0) return;

        string hive = path[..slash];
        string subKey = path[(slash + 1)..];

        if (_lvValues.SelectedItems[0].Tag is RegistryValueDisplay d)
        {
            _editorService.DeleteValue(ctx, bm, hive, subKey, d.Name == "(По умолчанию)" ? "" : d.Name);
            OnStateChanged?.Invoke();
            NavigateToAddress(path);
        }
    }

    private void ExportRegFile()
    {
        var ctx = GetContext?.Invoke();
        if (ctx == null) return;

        string path = _txtAddress.Text.Trim();
        int slash = path.IndexOf('\\');
        if (slash <= 0) return;

        string hive = path[..slash];
        string subKey = path[(slash + 1)..];

        using var sfd = new SaveFileDialog
        {
            Filter = "Файл реестра (*.reg)|*.reg",
            FileName = $"RegExport_{DateTime.Now:yyyyMMdd_HHmmss}.reg"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            var res = _editorService.ExportKeyToReg(ctx, hive, subKey, sfd.FileName);
            MessageBox.Show(res.Success ? "Ветка успешно экспортирована." : res.ErrorMessage, ".reg экспорт", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }
    }

    private void ImportRegFile()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        using var ofd = new OpenFileDialog
        {
            Filter = "Файл реестра (*.reg)|*.reg",
            Title = "Выберите .reg файл для импорта"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            var res = _editorService.ImportRegFile(ctx, bm, ofd.FileName);
            MessageBox.Show(res.Success ? "Файл реестра успешно импортирован." : res.ErrorMessage, ".reg импорт", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            OnStateChanged?.Invoke();
            NavigateToAddress(_txtAddress.Text);
        }
    }

    private void TakeOwnershipCurrentKey()
    {
        string path = _txtAddress.Text.Trim();
        if (string.IsNullOrEmpty(path)) return;

        var res = _editorService.TakeOwnershipAndGrantAccess(path);
        MessageBox.Show(res.Success ? "Права владельца (Take Ownership) и Полный доступ успешно назначены Администраторам." : res.ErrorMessage, "Владелец ключа", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private static string PromptInput(string text, string defaultValue = "")
    {
        using var prompt = new Form
        {
            Width = 450,
            Height = 160,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Редактирование реестра",
            StartPosition = FormStartPosition.CenterParent
        };
        var lbl = new Label { Left = 20, Top = 15, Text = text, AutoSize = true };
        var textBox = new TextBox { Left = 20, Top = 40, Width = 390, Text = defaultValue };
        var btnOk = new RoundedButton { Text = "OK", Left = 210, Width = 85, Top = 80, DialogResult = DialogResult.OK, IsPrimary = true };
        var btnCancel = new RoundedButton { Text = "Отмена", Left = 305, Width = 85, Top = 80, DialogResult = DialogResult.Cancel };
        prompt.Controls.AddRange(new Control[] { lbl, textBox, btnOk, btnCancel });
        prompt.AcceptButton = btnOk;
        prompt.CancelButton = btnCancel;
        ThemeManager.ApplyTheme(prompt);

        return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
    }
}
