using HotelComplexApp.Services;

namespace HotelComplexApp;

// Окно «Настройка»: шрифт, размер шрифта и цвет фона главного окна
public class SettingsForm : Form
{
    private readonly ComboBox _font = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _size = new();
    private readonly ComboBox _theme = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Panel _previewPanel = new();
    private readonly Label _preview = new();

    public SettingsForm()
    {
        Text = "Настройка";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(500, 360);

        // Значения из текущих настроек
        _font.Items.AddRange(AppSettings.FontFamilies);
        _font.SelectedItem = AppSettings.FontFamily;

        _size.Minimum = (decimal)AppSettings.MinFontSize;
        _size.Maximum = (decimal)AppSettings.MaxFontSize;
        _size.DecimalPlaces = 0;
        _size.Value = Math.Clamp((decimal)Math.Round(AppSettings.FontSize), _size.Minimum, _size.Maximum);

        _theme.Items.AddRange(AppSettings.Themes);
        _theme.SelectedItem = AppSettings.Theme;

        // Предпросмотр
        _preview.Dock = DockStyle.Fill;
        _preview.TextAlign = ContentAlignment.MiddleCenter;
        _preview.Text = "Пример текста\nГостиничный комплекс, номер 101\n12 345,00";
        _previewPanel.Dock = DockStyle.Fill;
        _previewPanel.BorderStyle = BorderStyle.FixedSingle;
        _previewPanel.Margin = new Padding(0, 10, 0, 0);
        _previewPanel.Controls.Add(_preview);

        _font.SelectedIndexChanged += (_, _) => UpdatePreview();
        _size.ValueChanged += (_, _) => UpdatePreview();
        _theme.SelectedIndexChanged += (_, _) => UpdatePreview();

        // Раскладка
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(14, 14, 14, 0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddRow(table, 0, "Шрифт:", _font);
        AddRow(table, 1, "Размер шрифта:", _size);
        AddRow(table, 2, "Цвет фона главного окна:", _theme);
        table.Controls.Add(_previewPanel, 0, 3);
        table.SetColumnSpan(_previewPanel, 2);

        // Кнопки
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 10, 12, 0)
        };
        var btnSave = new Button { Text = "Сохранить", Width = 120, Height = 36 };
        var btnCancel = new Button { Text = "Отмена", Width = 110, Height = 36, DialogResult = DialogResult.Cancel };
        var btnDefault = new Button { Text = "По умолчанию", Width = 130, Height = 36 };
        btnSave.Click += (_, _) => OnSave();
        btnDefault.Click += (_, _) =>
        {
            _font.SelectedItem = "Segoe UI";
            _size.Value = 9;
            _theme.SelectedItem = "Светлая";
        };
        buttons.Controls.Add(btnSave);
        buttons.Controls.Add(btnCancel);
        buttons.Controls.Add(btnDefault);

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        Controls.Add(table);
        Controls.Add(buttons);

        UpdatePreview();
    }

    private static void AddRow(TableLayoutPanel table, int row, string caption, Control input)
    {
        var label = new Label
        {
            Text = caption,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 6, 8)
        };
        input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        input.Margin = new Padding(0, 4, 0, 4);
        table.Controls.Add(label, 0, row);
        table.Controls.Add(input, 1, row);
    }

    private void UpdatePreview()
    {
        try
        {
            string family = _font.SelectedItem?.ToString() ?? AppSettings.FontFamily;
            _preview.Font = new Font(family, (float)_size.Value);
        }
        catch
        {
            // Если шрифт не удалось создать, оставляем предыдущий предпросмотр
        }

        string theme = _theme.SelectedItem?.ToString() ?? "Светлая";
        _previewPanel.BackColor = AppSettings.BackColorFor(theme);
    }

    private void OnSave()
    {
        AppSettings.FontFamily = _font.SelectedItem?.ToString() ?? "Segoe UI";
        AppSettings.FontSize = (float)_size.Value;
        AppSettings.Theme = _theme.SelectedItem?.ToString() ?? "Светлая";
        AppSettings.Save();
        DialogResult = DialogResult.OK;
    }
}
