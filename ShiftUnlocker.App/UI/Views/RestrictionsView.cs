using System.Diagnostics;
using System.Windows.Forms;
using ShiftUnlocker.App.UI.Controls;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.Restrictions;
using ShiftUnlocker.Core.Modules.SystemRecovery;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.App.UI.Views;

public class RestrictionsView : UserControl
{
    private readonly RoundedTabControl _tabControl;
    private readonly RoundedTabPage _tabRestrictions;
    private readonly RoundedTabPage _tabThemeAndLang;
    private readonly RoundedTabPage _tabAccessibility;
    private readonly RoundedTabPage _tabUsers;

    // вкладка ограничений
    private readonly ListView _lvRestrictions;
    private readonly ListViewGroup _grpBuiltIn;
    private readonly ListViewGroup _grpCustom;
    private readonly RoundedButton _btnRefresh;
    private readonly RoundedButton _btnUnlockSelected;
    private readonly RoundedButton _btnLockSelected;
    private readonly RoundedButton _btnUnlockAll;
    private readonly RoundedButton _btnAddCustom;
    private readonly RoundedButton _btnDeleteCustom;
    private readonly RoundedButton _btnUnhideDrive;
    private readonly RoundedButton _btnFixAssociations;
    private readonly RoundedButton _btnResetProxy;
    private readonly ComboBox _cbCategory;
    private readonly Label _lblInfo;

    // шрифты, тема и язык
    private readonly RoundedButton _btnFixFonts;
    private readonly RoundedButton _btnRestoreTheme;
    private readonly RoundedButton _btnRestoreRussian;
    private readonly TextBox _txtThemeInfo;

    // замена sethc и utilman
    private readonly Label _lblSethcStatus;
    private readonly Label _lblUtilmanStatus;
    private readonly RoundedButton _btnSethcCmd;
    private readonly RoundedButton _btnSethcApp;
    private readonly RoundedButton _btnUtilmanCmd;
    private readonly RoundedButton _btnUtilmanApp;
    private readonly RoundedButton _btnRestoreAccess;
    private readonly RoundedButton _btnRestoreAccessFiles;
    private readonly RoundedButton _btnCheckAccess;
    private readonly TextBox _txtAccessInfo;

    // пользователи
    private readonly ListView _lvUsers;
    private readonly RoundedButton _btnUserRefresh;
    private readonly RoundedButton _btnResetPassword;
    private readonly RoundedButton _btnMakeAdmin;
    private readonly RoundedButton _btnToggleActive;
    private readonly RoundedButton _btnCreateUser;
    private readonly RoundedButton _btnDeleteUser;
    private readonly RoundedButton _btnLogoff;

    private readonly RestrictionsService _restrictionsService = new();
    private readonly AdvancedUnlockerService _advancedUnlocker = new();
    private readonly SystemRecoveryService _systemRecovery = new();
    private readonly UserManagerService _userManager = new();

    private List<RestrictionItem> _items = new();

    public Func<ITargetContext>? GetContext { get; set; }
    public Func<BackupManager>? GetBackupManager { get; set; }
    public Action? OnStateChanged { get; set; }

    public RestrictionsView()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        _tabControl = new RoundedTabControl { Dock = DockStyle.Fill };
        _tabRestrictions = new RoundedTabPage { Text = "Ограничения и Политики" };
        _tabThemeAndLang = new RoundedTabPage { Text = "Шрифты, Тема и Язык" };
        _tabAccessibility = new RoundedTabPage { Text = "Замена Sethc и Utilman" };
        _tabUsers = new RoundedTabPage { Text = "Управление пользователями" };

        _tabControl.TabPages.AddRange(new[] { _tabRestrictions, _tabThemeAndLang, _tabAccessibility, _tabUsers });

        // настройка вкладки ограничений
        var resTop = new RoundedFlowPanel { Dock = DockStyle.Top };
        _btnRefresh = new RoundedButton { Text = "Обновить", Size = new Size(95, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnUnlockSelected = new RoundedButton { Text = "Разблокировать", Size = new Size(135, 30), Font = ThemeManager.BoldFont, Margin = new Padding(2, 2, 6, 4) };
        _btnLockSelected = new RoundedButton { Text = "Заблокировать", Size = new Size(135, 30), Font = ThemeManager.BoldFont, Margin = new Padding(2, 2, 6, 4) };
        _btnUnlockAll = new RoundedButton { Text = "Разблокировать ВСЕ", Size = new Size(165, 30), Font = ThemeManager.BoldFont, IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };
        _btnAddCustom = new RoundedButton { Text = "+ Добавить свое...", Size = new Size(145, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnDeleteCustom = new RoundedButton { Text = "Удалить свое", Size = new Size(120, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnFixAssociations = new RoundedButton { Text = "Ассоциации .exe/.bat/.reg", Size = new Size(190, 30), Margin = new Padding(2, 2, 6, 4) };

        var lblCat = new Label { Text = "Категория:", AutoSize = true, Margin = new Padding(4, 7, 2, 4) };
        _cbCategory = new ComboBox { Size = new Size(155, 24), DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(2, 4, 6, 4) };
        _cbCategory.Items.AddRange(new object[] { "Все категории", "Инструменты", "Оболочка", "Защита", "Накопители", "Файлы", "Пользовательские" });
        _cbCategory.SelectedIndex = 0;

        _btnUnhideDrive = new RoundedButton { Text = "Снять скрытые атрибуты папки", Size = new Size(210, 30), Margin = new Padding(2, 2, 6, 4) };
        _btnResetProxy = new RoundedButton { Text = "Сбросить Proxy", Size = new Size(130, 30), Margin = new Padding(2, 2, 6, 4) };
        _lblInfo = new Label { Text = "", AutoSize = true, ForeColor = ThemeManager.TextMutedColor, Margin = new Padding(4, 7, 4, 4) };

        resTop.Controls.AddRange(new Control[]
        {
            _btnRefresh, _btnUnlockSelected, _btnLockSelected, _btnUnlockAll,
            _btnAddCustom, _btnDeleteCustom, _btnFixAssociations,
            lblCat, _cbCategory, _btnUnhideDrive, _btnResetProxy, _lblInfo
        });

        _grpBuiltIn = new ListViewGroup("Встроенные ограничения (система)", HorizontalAlignment.Left);
        _grpCustom = new ListViewGroup("Пользовательские ограничения (добавленные вами)", HorizontalAlignment.Left);

        _lvRestrictions = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None,
            ShowGroups = true
        };
        ThemeManager.EnableDoubleBuffering(_lvRestrictions);
        _lvRestrictions.Groups.AddRange(new[] { _grpBuiltIn, _grpCustom });

        _lvRestrictions.Columns.Add("Статус", 160);
        _lvRestrictions.Columns.Add("Категория", 120);
        _lvRestrictions.Columns.Add("Ограничение / Блокировка", 230);
        _lvRestrictions.Columns.Add("Текущее значение", 140);
        _lvRestrictions.Columns.Add("Описание воздействия", 380);

        var resMenu = new ContextMenuStrip();
        resMenu.Items.Add("Разблокировать выбранное", null, (s, e) => UnlockSelected());
        resMenu.Items.Add("Заблокировать выбранное", null, (s, e) => LockSelected());
        resMenu.Items.Add(new ToolStripSeparator());
        resMenu.Items.Add("Добавить свое ограничение...", null, (s, e) => AddCustomRestriction());
        resMenu.Items.Add("Удалить пользовательское ограничение", null, (s, e) => DeleteCustomSelected());
        _lvRestrictions.ContextMenuStrip = resMenu;

        var resCenter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        resCenter.Controls.Add(_lvRestrictions);

        _tabRestrictions.Controls.Add(resCenter);
        _tabRestrictions.Controls.Add(resTop);

        // настройка шрифтов, темы и языка
        var themeTop = new RoundedFlowPanel { Dock = DockStyle.Top };
        _btnFixFonts = new RoundedButton { Text = "🔤 Исправление шрифтов", Size = new Size(195, 32), Font = ThemeManager.BoldFont, Margin = new Padding(2, 2, 8, 4) };
        _btnRestoreTheme = new RoundedButton { Text = "🎨 Вернуть в стандартную тему", Size = new Size(240, 32), Font = ThemeManager.BoldFont, Margin = new Padding(2, 2, 8, 4) };
        _btnRestoreRussian = new RoundedButton { Text = "🇷🇺 Вернуть русский язык (CP1251)", Size = new Size(250, 32), Font = ThemeManager.BoldFont, Margin = new Padding(2, 2, 8, 4) };

        themeTop.Controls.AddRange(new Control[] { _btnFixFonts, _btnRestoreTheme, _btnRestoreRussian });

        _txtThemeInfo = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = ThemeManager.MonoFont,
            BorderStyle = BorderStyle.None
        };
        InitThemeInfoText();

        var themeCenter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        themeCenter.Controls.Add(_txtThemeInfo);

        _tabThemeAndLang.Controls.Add(themeCenter);
        _tabThemeAndLang.Controls.Add(themeTop);

        // настройка sethc / utilman
        var accessTop = new RoundedFlowPanel { Dock = DockStyle.Top };
        _btnSethcCmd = new RoundedButton { Text = "Заменить sethc на CMD (Shift x5)", Size = new Size(230, 32), Font = ThemeManager.BoldFont, Margin = new Padding(2, 2, 6, 4) };
        _btnSethcApp = new RoundedButton { Text = "Заменить sethc на Shift Unlocker", Size = new Size(230, 32), Margin = new Padding(2, 2, 6, 4) };
        _btnUtilmanCmd = new RoundedButton { Text = "Заменить utilman на CMD (Win+U)", Size = new Size(235, 32), Font = ThemeManager.BoldFont, Margin = new Padding(2, 2, 6, 4) };
        _btnUtilmanApp = new RoundedButton { Text = "Заменить utilman на Shift Unlocker", Size = new Size(235, 32), Margin = new Padding(2, 2, 6, 4) };
        _btnRestoreAccess = new RoundedButton { Text = "Восстановить (IFEO / .bak)", Size = new Size(195, 32), Margin = new Padding(2, 2, 6, 4) };
        _btnRestoreAccessFiles = new RoundedButton { Text = "Восстановить файлы sethc.exe и Utilman.exe", Size = new Size(305, 32), Font = ThemeManager.BoldFont, IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };
        _btnCheckAccess = new RoundedButton { Text = "Проверить статус", Size = new Size(140, 32), Margin = new Padding(2, 2, 6, 4) };

        accessTop.Controls.AddRange(new Control[]
        {
            _btnSethcCmd, _btnSethcApp, _btnUtilmanCmd, _btnUtilmanApp, _btnRestoreAccess, _btnRestoreAccessFiles, _btnCheckAccess
        });

        var accessHeader = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(8) };
        _lblSethcStatus = new Label { Text = "sethc.exe (Залипание клавиш): Проверка...", Font = ThemeManager.BoldFont, Location = new Point(10, 8), AutoSize = true };
        _lblUtilmanStatus = new Label { Text = "utilman.exe (Спец. возможности): Проверка...", Font = ThemeManager.BoldFont, Location = new Point(10, 32), AutoSize = true };
        accessHeader.Controls.AddRange(new Control[] { _lblSethcStatus, _lblUtilmanStatus });

        _txtAccessInfo = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = ThemeManager.MonoFont,
            BorderStyle = BorderStyle.None
        };
        InitAccessInfoText();

        var accessCenter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        accessCenter.Controls.Add(_txtAccessInfo);
        accessCenter.Controls.Add(accessHeader);

        _tabAccessibility.Controls.Add(accessCenter);
        _tabAccessibility.Controls.Add(accessTop);

        // настройка пользователей
        var userTop = new RoundedFlowPanel { Dock = DockStyle.Top };
        _btnResetPassword = new RoundedButton { Text = "🔑 Сброс пароля", Size = new Size(140, 32), Font = ThemeManager.BoldFont, IsPrimary = true, Margin = new Padding(2, 2, 6, 4) };
        _btnMakeAdmin = new RoundedButton { Text = "🛡️ Сделать Админом", Size = new Size(160, 32), Margin = new Padding(2, 2, 6, 4) };
        _btnToggleActive = new RoundedButton { Text = "Вкл / Выкл запись", Size = new Size(140, 32), Margin = new Padding(2, 2, 6, 4) };
        _btnCreateUser = new RoundedButton { Text = "➕ Создать пользователя...", Size = new Size(190, 32), Margin = new Padding(2, 2, 6, 4) };
        _btnDeleteUser = new RoundedButton { Text = "❌ Удалить", Size = new Size(100, 32), Margin = new Padding(2, 2, 6, 4) };
        _btnLogoff = new RoundedButton { Text = "🚪 Выйти из пользователя", Size = new Size(190, 32), Margin = new Padding(2, 2, 6, 4) };
        _btnUserRefresh = new RoundedButton { Text = "⟳ Обновить", Size = new Size(105, 32), Margin = new Padding(2, 2, 6, 4) };

        userTop.Controls.AddRange(new Control[]
        {
            _btnResetPassword, _btnMakeAdmin, _btnToggleActive, _btnCreateUser,
            _btnDeleteUser, _btnLogoff, _btnUserRefresh
        });

        _lvUsers = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None
        };
        ThemeManager.EnableDoubleBuffering(_lvUsers);
        _lvUsers.Columns.Add("Имя пользователя", 180);
        _lvUsers.Columns.Add("Полное имя", 180);
        _lvUsers.Columns.Add("Роль / Группа", 140);
        _lvUsers.Columns.Add("Активен", 90);
        _lvUsers.Columns.Add("Требуется пароль", 130);
        _lvUsers.Columns.Add("Комментарий", 240);

        var userCenter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        userCenter.Controls.Add(_lvUsers);

        _tabUsers.Controls.Add(userCenter);
        _tabUsers.Controls.Add(userTop);

        Controls.Add(_tabControl);

        // обработчики событий
        _btnRefresh.Click += (s, e) => RefreshRestrictions();
        _cbCategory.SelectedIndexChanged += (s, e) => ApplyFilter();
        _btnUnlockSelected.Click += (s, e) => UnlockSelected();
        _btnLockSelected.Click += (s, e) => LockSelected();
        _btnUnlockAll.Click += (s, e) => UnlockAll();
        _btnAddCustom.Click += (s, e) => AddCustomRestriction();
        _btnDeleteCustom.Click += (s, e) => DeleteCustomSelected();
        _btnFixAssociations.Click += (s, e) => FixAssociations();
        _btnUnhideDrive.Click += (s, e) => UnhideDriveRoot();
        _btnResetProxy.Click += (s, e) => ResetProxy();

        _btnFixFonts.Click += (s, e) => RunFixFonts();
        _btnRestoreTheme.Click += (s, e) => RunRestoreTheme();
        _btnRestoreRussian.Click += (s, e) => RunRestoreRussian();

        _btnSethcCmd.Click += (s, e) => RunReplaceSethcCmd();
        _btnSethcApp.Click += (s, e) => RunReplaceSethcApp();
        _btnUtilmanCmd.Click += (s, e) => RunReplaceUtilmanCmd();
        _btnUtilmanApp.Click += (s, e) => RunReplaceUtilmanApp();
        _btnRestoreAccess.Click += (s, e) => RunRestoreAccessibility();
        _btnRestoreAccessFiles.Click += (s, e) => RunRestoreAccessibilityFiles();
        _btnCheckAccess.Click += (s, e) => RefreshAccessibilityStatus();

        _btnUserRefresh.Click += (s, e) => RefreshUsers();
        _btnResetPassword.Click += (s, e) => RunResetPassword();
        _btnMakeAdmin.Click += (s, e) => RunMakeAdmin();
        _btnToggleActive.Click += (s, e) => RunToggleActive();
        _btnCreateUser.Click += (s, e) => RunCreateUser();
        _btnDeleteUser.Click += (s, e) => RunDeleteUser();
        _btnLogoff.Click += (s, e) => RunLogoff();

        RefreshAccessibilityStatus();
        RefreshUsers();

        bool isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        _btnUnlockSelected.Enabled = isAdmin;
        _btnLockSelected.Enabled = isAdmin;
        _btnUnlockAll.Enabled = isAdmin;
        _btnFixAssociations.Enabled = isAdmin;
        _btnUnhideDrive.Enabled = isAdmin;
        _btnResetProxy.Enabled = isAdmin;

        _btnFixFonts.Enabled = isAdmin;
        _btnRestoreTheme.Enabled = isAdmin;
        _btnRestoreRussian.Enabled = isAdmin;

        _btnSethcCmd.Enabled = isAdmin;
        _btnSethcApp.Enabled = isAdmin;
        _btnUtilmanCmd.Enabled = isAdmin;
        _btnUtilmanApp.Enabled = isAdmin;
        _btnRestoreAccess.Enabled = isAdmin;
        _btnRestoreAccessFiles.Enabled = isAdmin;

        _btnResetPassword.Enabled = isAdmin;
        _btnMakeAdmin.Enabled = isAdmin;
        _btnToggleActive.Enabled = isAdmin;
        _btnCreateUser.Enabled = isAdmin;
        _btnDeleteUser.Enabled = isAdmin;
    }

    private void InitThemeInfoText()
    {
        _txtThemeInfo.Text =
            "========================================================================================\r\n" +
            "  ИНСТРУМЕНТЫ ВОССТАНОВЛЕНИЯ ИНТЕРФЕЙСА, ШРИФТОВ И ЛОКАЛИЗАЦИИ WINDOWS\r\n" +
            "========================================================================================\r\n\r\n" +
            "1. Исправление шрифтов:\r\n" +
            "   - Восстанавливает стандартные значения в реестре FontSubstitutes:\r\n" +
            "     MS Shell Dlg -> Tahoma, MS Shell Dlg 2 -> Tahoma, Segoe UI, Arial, Times New Roman.\r\n" +
            "   - Восстанавливает стандартные регистрации TrueType шрифтов в реестре.\r\n" +
            "   - Очищает поврежденный кэш шрифтов Windows (FontCache*.dat и GDIPFONTCACHEV1.DAT).\r\n" +
            "   - Решает проблему квадратиков (тофу), невидимых букв и испорченных шрифтов после вирусов.\r\n\r\n" +
            "2. Вернуть в стандартную тему:\r\n" +
            "   - Сбрасывает текущую тему Windows на стандартную тему Aero (aero.theme).\r\n" +
            "   - Удаляет вредоносные фоновые изображения (баннеры вымогателей с рабочего стола).\r\n" +
            "   - Восстанавливает дефолтные цвета окон, текста и фона рабочего стола.\r\n" +
            "   - Переводит службу Темы (Themes) в режим автозапуска и запускает её.\r\n\r\n" +
            "3. Вернуть русский язык (CP1251):\r\n" +
            "   - Восстанавливает кодовые страницы кириллицы (1251 -> c_1251.nls, 866, 10007).\r\n" +
            "   - Устанавливает 1250 и 1252 на c_1251.nls (классическое исправление 'кракозябр' в русских программах).\r\n" +
            "   - Восстанавливает русскую локаль по умолчанию (0419) и формат региона ru-RU.\r\n";
    }

    private void InitAccessInfoText()
    {
        _txtAccessInfo.Text =
            "========================================================================================\r\n" +
            "  ЗАМЕНА SETHC.EXE И UTILMAN.EXE (АВАРИЙНЫЙ ДОСТУП И СБРОС БЛОКИРОВКИ)\r\n" +
            "========================================================================================\r\n\r\n" +
            "Как это работает:\r\n" +
            "1. sethc.exe — системная утилита 'Залипание клавиш'. Вызывается при 5-кратном нажатии клавиши Shift\r\n" +
            "   на экране блокировки Windows (Logon Screen).\r\n" +
            "2. utilman.exe — утилита 'Специальные возможности'. Вызывается кнопкой в правом нижнем углу экрана\r\n" +
            "   входа или сочетанием клавиш Win + U.\r\n\r\n" +
            "Возможности Shift Unlocker:\r\n" +
            "- При замене на CMD или Shift Unlocker через безопасный механизм IFEO (Image File Execution Options),\r\n" +
            "  вызов залипания клавиш (Shift 5 раз) или иконки доступности мгновенно открывает окно командной строки\r\n" +
            "  или Shift Unlocker с правами NT AUTHORITY\\SYSTEM без необходимости ввода пароля Windows!\r\n" +
            "- Метод IFEO не повреждает и не модифицирует файлы в System32, полностью безопасен и откатывается\r\n" +
            "  одной кнопкой 'Восстановить оригиналы'.\r\n" +
            "- Также используется при лечении системы, если вредоносное ПО само перехватило sethc/utilman.\r\n";
    }

    public void RefreshRestrictions()
    {
        var ctx = GetContext?.Invoke();
        if (ctx == null) return;

        _items = _restrictionsService.CheckAll(ctx);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        _lvRestrictions.Items.Clear();
        string selectedCat = _cbCategory.SelectedItem?.ToString() ?? "Все категории";

        var filtered = _items.AsEnumerable();
        if (selectedCat != "Все категории")
        {
            filtered = filtered.Where(x => x.Category.Equals(selectedCat, StringComparison.OrdinalIgnoreCase));
        }

        int blockedCount = 0;
        foreach (var r in filtered)
        {
            string statusStr = r.CurrentStatus switch
            {
                RestrictionStatus.Restricted => "ЗАБЛОКИРОВАНО",
                RestrictionStatus.Allowed => "Разрешено",
                _ => "Не задано"
            };

            if (r.CurrentStatus == RestrictionStatus.Restricted) blockedCount++;

            var item = new ListViewItem(new[]
            {
                statusStr,
                r.Category,
                r.Title,
                r.CurrentRawValue?.ToString() ?? "(null)",
                r.Description
            })
            {
                Tag = r,
                Group = r.IsCustom ? _grpCustom : _grpBuiltIn,
                Font = r.CurrentStatus == RestrictionStatus.Restricted ? ThemeManager.BoldFont : ThemeManager.StandardFont
            };

            _lvRestrictions.Items.Add(item);
        }

        _lblInfo.Text = $"Заблокировано: {blockedCount} из {_items.Count}";
    }

    private void UnlockSelected()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        if (_lvRestrictions.SelectedItems.Count == 0)
        {
            MessageBox.Show("Выберите хотя бы один пункт в списке.", "Shift Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int unlockedCount = 0;
        foreach (ListViewItem it in _lvRestrictions.SelectedItems)
        {
            if (it.Tag is RestrictionItem r)
            {
                var res = r.Unlock(ctx, bm);
                if (res.Success) unlockedCount++;
            }
        }

        OnStateChanged?.Invoke();
        RefreshRestrictions();
        MessageBox.Show($"Разблокировано элементов: {unlockedCount}.\nПараметры успешно сброшены в стандартные значения.", "Снятие ограничений", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void LockSelected()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        if (_lvRestrictions.SelectedItems.Count == 0)
        {
            MessageBox.Show("Выберите хотя бы один пункт в списке для блокировки.", "Shift Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int lockedCount = 0;
        var errors = new List<string>();
        foreach (ListViewItem it in _lvRestrictions.SelectedItems)
        {
            if (it.Tag is RestrictionItem r)
            {
                var res = r.Lock(ctx, bm);
                if (res.Success) lockedCount++;
                else if (!string.IsNullOrEmpty(res.ErrorMessage)) errors.Add(res.ErrorMessage);
            }
        }

        OnStateChanged?.Invoke();
        RefreshRestrictions();

        string msg = $"Заблокировано элементов: {lockedCount}.";
        if (errors.Count > 0) msg += "\nОшибки: " + string.Join("; ", errors);
        MessageBox.Show(msg, "Блокировка", MessageBoxButtons.OK, errors.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private void AddCustomRestriction()
    {
        using var dlg = new Form
        {
            Text = "Добавить свое ограничение / параметр реестра",
            Size = new Size(520, 470),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        int y = 16;
        Label MkLbl(string txt) => new Label { Left = 16, Top = y + 3, Width = 160, Text = txt };
        TextBox MkTxt(string def = "") => new TextBox { Left = 180, Top = y, Width = 310, Text = def };

        var lblTitle = MkLbl("Название:"); var txtTitle = MkTxt("Запрет панели управления"); y += 34;
        var lblCat = MkLbl("Категория:");
        var cbCat = new ComboBox { Left = 180, Top = y, Width = 310, DropDownStyle = ComboBoxStyle.DropDown };
        cbCat.Items.AddRange(new object[] { "Пользовательские", "Инструменты", "Оболочка", "Защита", "Накопители", "Файлы" });
        cbCat.SelectedIndex = 0;
        y += 34;

        var lblHive = MkLbl("Раздел (Hive):");
        var cbHive = new ComboBox { Left = 180, Top = y, Width = 310, DropDownStyle = ComboBoxStyle.DropDownList };
        cbHive.Items.AddRange(new object[] { "HKCU", "HKLM" });
        cbHive.SelectedIndex = 0;
        y += 34;

        var lblKey = MkLbl("Путь ключа:"); var txtKey = MkTxt(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"); y += 34;
        var lblVal = MkLbl("Имя параметра:"); var txtVal = MkTxt("NoControlPanel"); y += 34;

        var lblKind = MkLbl("Тип значения:");
        var cbKind = new ComboBox { Left = 180, Top = y, Width = 310, DropDownStyle = ComboBoxStyle.DropDownList };
        cbKind.Items.AddRange(new object[] { "DWord", "String" });
        cbKind.SelectedIndex = 0;
        y += 34;

        var lblRest = MkLbl("Значение блокировки:"); var txtRest = MkTxt("1"); y += 34;
        var lblAllow = MkLbl("Значение разблокировки:"); var txtAllow = MkTxt("0"); y += 34;
        var chkDelete = new CheckBox { Left = 180, Top = y, Width = 310, Text = "Удалять параметр при разблокировке", Checked = true }; y += 30;
        var lblDesc = MkLbl("Описание:"); var txtDesc = MkTxt("Пользовательское правило"); y += 42;

        var btnOk = new RoundedButton { Text = "Сохранить", Left = 285, Top = y, Size = new Size(100, 32), DialogResult = DialogResult.OK, IsPrimary = true };
        var btnCancel = new RoundedButton { Text = "Отмена", Left = 395, Top = y, Size = new Size(95, 32), DialogResult = DialogResult.Cancel };

        dlg.Controls.AddRange(new Control[]
        {
            lblTitle, txtTitle, lblCat, cbCat, lblHive, cbHive, lblKey, txtKey,
            lblVal, txtVal, lblKind, cbKind, lblRest, txtRest, lblAllow, txtAllow,
            chkDelete, lblDesc, txtDesc, btnOk, btnCancel
        });
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;
        ThemeManager.ApplyTheme(dlg);

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var dto = new CustomRestrictionDto
        {
            Title = txtTitle.Text.Trim(),
            Category = string.IsNullOrWhiteSpace(cbCat.Text) ? "Пользовательские" : cbCat.Text.Trim(),
            Hive = cbHive.SelectedItem?.ToString() ?? "HKCU",
            SubKey = txtKey.Text.Trim(),
            ValueName = txtVal.Text.Trim(),
            ValueKind = cbKind.SelectedItem?.ToString() ?? "DWord",
            RestrictedValue = txtRest.Text.Trim(),
            AllowedValue = txtAllow.Text.Trim(),
            DeleteToAllow = chkDelete.Checked,
            Description = txtDesc.Text.Trim()
        };

        var res = _restrictionsService.AddCustomRestriction(dto);
        if (res.Success)
        {
            RefreshRestrictions();
            MessageBox.Show("Пользовательское правило добавлено в отдельный блок «Пользовательские ограничения».", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteCustomSelected()
    {
        if (_lvRestrictions.SelectedItems.Count == 0 || _lvRestrictions.SelectedItems[0].Tag is not RestrictionItem r)
        {
            MessageBox.Show("Выберите пользовательское ограничение для удаления.", "Удаление", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!r.IsCustom)
        {
            MessageBox.Show("Встроенные системные ограничения удалять нельзя. Вы можете удалять только добавленные вами значения.", "Встроенное ограничение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show($"Удалить добавленное правило «{r.Title}»?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        var res = _restrictionsService.RemoveCustomRestriction(r.Id);
        if (res.Success)
        {
            RefreshRestrictions();
        }
        else
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UnlockAll()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        if (MessageBox.Show("Снять все системные ограничения, блокировки диспетчера задач, реестра, cmd и проводника?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _restrictionsService.UnlockAll(ctx, bm, _items);
        OnStateChanged?.Invoke();
        RefreshRestrictions();
        MessageBox.Show("Все системные блокировки и политики успешно сняты.", "Shift Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void FixAssociations()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        var res = _advancedUnlocker.RestoreFileAssociations(ctx, bm);
        MessageBox.Show(res.Success ? "Ассоциации .exe, .bat, .cmd, .reg, .lnk успешно восстановлены." : res.ErrorMessage, "Ассоциации", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        OnStateChanged?.Invoke();
    }

    private void UnhideDriveRoot()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        using var fbd = new FolderBrowserDialog
        {
            Description = "Выберите диск или папку для снятия скрытых и системных атрибутов",
            InitialDirectory = ctx.TargetRootPath
        };

        if (fbd.ShowDialog() == DialogResult.OK)
        {
            var res = _advancedUnlocker.UnhideDirectoryContents(fbd.SelectedPath, bm);
            MessageBox.Show(res.Success ? "Скрытые и системные атрибуты успешно сняты." : res.ErrorMessage, "Атрибуты", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            OnStateChanged?.Invoke();
        }
    }

    private void ResetProxy()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        var res = _advancedUnlocker.ResetProxySettings(ctx, bm);
        MessageBox.Show(res.Success ? "Настройки прокси успешно сброшены." : res.ErrorMessage, "Прокси", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        OnStateChanged?.Invoke();
    }

    // обработчики темы и шрифтов
    private void RunFixFonts()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        var res = _systemRecovery.FixFonts(ctx, bm);
        MessageBox.Show(res.Success ? "Шрифты и параметры подстановки FontSubstitutes успешно восстановлены. Кэш шрифтов очищен." : res.ErrorMessage, "Исправление шрифтов", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        OnStateChanged?.Invoke();
    }

    private void RunRestoreTheme()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        var res = _systemRecovery.RestoreDefaultTheme(ctx, bm);
        MessageBox.Show(res.Success ? "Тема Windows сброшена на стандартную (Aero), обои и цвета восстановлены, служба Themes запущена." : res.ErrorMessage, "Восстановление темы", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        OnStateChanged?.Invoke();
    }

    private void RunRestoreRussian()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        var res = _systemRecovery.RestoreRussianLanguage(ctx, bm);
        MessageBox.Show(res.Success ? "Кодовые страницы кириллицы (CP1251, 866) и русская локаль 0419 успешно восстановлены. Перезагрузите ПК для полного применения." : res.ErrorMessage, "Русский язык", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        OnStateChanged?.Invoke();
    }

    // обработчики sethc / utilman
    public void RefreshAccessibilityStatus()
    {
        var ctx = GetContext?.Invoke();
        if (ctx == null) return;

        var stat = _systemRecovery.CheckAccessibilityStatus(ctx);
        _lblSethcStatus.Text = stat.SethcIsReplaced
            ? $"sethc.exe (Shift x5): ПЕРЕХВАЧЕН / ЗАМЕНЕН -> {stat.SethcTarget}"
            : "sethc.exe (Shift x5): Штатный системный файл";
        _lblSethcStatus.ForeColor = stat.SethcIsReplaced ? Color.FromArgb(180, 40, 40) : Color.FromArgb(20, 120, 40);

        _lblUtilmanStatus.Text = stat.UtilmanIsReplaced
            ? $"utilman.exe (Win+U): ПЕРЕХВАЧЕН / ЗАМЕНЕН -> {stat.UtilmanTarget}"
            : "utilman.exe (Win+U): Штатный системный файл";
        _lblUtilmanStatus.ForeColor = stat.UtilmanIsReplaced ? Color.FromArgb(180, 40, 40) : Color.FromArgb(20, 120, 40);
    }

    private void RunReplaceSethcCmd()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        var res = _systemRecovery.ReplaceSethcWithCmd(ctx, bm);
        MessageBox.Show(res.Success ? "sethc.exe успешно перенаправлен на cmd.exe! Теперь при 5-кратном нажатии Shift на экране блокировки откроется командная строка." : res.ErrorMessage, "Замена sethc", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        RefreshAccessibilityStatus();
    }

    private void RunReplaceSethcApp()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        string appPath = Environment.ProcessPath ?? Application.ExecutablePath;
        var res = _systemRecovery.ReplaceSethcWithApp(ctx, bm, appPath);
        MessageBox.Show(res.Success ? "sethc.exe успешно перенаправлен на Shift Unlocker! Теперь при 5-кратном нажатии Shift на экране блокировки откроется данная программа." : res.ErrorMessage, "Замена sethc", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        RefreshAccessibilityStatus();
    }

    private void RunReplaceUtilmanCmd()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        var res = _systemRecovery.ReplaceUtilmanWithCmd(ctx, bm);
        MessageBox.Show(res.Success ? "utilman.exe успешно перенаправлен на cmd.exe! Нажмите иконку специальных возможностей на экране входа для открытия CMD." : res.ErrorMessage, "Замена utilman", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        RefreshAccessibilityStatus();
    }

    private void RunReplaceUtilmanApp()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        string appPath = Environment.ProcessPath ?? Application.ExecutablePath;
        var res = _systemRecovery.ReplaceUtilmanWithApp(ctx, bm, appPath);
        MessageBox.Show(res.Success ? "utilman.exe успешно перенаправлен на Shift Unlocker! Нажмите иконку специальных возможностей на экране входа для открытия Shift Unlocker." : res.ErrorMessage, "Замена utilman", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        RefreshAccessibilityStatus();
    }

    private void RunRestoreAccessibility()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        var res = _systemRecovery.RestoreOriginalAccessibility(ctx, bm);
        MessageBox.Show(res.Success ? "Штатные sethc.exe и utilman.exe успешно восстановлены в оригинальное состояние." : res.ErrorMessage, "Восстановление", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        RefreshAccessibilityStatus();
    }

    private void RunRestoreAccessibilityFiles()
    {
        var ctx = GetContext?.Invoke();
        var bm = GetBackupManager?.Invoke();
        if (ctx == null || bm == null) return;

        if (MessageBox.Show(
            $"Восстановить оригинальные файлы sethc.exe и Utilman.exe из резервной копии (.bak) или WinSxS в:\n{ctx.System32Directory}?\n\nТекущие файлы (если есть) будут сохранены в бэкап.",
            "Восстановление файлов", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        var res = _systemRecovery.RestoreAccessibilityFilesFromWinSxS(ctx, bm);
        MessageBox.Show(res.Success ? "Оригинальные файлы sethc.exe и Utilman.exe успешно восстановлены в System32!" : res.ErrorMessage, "Восстановление файлов", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        OnStateChanged?.Invoke();
        RefreshAccessibilityStatus();
    }

    // обработчики пользователей
    public void RefreshUsers()
    {
        _lvUsers.Items.Clear();
        var users = _userManager.GetUsers();
        foreach (var u in users)
        {
            string role = u.IsAdmin ? "[!] Администратор" : "Пользователь";
            string active = u.IsActive ? "Да" : "[Отключен]";
            string passReq = u.PasswordRequired ? "Да" : "Нет";

            var lvi = new ListViewItem(new[]
            {
                u.Username,
                u.FullName,
                role,
                active,
                passReq,
                u.Comment
            })
            {
                Tag = u,
                Font = u.IsAdmin ? ThemeManager.BoldFont : ThemeManager.StandardFont
            };

            if (!u.IsActive) lvi.ForeColor = ThemeManager.TextMutedColor;
            _lvUsers.Items.Add(lvi);
        }
    }

    private void RunResetPassword()
    {
        if (_lvUsers.SelectedItems.Count == 0 || _lvUsers.SelectedItems[0].Tag is not UserAccountInfo user)
        {
            MessageBox.Show("Выберите учетную запись для сброса пароля.", "Сброс пароля", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string newPass = PromptDialog("Сброс пароля", $"Введите новый пароль для пользователя '{user.Username}':\n(Оставьте поле пустым, чтобы сбросить пароль в пустой)", "");
        var res = _userManager.ResetPassword(user.Username, newPass);
        if (res.Success)
        {
            MessageBox.Show($"Пароль для пользователя '{user.Username}' успешно изменен.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshUsers();
        }
        else
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RunMakeAdmin()
    {
        if (_lvUsers.SelectedItems.Count == 0 || _lvUsers.SelectedItems[0].Tag is not UserAccountInfo user)
        {
            MessageBox.Show("Выберите пользователя.", "Права Администратора", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var res = _userManager.MakeAdministrator(user.Username);
        if (res.Success)
        {
            MessageBox.Show($"Пользователь '{user.Username}' успешно добавлен в группу Администраторы.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshUsers();
        }
        else
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RunToggleActive()
    {
        if (_lvUsers.SelectedItems.Count == 0 || _lvUsers.SelectedItems[0].Tag is not UserAccountInfo user)
        {
            MessageBox.Show("Выберите пользователя.", "Активность", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        bool newActive = !user.IsActive;
        var res = _userManager.SetAccountActive(user.Username, newActive);
        if (res.Success)
        {
            MessageBox.Show($"Пользователь '{user.Username}' теперь {(newActive ? "активен" : "отключен")}.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshUsers();
        }
        else
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RunCreateUser()
    {
        string username = PromptDialog("Создание пользователя", "Введите имя нового пользователя:", "NewUser");
        if (string.IsNullOrWhiteSpace(username)) return;

        string password = PromptDialog("Пароль нового пользователя", $"Введите пароль для '{username}':", "");

        bool makeAdmin = MessageBox.Show($"Сделать пользователя '{username}' Администратором?", "Права", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        var res = _userManager.CreateUser(username, password, makeAdmin);
        if (res.Success)
        {
            MessageBox.Show($"Пользователь '{username}' успешно создан.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshUsers();
        }
        else
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка создания", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RunDeleteUser()
    {
        if (_lvUsers.SelectedItems.Count == 0 || _lvUsers.SelectedItems[0].Tag is not UserAccountInfo user)
        {
            MessageBox.Show("Выберите пользователя для удаления.", "Удаление", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show($"Вы действительно хотите безвозвратно удалить учетную запись '{user.Username}'?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        var res = _userManager.DeleteUser(user.Username);
        if (res.Success)
        {
            MessageBox.Show($"Пользователь '{user.Username}' успешно удален.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshUsers();
        }
        else
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка удаления", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RunLogoff()
    {
        if (MessageBox.Show("Вы действительно хотите завершить текущий сеанс пользователя Windows (Logoff)?\nВсе несохраненные данные будут закрыты.", "Выход из системы", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        var res = _systemRecovery.LogoffCurrentUser();
        if (!res.Success)
        {
            MessageBox.Show(res.ErrorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
}
