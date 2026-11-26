namespace HotelComplexApp;

// Окно «Справка → О программе»
public class AboutForm : Form
{
    // Впишите свои данные
    private const string Author = "Заморщикова С.К.";
    private const string Group = "АВТ-413";
    private const string Version = "1.0";

    public AboutForm()
    {
        Text = "О программе";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 330);

        var title = new Label
        {
            Text = "Гостиничный комплекс",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 20)
        };

        var subtitle = new Label
        {
            Text = $"Информационная система, версия {Version}",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Location = new Point(26, 58)
        };

        var info = new Label
        {
            AutoSize = false,
            Location = new Point(26, 96),
            Size = new Size(470, 170),
            Text =
                "Курсовая работа по дисциплине «Базы данных»\n" +
                "Вариант 7: «Гостиничный комплекс»\n\n" +
                $"Разработчик: {Author}\n" +
                $"Группа: {Group}\n\n" +
                "Средства разработки: C# (.NET 9, Windows Forms), PostgreSQL,\n" +
                "драйвер Npgsql, библиотека BCrypt.Net-Next для хеширования паролей.\n\n" +
                $"© {DateTime.Now.Year}"
        };

        var ok = new Button
        {
            Text = "Закрыть",
            Width = 120,
            Height = 36,
            Location = new Point(376, 276),
            DialogResult = DialogResult.OK
        };

        AcceptButton = ok;
        CancelButton = ok;

        Controls.Add(title);
        Controls.Add(subtitle);
        Controls.Add(info);
        Controls.Add(ok);
    }
}
