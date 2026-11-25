using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.FileManager;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.App.UI.Views;

public class FileManagerView : UserControl
{
    private readonly TextBox _txtPath;
    private readonly RoundedButton _btnBack;
    private readonly RoundedButton _btnForward;
    private readonly RoundedButton _btnUp;
    private readonly RoundedButton _btnRefresh;
    private readonly RoundedButton _btnGo;
    private readonly RoundedButton _btnOpenExplorer;

    private readonly RoundedFlowPanel _pnlDrives;

    private readonly RoundedButton _btnRun;
    private readonly RoundedButton _btnNotepad;
    private readonly RoundedButton _btnNewFolder;
    private readonly RoundedButton _btnRename;
    private readonly RoundedButton _btnUnhide;
    private readonly RoundedButton _btnTakeOwnership;
    private readonly RoundedButton _btnDelete;
    private readonly RoundedButton _btnDeleteLocked;
    private readonly RoundedButton _btnQuarantine;
    private readonly RoundedButton _btnViewQuarantine;

    private readonly TreeView _tvFolders;
    private readonly ListView _lvFiles;
    private readonly TextBox _txtDetails;
    private readonly ContextMenuStrip _contextMenu;

    private readonly FileManagerService _fileService = new();
    private string _currentDirectory = @"C:\";

    private readonly Stack<string> _backHistory = new();
    private readonly Stack<string> _forwardHistory = new();
    private bool _isNavigatingHistory = false;

    private int _sortColumn = 0;
    private bool _sortAscending = true;

    public Func<ITargetContext>? GetContext { get; set; }
    public Func<BackupManager>? GetBackupManager { get; set; }
    public Func<QuarantineManager>? GetQuarantineManager { get; set; }
    public Action? OnStateChanged { get; set; }

    public FileManagerView()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        // таблица чтобы панели сверху не наползали на проводник
        var rootTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        rootTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        rootTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        // строка навигации
        var navPanel = new RoundedFlowPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 4)
        };

        _btnBack = new RoundedButton { Text = "< Назад", Size = new Size(76, 28), Margin = new Padding(2, 2, 4, 4), Enabled = false };
        _btnForward = new RoundedButton { Text = "Вперед >", Size = new Size(80, 28), Margin = new Padding(2, 2, 6, 4), Enabled = false };
        _btnUp = new RoundedButton { Text = "Вверх", Size = new Size(68, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnRefresh = new RoundedButton { Text = "Обновить", Size = new Size(86, 28), Margin = new Padding(2, 2, 6, 4) };
        _txtPath = new TextBox { Width = 280, Height = 24, Margin = new Padding(2, 4, 6, 4), Font = ThemeManager.StandardFont };
        _btnGo = new RoundedButton { Text = "Перейти", Size = new Size(78, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnOpenExplorer = new RoundedButton { Text = "Проводник Windows", Size = new Size(155, 28), Margin = new Padding(2, 2, 6, 4) };

        navPanel.Controls.AddRange(new Control[]
        {
            _btnBack, _btnForward, _btnUp, _btnRefresh, _txtPath, _btnGo, _btnOpenExplorer
        });

        // панель дисков
        _pnlDrives = new RoundedFlowPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 4)
        };
        InitDriveButtons();

        // кнопки действий
        var opsPanel = new RoundedFlowPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 6)
        };

        _btnRun = new RoundedButton { Text = "Открыть / Запустить", Size = new Size(155, 28), Font = ThemeManager.BoldFont, IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };
        _btnNotepad = new RoundedButton { Text = "Блокнот", Size = new Size(80, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnNewFolder = new RoundedButton { Text = "+ Папка", Size = new Size(80, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnRename = new RoundedButton { Text = "Переименовать", Size = new Size(120, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnUnhide = new RoundedButton { Text = "Снять Скрытый/Системный", Size = new Size(185, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnTakeOwnership = new RoundedButton { Text = "Стать владельцем", Size = new Size(140, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnDelete = new RoundedButton { Text = "Удалить", Size = new Size(80, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnDeleteLocked = new RoundedButton { Text = "Удалить заблокированный", Size = new Size(190, 28), Font = ThemeManager.BoldFont, Margin = new Padding(2, 2, 6, 4) };
        _btnQuarantine = new RoundedButton { Text = "В карантин", Size = new Size(95, 28), Margin = new Padding(2, 2, 6, 4) };
        _btnViewQuarantine = new RoundedButton { Text = "Карантин...", Size = new Size(95, 28), Margin = new Padding(2, 2, 6, 4) };

        opsPanel.Controls.AddRange(new Control[]
        {
            _btnRun, _btnNotepad, _btnNewFolder, _btnRename, _btnUnhide, _btnTakeOwnership,
            _btnDelete, _btnDeleteLocked, _btnQuarantine, _btnViewQuarantine
        });

        // основная область: слева дерево, справа файлы и инфо
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 230,
            SplitterWidth = 6,
            Margin = Padding.Empty
        };

        // дерево папок
        _tvFolders = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            HideSelection = false
        };
        ThemeManager.EnableDoubleBuffering(_tvFolders);

        var pnlTree = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
        pnlTree.Controls.Add(_tvFolders);
        mainSplit.Panel1.Controls.Add(pnlTree);

        // справа: список файлов и детали снизу
        var rightSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            FixedPanel = FixedPanel.Panel2,
            Panel2MinSize = 85,
            SplitterWidth = 6
        };
        rightSplit.SizeChanged += (s, e) =>
        {
            if (rightSplit.Height > 220)
            {
                rightSplit.SplitterDistance = Math.Max(120, rightSplit.Height - 115);
            }
        };

        _lvFiles = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None,
            MultiSelect = false
        };
        ThemeManager.EnableDoubleBuffering(_lvFiles);

        _lvFiles.Columns.Add("Имя файла / папки", 260);
        _lvFiles.Columns.Add("Тип", 110);
        _lvFiles.Columns.Add("Размер", 90);
        _lvFiles.Columns.Add("Дата изменения", 135);
        _lvFiles.Columns.Add("Атрибуты", 75);
        _lvFiles.Columns.Add("Статус безопасности", 135);

        var pnlFiles = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
        pnlFiles.Controls.Add(_lvFiles);
        rightSplit.Panel1.Controls.Add(pnlFiles);

        // инфа по выбранному файлу
        var bottomDetailsPanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
        _txtDetails = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = ThemeManager.MonoFont,
            BorderStyle = BorderStyle.None
        };
        bottomDetailsPanel.Controls.Add(_txtDetails);
        rightSplit.Panel2.Controls.Add(bottomDetailsPanel);

        mainSplit.Panel2.Controls.Add(rightSplit);

        rootTable.Controls.Add(navPanel, 0, 0);
        rootTable.Controls.Add(_pnlDrives, 0, 1);
        rootTable.Controls.Add(opsPanel, 0, 2);
        rootTable.Controls.Add(mainSplit, 0, 3);

        Controls.Add(rootTable);

        // контекстное меню
        _contextMenu = new ContextMenuStrip();
        BuildContextMenu();
        _lvFiles.ContextMenuStrip = _contextMenu;

        // обработчики событий
        _btnBack.Click += (s, e) => NavigateBack();
        _btnForward.Click += (s, e) => NavigateForward();
        _btnUp.Click += (s, e) => NavigateUp();
        _btnRefresh.Click += (s, e) => NavigateTo(_currentDirectory, recordHistory: false);
        _btnGo.Click += (s, e) => NavigateTo(_txtPath.Text, recordHistory: true);
        _txtPath.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                NavigateTo(_txtPath.Text, recordHistory: true);
            }
        };

        _btnOpenExplorer.Click += (s, e) => LaunchSystemExplorer();

        _lvFiles.DoubleClick += (s, e) => OpenSelected();
        _lvFiles.SelectedIndexChanged += (s, e) => ShowSelectedDetails();
        _lvFiles.ColumnClick += (s, e) => SortFilesByColumn(e.Column);
        _lvFiles.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; OpenSelected(); }
            else if (e.KeyCode == Keys.Back) { e.SuppressKeyPress = true; NavigateUp(); }
            else if (e.KeyCode == Keys.F2) { e.SuppressKeyPress = true; RenameSelected(); }
            else if (e.KeyCode == Keys.Delete) { e.SuppressKeyPress = true; DeleteSelected(); }
            else if (e.KeyCode == Keys.F5) { e.SuppressKeyPress = true; NavigateTo(_currentDirectory, recordHistory: false); }
        };

        _tvFolders.BeforeExpand += TvFolders_BeforeExpand;
        _tvFolders.AfterSelect += TvFolders_AfterSelect;

        _btnRun.Click += (s, e) => OpenSelected();
        _btnNotepad.Click += (s, e) => OpenSelectedInNotepad();
        _btnNewFolder.Click += (s, e) => CreateNewFolder();
        _btnRename.Click += (s, e) => RenameSelected();
        _btnUnhide.Click += (s, e) => UnhideSelected();
        _btnTakeOwnership.Click += (s, e) => TakeOwnershipSelected();
        _btnDelete.Click += (s, e) => DeleteSelected();
        _btnDeleteLocked.Click += (s, e) => DeleteSelectedLocked();
        _btnQuarantine.Click += (s, e) => QuarantineSelected();
        _btnViewQuarantine.Click += (s, e) => ShowQuarantineDialog();

        // грузим дерево и стартуем с диска C
        InitFolderTree();
        string initialDir = Directory.Exists(@"C:\") ? @"C:\" : AppDomain.CurrentDomain.BaseDirectory;
        NavigateTo(initialDir, recordHistory: true);

        bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        _btnUnhide.Enabled = isAdmin;
        _btnTakeOwnership.Enabled = isAdmin;
        _btnDeleteLocked.Enabled = isAdmin;
        _btnQuarantine.Enabled = isAdmin;
    }

    private void BuildContextMenu()
    {
        _contextMenu.Items.Clear();

        var miRun = new ToolStripMenuItem("Открыть / Запустить", null, (s, e) => OpenSelected()) { Font = ThemeManager.BoldFont };
        var miRunAsAdmin = new ToolStripMenuItem("Запустить от имени Администратора", null, (s, e) => RunSelectedAsAdmin());
        var miNotepad = new ToolStripMenuItem("Открыть в Блокноте", null, (s, e) => OpenSelectedInNotepad());
        var miExplorer = new ToolStripMenuItem("Открыть в Проводнике Windows", null, (s, e) => OpenSelectedInExplorer());

        var miCopyPath = new ToolStripMenuItem("Копировать полный путь", null, (s, e) => CopySelectedPath());
        var miNewFolder = new ToolStripMenuItem("Создать новую папку...", null, (s, e) => CreateNewFolder());
        var miRename = new ToolStripMenuItem("Переименовать (F2)...", null, (s, e) => RenameSelected());

        var miUnhide = new ToolStripMenuItem("Снять атрибуты Скрытый и Системный", null, (s, e) => UnhideSelected());
        var miTakeOwner = new ToolStripMenuItem("Стать владельцем (Take Ownership)", null, (s, e) => TakeOwnershipSelected());
        var miQuarantine = new ToolStripMenuItem("Поместить в карантин", null, (s, e) => QuarantineSelected());

        var miDelete = new ToolStripMenuItem("Удалить", null, (s, e) => DeleteSelected());
        var miDeleteLocked = new ToolStripMenuItem("Принудительно удалить заблокированный (Unlocker)", null, (s, e) => DeleteSelectedLocked()) { Font = ThemeManager.BoldFont };

        var miProperties = new ToolStripMenuItem("Свойства и хеши (MD5 / SHA256 / PE)", null, (s, e) => ShowPropertiesDialog());

        _contextMenu.Items.AddRange(new ToolStripItem[]
        {
            miRun,
            miRunAsAdmin,
            miNotepad,
            miExplorer,
            new ToolStripSeparator(),
            miCopyPath,
            miNewFolder,
            miRename,
            new ToolStripSeparator(),
            miUnhide,
            miTakeOwner,
            miQuarantine,
            new ToolStripSeparator(),
            miDelete,
            miDeleteLocked,
            new ToolStripSeparator(),
            miProperties
        });
    }

    private void InitDriveButtons()
    {
        _pnlDrives.Controls.Clear();
        var lbl = new Label
        {
            Text = "Диски:",
            AutoSize = true,
            Padding = new Padding(4, 6, 4, 4),
            Font = ThemeManager.BoldFont
        };
        _pnlDrives.Controls.Add(lbl);

        foreach (var d in DriveInfo.GetDrives())
        {
            if (d.IsReady)
            {
                string label = $"{d.Name.TrimEnd('\\')}";
                var btn = new RoundedButton
                {
                    Text = label,
                    Size = new Size(54, 26),
                    Tag = d.RootDirectory.FullName,
                    CornerRadius = 4,
                    Margin = new Padding(2, 2, 4, 4)
                };
                btn.Click += (s, e) => NavigateTo(btn.Tag?.ToString() ?? @"C:\", recordHistory: true);
                _pnlDrives.Controls.Add(btn);
            }
        }
    }

    private void InitFolderTree()
    {
        _tvFolders.BeginUpdate();
        _tvFolders.Nodes.Clear();

        // быстрый доступ
        var quickNode = new TreeNode("Быстрый доступ") { Tag = "QUICK_ACCESS" };

        void AddQuick(string name, Environment.SpecialFolder folder)
        {
            try
            {
                string p = Environment.GetFolderPath(folder);
                if (Directory.Exists(p))
                {
                    var node = new TreeNode(name) { Tag = p };
                    node.Nodes.Add(new TreeNode("..."));
                    quickNode.Nodes.Add(node);
                }
            }
            catch { }
        }

        AddQuick("Рабочий стол", Environment.SpecialFolder.Desktop);
        try
        {
            string userProf = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string downloads = Path.Combine(userProf, "Downloads");
            if (Directory.Exists(downloads))
            {
                var dNode = new TreeNode("Загрузки") { Tag = downloads };
                dNode.Nodes.Add(new TreeNode("..."));
                quickNode.Nodes.Add(dNode);
            }
        }
        catch { }
        AddQuick("Документы", Environment.SpecialFolder.MyDocuments);
        AddQuick("Windows", Environment.SpecialFolder.Windows);
        AddQuick("System32", Environment.SpecialFolder.System);

        try
        {
            string sysRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
            string usersDir = Path.Combine(sysRoot, "Users");
            if (Directory.Exists(usersDir))
            {
                var uNode = new TreeNode("Пользователи") { Tag = usersDir };
                uNode.Nodes.Add(new TreeNode("..."));
                quickNode.Nodes.Add(uNode);
            }
        }
        catch { }

        quickNode.Expand();
        _tvFolders.Nodes.Add(quickNode);

        // логические диски
        var pcNode = new TreeNode("Этот компьютер") { Tag = "THIS_PC" };
        foreach (var d in DriveInfo.GetDrives())
        {
            if (d.IsReady)
            {
                string driveTitle = $"{d.Name} ({GetDriveTypeString(d.DriveType)})";
                var dNode = new TreeNode(driveTitle) { Tag = d.RootDirectory.FullName };
                dNode.Nodes.Add(new TreeNode("...")); // placeholder for expand
                pcNode.Nodes.Add(dNode);
            }
        }

        pcNode.Expand();
        _tvFolders.Nodes.Add(pcNode);

        _tvFolders.EndUpdate();
    }

    private static string GetDriveTypeString(DriveType dt) => dt switch
    {
        DriveType.Fixed => "Локальный диск",
        DriveType.Removable => "Съемный диск",
        DriveType.CDRom => "CD/DVD",
        DriveType.Network => "Сетевой",
        DriveType.Ram => "RAM-диск (WinPE)",
        _ => "Диск"
    };

    private void TvFolders_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node == null || e.Node.Tag is not string path || !Directory.Exists(path)) return;

        // подгружаем реальные подпапки вместо заглушки
        if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Text == "...")
        {
            e.Node.Nodes.Clear();
            try
            {
                var di = new DirectoryInfo(path);
                foreach (var sub in di.GetDirectories())
                {
                    var subNode = new TreeNode(sub.Name) { Tag = sub.FullName };
                    try
                    {
                        if (sub.GetDirectories().Length > 0)
                        {
                            subNode.Nodes.Add(new TreeNode("..."));
                        }
                    }
                    catch { }
                    e.Node.Nodes.Add(subNode);
                }
            }
            catch { }
        }
    }

    private void TvFolders_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is string path && Directory.Exists(path))
        {
            if (!string.Equals(path, _currentDirectory, StringComparison.OrdinalIgnoreCase))
            {
                NavigateTo(path, recordHistory: true, syncTree: false);
            }
        }
    }

    public void NavigateTo(string path, bool recordHistory = true, bool syncTree = true)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        path = path.Trim('\"', ' ');

        if (!Directory.Exists(path))
        {
            if (File.Exists(path))
            {
                string? p = Path.GetDirectoryName(path);
                if (p != null && Directory.Exists(p)) path = p;
                else return;
            }
            else return;
        }

        if (recordHistory && !_isNavigatingHistory && !string.Equals(path, _currentDirectory, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(_currentDirectory))
            {
                _backHistory.Push(_currentDirectory);
                _btnBack.Enabled = true;
            }
            _forwardHistory.Clear();
            _btnForward.Enabled = false;
        }

        _currentDirectory = path;
        _txtPath.Text = path;

        _lvFiles.BeginUpdate();
        _lvFiles.Items.Clear();

        var items = _fileService.ListDirectory(path);
        foreach (var it in items)
        {
            string szStr = it.IsDirectory ? "<ПАПКА>" : FormatSize(it.SizeBytes);
            string typeStr = it.IsDirectory ? "Папка с файлами" : GetFileTypeDescription(it.Name);
            string flagStr = it.IsHiddenOrSystem ? "[!] Скрытый/Системный" : "Обычный";

            var lvi = new ListViewItem(new[]
            {
                it.Name,
                typeStr,
                szStr,
                it.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                it.AttributesString,
                flagStr
            })
            {
                Tag = it,
                Font = it.IsHiddenOrSystem ? ThemeManager.BoldFont : ThemeManager.StandardFont
            };

            if (it.IsHiddenOrSystem)
            {
                lvi.ForeColor = Color.FromArgb(160, 40, 40);
            }

            _lvFiles.Items.Add(lvi);
        }
        _lvFiles.EndUpdate();

        if (syncTree)
        {
            SyncTreeToCurrentDirectory(path);
        }

        _txtDetails.Text = $"Каталог: {path} | Элементов: {items.Count}";
    }

    private void SyncTreeToCurrentDirectory(string path)
    {
        // синхронизируем выделение в дереве
        foreach (TreeNode root in _tvFolders.Nodes)
        {
            var match = FindNodeByPath(root, path);
            if (match != null)
            {
                _tvFolders.SelectedNode = match;
                match.EnsureVisible();
                break;
            }
        }
    }

    private TreeNode? FindNodeByPath(TreeNode parent, string path)
    {
        if (parent.Tag is string p && string.Equals(p, path, StringComparison.OrdinalIgnoreCase))
        {
            return parent;
        }
        foreach (TreeNode child in parent.Nodes)
        {
            var res = FindNodeByPath(child, path);
            if (res != null) return res;
        }
        return null;
    }

    private void NavigateBack()
    {
        if (_backHistory.Count == 0) return;
        _isNavigatingHistory = true;
        _forwardHistory.Push(_currentDirectory);
        _btnForward.Enabled = true;

        string prev = _backHistory.Pop();
        _btnBack.Enabled = _backHistory.Count > 0;
        NavigateTo(prev, recordHistory: false);
        _isNavigatingHistory = false;
    }

    private void NavigateForward()
    {
        if (_forwardHistory.Count == 0) return;
        _isNavigatingHistory = true;
        _backHistory.Push(_currentDirectory);
        _btnBack.Enabled = true;

        string next = _forwardHistory.Pop();
        _btnForward.Enabled = _forwardHistory.Count > 0;
        NavigateTo(next, recordHistory: false);
        _isNavigatingHistory = false;
    }

    private void NavigateUp()
    {
        var parent = Directory.GetParent(_currentDirectory);
        if (parent != null)
        {
            NavigateTo(parent.FullName, recordHistory: true);
        }
    }

    private void OpenSelected()
    {
        if (_lvFiles.SelectedItems.Count == 0) return;
        if (_lvFiles.SelectedItems[0].Tag is FileSystemItem item)
        {
            if (item.IsDirectory)
            {
                NavigateTo(item.FullPath, recordHistory: true);
            }
            else
            {
                OpenFileOrExecute(item.FullPath);
            }
        }
    }

    private void OpenFileOrExecute(string fullPath)
    {
        if (!File.Exists(fullPath)) return;

        string ext = Path.GetExtension(fullPath).ToLowerInvariant();

        // текстовые и конфиги открываем в блокноте
        if (ext is ".txt" or ".log" or ".ini" or ".cfg" or ".inf" or ".json" or ".xml" or ".reg" or ".csv")
        {
            LaunchProcess("notepad.exe", $"\"{fullPath}\"");
            return;
        }

        // бинарники и скрипты запускаем напрямую
        if (ext is ".exe" or ".bat" or ".cmd" or ".msi" or ".vbs" or ".ps1")
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fullPath,
                    WorkingDirectory = Path.GetDirectoryName(fullPath) ?? "",
                    UseShellExecute = true
                };
                Process.Start(psi);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска исполняемого файла:\n{ex.Message}", "Запуск", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        // остальное через оболочку, в WinPE предлагаем блокнот
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            if (MessageBox.Show($"Для файла '{Path.GetFileName(fullPath)}' нет зарегистрированного приложения.\nОткрыть его для просмотра в Блокноте?", "Открытие файла", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                LaunchProcess("notepad.exe", $"\"{fullPath}\"");
            }
        }
    }

    private void RunSelectedAsAdmin()
    {
        if (_lvFiles.SelectedItems.Count == 0 || _lvFiles.SelectedItems[0].Tag is not FileSystemItem item) return;
        if (item.IsDirectory) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = item.FullPath,
                WorkingDirectory = Path.GetDirectoryName(item.FullPath) ?? "",
                Verb = "runas",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось запустить от имени Администратора:\n{ex.Message}", "Запуск", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenSelectedInNotepad()
    {
        if (_lvFiles.SelectedItems.Count == 0 || _lvFiles.SelectedItems[0].Tag is not FileSystemItem item) return;
        if (!item.IsDirectory)
        {
            LaunchProcess("notepad.exe", $"\"{item.FullPath}\"");
        }
    }

    private void OpenSelectedInExplorer()
    {
        if (_lvFiles.SelectedItems.Count == 0 || _lvFiles.SelectedItems[0].Tag is not FileSystemItem item)
        {
            LaunchSystemExplorer();
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{item.FullPath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть файл в Проводнике Windows:\n{ex.Message}", "Проводник Windows", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void LaunchSystemExplorer()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = Directory.Exists(_currentDirectory) ? $"\"{_currentDirectory}\"" : "",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось запустить проводник Windows: {ex.Message}\n\nВ среде WinPE или при блокировке системы используйте Встроенный проводник Shift Unlocker.", "Проводник Windows", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private static void LaunchProcess(string exe, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopySelectedPath()
    {
        if (_lvFiles.SelectedItems.Count == 0 || _lvFiles.SelectedItems[0].Tag is not FileSystemItem item) return;
        try
        {
            Clipboard.SetText(item.FullPath);
        }
        catch { }
    }

    private void CreateNewFolder()
    {
        string name = PromptDialog("Создание папки", "Введите имя новой папки:", "Новая папка");
        if (string.IsNullOrWhiteSpace(name)) return;

        var res = _fileService.CreateFolder(_currentDirectory, name);
        if (res.Success)
        {
            NavigateTo(_currentDirectory, recordHistory: false);
        }
        else
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RenameSelected()
    {
        if (_lvFiles.SelectedItems.Count == 0 || _lvFiles.SelectedItems[0].Tag is not FileSystemItem item) return;

        string currentName = item.Name;
        string newName = PromptDialog("Переименование", "Введите новое имя:", currentName);
        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, currentName, StringComparison.OrdinalIgnoreCase)) return;

        var res = _fileService.RenameItem(item.FullPath, newName);
        if (res.Success)
        {
            NavigateTo(_currentDirectory, recordHistory: false);
        }
        else
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка переименования", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UnhideSelected()
    {
        if (_lvFiles.SelectedItems.Count == 0 || _lvFiles.SelectedItems[0].Tag is not FileSystemItem item) return;

        var res = _fileService.UnhideItem(item.FullPath, recursive: item.IsDirectory);
        if (res.Success)
        {
            MessageBox.Show($"Атрибуты 'Скрытый' и 'Системный' успешно сняты с:\n{item.Name}", "Атрибуты файлов", MessageBoxButtons.OK, MessageBoxIcon.Information);
            NavigateTo(_currentDirectory, recordHistory: false);
        }
        else
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteSelected()
    {
        if (_lvFiles.SelectedItems.Count == 0 || _lvFiles.SelectedItems[0].Tag is not FileSystemItem item) return;

        string label = item.IsDirectory ? $"папку '{item.Name}' и все её содержимое" : $"файл '{item.Name}'";
        if (MessageBox.Show($"Вы действительно хотите безвозвратно удалить {label}?", "Подтверждение удаления", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        var res = _fileService.DeleteItem(item.FullPath, recursive: true);
        if (res.Success)
        {
            NavigateTo(_currentDirectory, recordHistory: false);
        }
        else
        {
            MessageBox.Show($"Не удалось удалить элемент:\n{res.ErrorMessage}\n\nЕсли файл заблокирован вирусом или процессом, используйте 'Удалить заблокированный'.", "Удаление", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteSelectedLocked()
    {
        if (_lvFiles.SelectedItems.Count == 0 || _lvFiles.SelectedItems[0].Tag is not FileSystemItem item) return;
        var bm = GetBackupManager?.Invoke();
        if (bm == null) return;

        if (MessageBox.Show($"Принудительно удалить заблокированный файл:\n{item.FullPath}?\n\nФайл будет автоматически сохранен в резервную копию. Если он занят системным процессом или вирусом, защита будет снята, а удаление запланировано при перезагрузке (MoveFileEx).", "Удаление заблокированного", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        var res = _fileService.DeleteLockedFile(item.FullPath, bm);
        MessageBox.Show(res.Success ? "Файл успешно разблокирован и удален (или запланирован к удалению)." : res.ErrorMessage, "Удаление", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        OnStateChanged?.Invoke();
        NavigateTo(_currentDirectory, recordHistory: false);
    }

    private void QuarantineSelected()
    {
        if (_lvFiles.SelectedItems.Count == 0 || _lvFiles.SelectedItems[0].Tag is not FileSystemItem item) return;
        var qm = GetQuarantineManager?.Invoke();
        if (qm == null) return;

        if (item.IsDirectory)
        {
            MessageBox.Show("Изоляция в карантин применяется к отдельным файлам.", "Карантин", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var res = qm.QuarantineFile(item.FullPath, "Ручная изоляция из Встроенного проводника");
        if (res.Success)
        {
            MessageBox.Show("Файл успешно зашифрован и изолирован в карантин.", "Карантин", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OnStateChanged?.Invoke();
            NavigateTo(_currentDirectory, recordHistory: false);
        }
        else
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка карантина", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void TakeOwnershipSelected()
    {
        if (_lvFiles.SelectedItems.Count == 0 || _lvFiles.SelectedItems[0].Tag is not FileSystemItem item) return;
        var res = _fileService.TakeOwnershipOfPath(item.FullPath);
        MessageBox.Show(res.Success ? "Владелец успешно изменен на группу Администраторов, предоставлены полные права доступа (Full Control)." : res.ErrorMessage, "Права доступа", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private void ShowPropertiesDialog()
    {
        if (_lvFiles.SelectedItems.Count == 0 || _lvFiles.SelectedItems[0].Tag is not FileSystemItem item) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Элемент: {item.Name}");
        sb.AppendLine($"Полный путь: {item.FullPath}");
        sb.AppendLine($"Тип: {(item.IsDirectory ? "Папка" : GetFileTypeDescription(item.Name))}");
        if (!item.IsDirectory) sb.AppendLine($"Размер: {FormatSize(item.SizeBytes)} ({item.SizeBytes:N0} байт)");
        sb.AppendLine($"Дата изменения: {item.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Атрибуты: {item.Attributes}");
        sb.AppendLine();

        if (!item.IsDirectory)
        {
            var (md5, sha1, sha256) = _fileService.CalculateHashes(item.FullPath);
            sb.AppendLine($"MD5:    {md5}");
            sb.AppendLine($"SHA1:   {sha1}");
            sb.AppendLine($"SHA256: {sha256}");
            sb.AppendLine();

            var pe = PeHeaderReader.ReadPeInfo(item.FullPath);
            if (pe.IsValidPe)
            {
                sb.AppendLine("Сведения PE-файла:");
                sb.AppendLine($"  Архитектура:  {pe.Architecture}");
                sb.AppendLine($"  Подсистема:   {pe.Subsystem}");
                sb.AppendLine($"  Секций:       {pe.NumberOfSections}");
                sb.AppendLine($"  Компиляция:   {pe.TimeDateStamp}");
            }
        }

        using var dlg = new Form
        {
            Text = $"Свойства: {item.Name}",
            Size = new Size(620, 420),
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
            Text = sb.ToString(),
            BorderStyle = BorderStyle.None
        };

        var pnl = new Panel { Dock = DockStyle.Bottom, Height = 45, Padding = new Padding(8) };
        var btnOk = new RoundedButton { Text = "Закрыть", Size = new Size(95, 28), DialogResult = DialogResult.OK, IsPrimary = true, Dock = DockStyle.Right };
        pnl.Controls.Add(btnOk);

        dlg.Controls.Add(tb);
        dlg.Controls.Add(pnl);
        dlg.AcceptButton = btnOk;
        ThemeManager.ApplyTheme(dlg);
        dlg.ShowDialog(this);
    }

    private void ShowSelectedDetails()
    {
        if (_lvFiles.SelectedItems.Count == 0 || _lvFiles.SelectedItems[0].Tag is not FileSystemItem item)
        {
            _txtDetails.Text = $"Каталог: {_currentDirectory}";
            return;
        }

        if (item.IsDirectory)
        {
            _txtDetails.Text = $"[ПАПКА] {item.FullPath}\r\nДата изменения: {item.LastWriteTime:yyyy-MM-dd HH:mm:ss} | Атрибуты: {item.Attributes}";
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Файл: {item.FullPath}  |  Размер: {FormatSize(item.SizeBytes)} ({item.SizeBytes:N0} байт)");
        sb.AppendLine($"Дата изменения: {item.LastWriteTime:yyyy-MM-dd HH:mm:ss}  |  Атрибуты: {item.AttributesString}");

        var (md5, sha1, sha256) = _fileService.CalculateHashes(item.FullPath);
        sb.AppendLine($"MD5:    {md5}  |  SHA256: {sha256}");

        var pe = PeHeaderReader.ReadPeInfo(item.FullPath);
        if (pe.IsValidPe)
        {
            sb.AppendLine($"PE-заголовок: {pe.Architecture}, Подсистема: {pe.Subsystem}, Секций: {pe.NumberOfSections}, Сборка: {pe.TimeDateStamp}");
        }

        // быстрый предпросмотр мелких текстовых файлов
        string ext = Path.GetExtension(item.FullPath).ToLowerInvariant();
        if (item.SizeBytes <= 64 * 1024 && ext is ".txt" or ".log" or ".ini" or ".cfg" or ".inf" or ".bat" or ".cmd" or ".ps1" or ".reg")
        {
            try
            {
                string preview = File.ReadAllText(item.FullPath);
                if (preview.Length > 300) preview = preview.Substring(0, 300) + "... [содержимое сокращено]";
                sb.AppendLine("--- Предпросмотр текста ---");
                sb.AppendLine(preview);
            }
            catch { }
        }

        _txtDetails.Text = sb.ToString();
    }

    private void SortFilesByColumn(int column)
    {
        if (_sortColumn == column)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }

        _lvFiles.ListViewItemSorter = new FileListViewComparer(column, _sortAscending);
        _lvFiles.Sort();
    }

    private class FileListViewComparer : System.Collections.IComparer
    {
        private readonly int _col;
        private readonly bool _asc;

        public FileListViewComparer(int col, bool asc)
        {
            _col = col;
            _asc = asc;
        }

        public int Compare(object? x, object? y)
        {
            if (x is not ListViewItem lviX || y is not ListViewItem lviY) return 0;
            if (lviX.Tag is not FileSystemItem itemX || lviY.Tag is not FileSystemItem itemY) return 0;

            // папки всегда сверху
            if (itemX.IsDirectory && !itemY.IsDirectory) return -1;
            if (!itemX.IsDirectory && itemY.IsDirectory) return 1;

            int res = 0;
            switch (_col)
            {
                case 0: // Name
                    res = string.Compare(itemX.Name, itemY.Name, StringComparison.OrdinalIgnoreCase);
                    break;
                case 1: // Type
                    res = string.Compare(lviX.SubItems[1].Text, lviY.SubItems[1].Text, StringComparison.OrdinalIgnoreCase);
                    break;
                case 2: // Size
                    res = itemX.SizeBytes.CompareTo(itemY.SizeBytes);
                    break;
                case 3: // Date
                    res = itemX.LastWriteTime.CompareTo(itemY.LastWriteTime);
                    break;
                default:
                    res = string.Compare(lviX.SubItems[_col].Text, lviY.SubItems[_col].Text, StringComparison.OrdinalIgnoreCase);
                    break;
            }

            return _asc ? res : -res;
        }
    }

    private void ShowQuarantineDialog()
    {
        var qm = GetQuarantineManager?.Invoke();
        if (qm == null) return;

        using var dlg = new Form
        {
            Text = "Карантин изолированных файлов (SU_Quarantine)",
            Size = new Size(740, 420),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog
        };

        var lv = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None
        };
        ThemeManager.EnableDoubleBuffering(lv);
        lv.Columns.Add("Имя файла", 150);
        lv.Columns.Add("Дата изоляции", 130);
        lv.Columns.Add("Причина", 180);
        lv.Columns.Add("Исходный путь", 240);

        var pnl = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };
        var btnRestore = new RoundedButton { Text = "Восстановить файл", Size = new Size(160, 30), Location = new Point(10, 10), IsPrimary = true };
        var btnDel = new RoundedButton { Text = "Удалить навсегда", Size = new Size(160, 30), Location = new Point(180, 10) };

        pnl.Controls.AddRange(new Control[] { btnRestore, btnDel });
        dlg.Controls.Add(lv);
        dlg.Controls.Add(pnl);

        void LoadQuarantine()
        {
            lv.Items.Clear();
            foreach (var q in qm.ListQuarantined())
            {
                var lvi = new ListViewItem(new[] { q.FileName, q.QuarantineDate.ToString("yyyy-MM-dd HH:mm"), q.Reason, q.OriginalPath }) { Tag = q };
                lv.Items.Add(lvi);
            }
        }

        btnRestore.Click += (s, e) =>
        {
            if (lv.SelectedItems.Count > 0 && lv.SelectedItems[0].Tag is QuarantineItem qi)
            {
                var res = qm.RestoreFile(qi.Sha256);
                MessageBox.Show(res.Success ? "Файл восстановлен." : res.ErrorMessage, "Карантин", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                LoadQuarantine();
                NavigateTo(_currentDirectory, recordHistory: false);
            }
        };

        btnDel.Click += (s, e) =>
        {
            if (lv.SelectedItems.Count > 0 && lv.SelectedItems[0].Tag is QuarantineItem qi)
            {
                var res = qm.DeletePermanently(qi.Sha256);
                MessageBox.Show(res.Success ? "Файл удален навсегда." : res.ErrorMessage, "Карантин", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                LoadQuarantine();
            }
        };

        LoadQuarantine();
        ThemeManager.ApplyTheme(dlg);
        dlg.ShowDialog();
    }

    private static string PromptDialog(string title, string text, string defaultValue)
    {
        using var prompt = new Form
        {
            Width = 450,
            Height = 160,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
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

    private static string GetFileTypeDescription(string filename)
    {
        string ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext switch
        {
            ".exe" => "Приложение (EXE)",
            ".dll" => "Библиотека (DLL)",
            ".sys" => "Драйвер (SYS)",
            ".bat" or ".cmd" => "Командный файл",
            ".ps1" => "Скрипт PowerShell",
            ".vbs" => "Скрипт VBScript",
            ".reg" => "Файл реестра",
            ".txt" => "Текстовый документ",
            ".log" => "Журнал (LOG)",
            ".ini" or ".cfg" or ".inf" => "Конфигурация",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "Архив",
            ".iso" => "Образ диска",
            ".lnk" => "Ярлык",
            ".msi" => "Установщик Windows",
            _ => string.IsNullOrEmpty(ext) ? "Файл" : $"Файл {ext.ToUpperInvariant()}"
        };
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} Б";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} КБ";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} МБ";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} ГБ";
    }
}
