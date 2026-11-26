using HotelComplexApp.Data;
using HotelComplexApp.Models;
using HotelComplexApp.Services;

namespace HotelComplexApp;

public class MainForm : Form
{
    public bool LogoutRequested { get; private set; }

    private readonly AppUser _user;
    private readonly MenuStrip _menu = new();
    private readonly StatusStrip _status = new();
    private AnalyticsForm? _analytics;

    public MainForm()
    {
        _user = Session.Current!;
        AppSettings.Load();

        Text = "Гостиничный комплекс";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        var welcome = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 16),
            Text = $"Добро пожаловать, {_user.FullName}!\nВыберите раздел в меню."
        };

        BuildMenu();
        BuildStatusBar();

        // Порядок важен: сначала центральная область, потом строка состояния и меню
        Controls.Add(welcome);
        Controls.Add(_status);
        Controls.Add(_menu);
        MainMenuStrip = _menu;

        ApplyAppearance();

        FormClosed += (_, _) =>
            AuditLogger.Log(_user.Id, LogoutRequested ? "LOGOUT" : "EXIT");
    }

    // Шрифт и цвет фона из настроек
    private void ApplyAppearance()
    {
        AppSettings.Apply(this);
        _menu.Font = Font;
        _status.Font = Font;
        BackColor = AppSettings.CurrentBackColor();
    }

    private void BuildMenu()
    {
        //  Справочники (подпункты сгруппированы) =====
        var refs = new ToolStripMenuItem("Справочники");

        AddGroup(refs, "Гостиничный комплекс",
            ("hotel", "Гостиницы"),
            ("room", "Номера"),
            ("hotel_services", "Услуги"));

        AddGroup(refs, "Клиенты",
            ("client", "Клиенты (контакты)"),
            ("natural_person", "Физические лица"),
            ("legal_entity", "Организации"),
            ("bank", "Банки"));

        AddGroup(refs, "Бронирование",
            ("reservation", "Бронирования"),
            ("reservation_room", "Номера брони"),
            ("reservation_guest", "Проживающие по брони"),
            ("contract", "Договоры"),
            ("contract_reservation", "Привязка броней к договорам"));

        AddGroup(refs, "Услуги и жалобы",
            ("service", "Услуги"),
            ("client_services", "Оказанные услуги"),
            ("complaint", "Жалобы"));

        AddGroup(refs, "Кадры",
            ("staff", "Сотрудники"),
            ("employment_record", "Трудовая книжка"),
            ("position", "Должности"),
            ("profession", "Профессии"),
            ("qualification", "Квалификации"),
            ("specialization", "Специальности"),
            ("structural_division", "Структурные подразделения"),
            ("work_place", "Места работы"));

        AddGroup(refs, "Адреса",
            ("city", "Города"),
            ("street", "Улицы"));

        if (refs.DropDownItems.Count > 0) _menu.Items.Add(refs);

        //  Документы и аналитика 
        AddTopAction("docs", "Документы", () => OpenDialog(() => new DocumentsForm(), "Документы"));
        AddTopAction("analytics", "Аналитика", OpenAnalytics);

        //  Справка 
        var help = new ToolStripMenuItem("Справка");
        AddItem(help, "help.manual", "Руководство пользователя",
            (_, _) => OpenDialog(() => new HelpForm(), "Руководство пользователя"));
        AddItem(help, "help.about", "О программе",
            (_, _) => OpenDialog(() => new AboutForm(), "О программе"));
        if (help.DropDownItems.Count > 0) _menu.Items.Add(help);

        //  Разное 
        var misc = new ToolStripMenuItem("Разное");
        AddItem(misc, "misc.settings", "Настройка", (_, _) => OpenSettings());
        AddItem(misc, "misc.password", "Сменить пароль",
            (_, _) => OpenDialog(() => new ChangePasswordForm(), "Смена пароля"));
        AddItem(misc, "misc.switch", "Сменить пользователя", (_, _) =>
        {
            LogoutRequested = true;
            Close();
        });
        if (misc.DropDownItems.Count > 0) _menu.Items.Add(misc);

        //  Выход
        var exit = new ToolStripMenuItem("Выход");
        exit.Click += (_, _) =>
        {
            var answer = MessageBox.Show("Выйти из программы?", "Выход",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer == DialogResult.Yes) Close();
        };
        _menu.Items.Add(exit);
    }

    // Группа справочников: показываются только таблицы, к которым у роли есть доступ
    private void AddGroup(ToolStripMenuItem parent, string title,
        params (string Table, string Title)[] tables)
    {
        var group = new ToolStripMenuItem(title);

        foreach (var (table, caption) in tables)
        {
            if (Permissions.TableOps(table, _user.Role).Length == 0) continue;

            var item = new ToolStripMenuItem(caption);
            string tableName = table;
            item.Click += (_, _) => OpenDialog(() => new TableForm(TableDefs.Get(tableName)), caption);
            group.DropDownItems.Add(item);
        }

        if (group.DropDownItems.Count > 0) parent.DropDownItems.Add(group);
    }

    // Единое место открытия окон: применяет шрифт и показывает понятную ошибку при сбое
    private void OpenDialog(Func<Form> create, string caption)
    {
        try
        {
            using var form = create();
            AppSettings.Apply(form);
            form.ShowDialog(this);
        }
        catch (Exception ex)
        {
            AuditLogger.Log(_user.Id, "ERROR", $"Открытие «{caption}»: {ex.Message}");
            MessageBox.Show("Не удалось открыть раздел:\n" + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenSettings()
    {
        try
        {
            using var form = new SettingsForm();
            AppSettings.Apply(form);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                ApplyAppearance();
                AuditLogger.Log(_user.Id, "SETTINGS", "Изменён внешний вид программы");
            }
        }
        catch (Exception ex)
        {
            AuditLogger.Log(_user.Id, "ERROR", $"Открытие «Настройка»: {ex.Message}");
            MessageBox.Show("Не удалось открыть настройки:\n" + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenAnalytics()
    {
        try
        {
            if (_analytics == null || _analytics.IsDisposed)
            {
                _analytics = new AnalyticsForm();
                AppSettings.Apply(_analytics);
                _analytics.Show(this);
            }
            else
            {
                if (_analytics.WindowState == FormWindowState.Minimized)
                    _analytics.WindowState = FormWindowState.Normal;
                _analytics.Activate();
            }
        }
        catch (Exception ex)
        {
            AuditLogger.Log(_user.Id, "ERROR", $"Открытие «Аналитика»: {ex.Message}");
            MessageBox.Show("Не удалось открыть раздел:\n" + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddItem(ToolStripMenuItem parent, string key, string text,
        EventHandler? onClick = null)
    {
        if (!Permissions.CanSee(key, _user.Role)) return;

        var item = new ToolStripMenuItem(text);
        item.Click += onClick ?? ((_, _) => ShowStub(text));
        parent.DropDownItems.Add(item);
    }

    private void AddTop(string key, string text)
    {
        if (!Permissions.CanSee(key, _user.Role)) return;

        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => ShowStub(text);
        _menu.Items.Add(item);
    }

    private void AddTopAction(string key, string text, Action action)
    {
        if (!Permissions.CanSee(key, _user.Role)) return;

        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => action();
        _menu.Items.Add(item);
    }

    private void BuildStatusBar()
    {
        _status.Items.Add(new ToolStripStatusLabel(
            $"Пользователь: {_user.FullName}   |   Роль: {Permissions.RoleTitle(_user.Role)}"));
    }

    private static void ShowStub(string name)
    {
        MessageBox.Show($"Раздел «{name}» будет реализован на следующем шаге.",
            "В разработке", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
