using HotelComplexApp.Data;
using HotelComplexApp.Models;

namespace HotelComplexApp;

// Окно смены пароля: старый пароль, новый пароль, повтор нового
public class ChangePasswordForm : Form
{
    private const int MinLength = 8;

    private readonly AppUser _user;
    private readonly TextBox _old = new() { UseSystemPasswordChar = true };
    private readonly TextBox _new = new() { UseSystemPasswordChar = true };
    private readonly TextBox _repeat = new() { UseSystemPasswordChar = true };
    private readonly CheckBox _show = new() { Text = "Показать пароли", AutoSize = true };

    public ChangePasswordForm()
    {
        _user = Session.Current!;

        Text = "Смена пароля";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(440, 290);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(14, 14, 14, 0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(table, "Текущий пароль:", _old);
        AddRow(table, "Новый пароль:", _new);
        AddRow(table, "Повтор нового:", _repeat);

        _show.Margin = new Padding(0, 4, 0, 4);
        _show.CheckedChanged += (_, _) =>
        {
            bool hide = !_show.Checked;
            _old.UseSystemPasswordChar = hide;
            _new.UseSystemPasswordChar = hide;
            _repeat.UseSystemPasswordChar = hide;
        };
        int r = table.RowCount;
        table.RowCount = r + 1;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(_show, 1, r);

        var rules = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 48,
            Padding = new Padding(14, 8, 14, 0),
            ForeColor = SystemColors.GrayText,
            Text = $"Требования к новому паролю: не меньше {MinLength} символов, " +
                   "хотя бы одна буква и одна цифра, не совпадает со старым."
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 10, 12, 0)
        };
        var btnSave = new Button { Text = "Сменить", Width = 120, Height = 36 };
        var btnCancel = new Button { Text = "Отмена", Width = 120, Height = 36, DialogResult = DialogResult.Cancel };
        btnSave.Click += (_, _) => OnSave();
        buttons.Controls.Add(btnSave);
        buttons.Controls.Add(btnCancel);

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        Controls.Add(rules);
        Controls.Add(table);
        Controls.Add(buttons);
    }

    private static void AddRow(TableLayoutPanel table, string caption, Control input)
    {
        var label = new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 6, 8) };
        input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        input.Margin = new Padding(0, 4, 0, 4);

        int r = table.RowCount;
        table.RowCount = r + 1;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(label, 0, r);
        table.Controls.Add(input, 1, r);
    }

    private void OnSave()
    {
        string oldPassword = _old.Text;
        string newPassword = _new.Text;

        if (oldPassword.Length == 0)
        {
            Warn("Введите текущий пароль.", _old);
            return;
        }
        if (newPassword.Length < MinLength)
        {
            Warn($"Новый пароль должен содержать не меньше {MinLength} символов.", _new);
            return;
        }
        if (!newPassword.Any(char.IsLetter) || !newPassword.Any(char.IsDigit))
        {
            Warn("Новый пароль должен содержать хотя бы одну букву и одну цифру.", _new);
            return;
        }
        if (newPassword == oldPassword)
        {
            Warn("Новый пароль должен отличаться от текущего.", _new);
            return;
        }
        if (newPassword != _repeat.Text)
        {
            Warn("Новый пароль и его повтор не совпадают.", _repeat);
            return;
        }

        try
        {
            var result = AuthRepository.ChangePassword(_user.Id, oldPassword, newPassword);

            switch (result)
            {
                case ChangePasswordResult.Ok:
                    AuditLogger.Log(_user.Id, "PASSWORD_CHANGE");
                    MessageBox.Show("Пароль успешно изменён. Используйте его при следующем входе.",
                        "Смена пароля", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    break;

                case ChangePasswordResult.WrongOldPassword:
                    AuditLogger.Log(_user.Id, "PASSWORD_CHANGE_FAIL", "Неверный текущий пароль");
                    Warn("Текущий пароль указан неверно.", _old);
                    break;

                default:
                    Warn("Пользователь не найден или отключён. Обратитесь к администратору.", null);
                    break;
            }
        }
        catch (Exception ex)
        {
            AuditLogger.Log(_user.Id, "ERROR", "Смена пароля: " + ex.Message);
            MessageBox.Show("Не удалось сменить пароль:\n" + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Warn(string message, Control? focus)
    {
        MessageBox.Show(message, "Смена пароля", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        focus?.Focus();
    }
}
