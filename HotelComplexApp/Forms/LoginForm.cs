using HotelComplexApp.Data;
using HotelComplexApp.Models;

namespace HotelComplexApp;

public partial class LoginForm : Form
{
    public LoginForm()
    {
        InitializeComponent();
    }

    private void btnLogin_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtLogin.Text) || string.IsNullOrEmpty(txtPassword.Text))
        {
            MessageBox.Show("Введите логин и пароль.", "Вход",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var login = txtLogin.Text.Trim();

        try
        {
            var user = AuthRepository.Login(login, txtPassword.Text);
            if (user == null)
            {
                AuditLogger.Log(null, "LOGIN_FAIL", $"Логин: {login}");
                MessageBox.Show("Неверный логин или пароль.", "Вход",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Session.Current = user;
            AuditLogger.Log(user.Id, "LOGIN");
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка подключения к базе данных:\n" + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnExit_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
    }

    private void LoginForm_Load(object sender, EventArgs e)
    {

    }
}