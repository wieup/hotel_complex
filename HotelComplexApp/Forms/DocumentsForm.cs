using System.Data;
using HotelComplexApp.Data;
using HotelComplexApp.Models;
using HotelComplexApp.Services;

namespace HotelComplexApp;

// Раздел «Документы»: готовые запросы и поле для своего SQL, результат в таблице, экспорт в CSV.
// Свободный ввод SQL доступен только директорам; остальные роли выполняют готовые запросы из списка.
public class DocumentsForm : Form
{
    private readonly AppUser _user;
    private readonly bool _canEditSql;

    private readonly ComboBox _picker = new();
    private readonly Label _lblFrom = new() { Text = "Период с:", AutoSize = true };
    private readonly DateTimePicker _from = new();
    private readonly Label _lblTo = new() { Text = "по:", AutoSize = true };
    private readonly DateTimePicker _to = new();
    private readonly Label _lblNumber = new() { AutoSize = true };
    private readonly NumericUpDown _number = new();
    private readonly Label _lblText = new() { AutoSize = true };
    private readonly TextBox _text = new();
    private readonly Label _hint = new();
    private readonly TextBox _sql = new();
    private readonly DataGridView _grid = new();
    private readonly Label _status = new();
    private DataTable? _result;
    private ReadyQuery? _current;

    public DocumentsForm()
    {
        _user = Session.Current!;
        _canEditSql = _user.Role == Permissions.DirectorComplex ||
                      _user.Role == Permissions.DirectorHotel;

        Text = $"Документы — {Permissions.RoleTitle(_user.Role)}";
        Size = new Size(1300, 800);
        MinimumSize = new Size(950, 600);
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F5)
            {
                RunQuery();
                e.Handled = true;
            }
        };

        // ----- Выбор готового запроса -----
        var pickerRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        pickerRow.Controls.Add(new Label { Text = "Запрос:", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        _picker.DropDownStyle = ComboBoxStyle.DropDownList;
        _picker.Width = 820;
        _picker.DropDownWidth = 900;

        // Пункт «Свой запрос» есть только у директоров
        var items = _canEditSql ? ReadyQueries.All : ReadyQueries.All.Skip(1).ToList();
        _picker.Items.AddRange(items.ToArray());
        _picker.SelectedIndexChanged += (_, _) => OnPickerChanged();
        pickerRow.Controls.Add(_picker);

        // ----- Параметры -----
        _from.Format = DateTimePickerFormat.Short;
        _from.Width = 120;
        _from.Value = new DateTime(2020, 1, 1);
        _to.Format = DateTimePickerFormat.Short;
        _to.Width = 120;
        _to.Value = DateTime.Today.AddYears(1);
        _number.Minimum = 1;
        _number.Maximum = 100000;
        _number.Value = 1;
        _number.Width = 90;
        _text.Width = 230;

        var paramsRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        _lblFrom.Margin = new Padding(0, 8, 4, 0);
        _lblTo.Margin = new Padding(14, 8, 4, 0);
        _lblNumber.Margin = new Padding(24, 8, 4, 0);
        _lblText.Margin = new Padding(24, 8, 4, 0);
        paramsRow.Controls.AddRange(new Control[]
            { _lblFrom, _from, _lblTo, _to, _lblNumber, _number, _lblText, _text });

        // ----- Подсказка -----
        _hint.AutoSize = true;
        _hint.ForeColor = SystemColors.GrayText;
        _hint.Margin = new Padding(0, 4, 0, 4);

        // ----- Поле для SQL -----
        _sql.Multiline = true;
        _sql.ScrollBars = ScrollBars.Both;
        _sql.WordWrap = false;
        _sql.AcceptsReturn = true;
        _sql.AcceptsTab = true;
        _sql.Font = new Font("Consolas", 10);
        _sql.Dock = DockStyle.Fill;
        _sql.ReadOnly = !_canEditSql;     // не директорам текст менять нельзя

        // ----- Кнопки -----
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var btnRun = new Button { Text = "Выполнить (F5)", Width = 150, Height = 34 };
        var btnExport = new Button { Text = "Экспорт в CSV", Width = 150, Height = 34 };
        btnRun.Click += (_, _) => RunQuery();
        btnExport.Click += (_, _) => ExportCsv();
        buttons.Controls.Add(btnRun);
        buttons.Controls.Add(btnExport);

        if (_canEditSql)
        {
            var btnClear = new Button { Text = "Очистить", Width = 110, Height = 34 };
            btnClear.Click += (_, _) => _sql.Clear();
            buttons.Controls.Add(btnClear);
        }

        // ----- Таблица результата -----
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewColumn col in _grid.Columns)
            {
                if (col.ValueType == typeof(DateTime)) col.DefaultCellStyle.Format = "dd.MM.yyyy";
                else if (col.ValueType == typeof(decimal)) col.DefaultCellStyle.Format = "N2";
            }
        };

        _status.Dock = DockStyle.Fill;
        _status.AutoSize = true;
        _status.Text = _canEditSql
            ? "Выберите запрос и нажмите «Выполнить»."
            : "Выберите готовый запрос и нажмите «Выполнить». Свободный ввод SQL доступен только директорам.";

        // ----- Раскладка -----
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(10)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // выбор запроса
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // параметры
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // подсказка
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));     // поле SQL
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // кнопки
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 65));     // таблица
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // статус

        layout.Controls.Add(pickerRow, 0, 0);
        layout.Controls.Add(paramsRow, 0, 1);
        layout.Controls.Add(_hint, 0, 2);
        layout.Controls.Add(_sql, 0, 3);
        layout.Controls.Add(buttons, 0, 4);
        layout.Controls.Add(_grid, 0, 5);
        layout.Controls.Add(_status, 0, 6);
        Controls.Add(layout);

        _picker.SelectedIndex = _canEditSql ? 1 : 0;
    }

    // При выборе запроса подставляем его текст и показываем только нужные параметры
    private void OnPickerChanged()
    {
        if (_picker.SelectedItem is not ReadyQuery q) return;
        _current = q;

        bool free = q.Sql.Length == 0;
        if (!free) _sql.Text = q.Sql.ReplaceLineEndings("\r\n");

        bool showPeriod = free || q.UsesPeriod;
        _lblFrom.Visible = _from.Visible = _lblTo.Visible = _to.Visible = showPeriod;

        bool showNumber = free || q.NumberLabel != null;
        _lblNumber.Visible = _number.Visible = showNumber;
        _lblNumber.Text = (q.NumberLabel ?? "Число") + ":";

        bool showText = free || q.TextLabel != null;
        _lblText.Visible = _text.Visible = showText;
        _lblText.Text = (q.TextLabel ?? "Текст") + ":";

        _hint.Text = q.Hint;
    }

    private static string Normalize(string s) => s.ReplaceLineEndings("\n").Trim();

    private void RunQuery()
    {
        string sql = _sql.Text;

        // Проверка прав: не директорам разрешены только готовые запросы без изменений
        if (!_canEditSql &&
            (_current == null || _current.Sql.Length == 0 || Normalize(sql) != Normalize(_current.Sql)))
        {
            MessageBox.Show("Вашей роли доступны только готовые запросы из списка.",
                "Запрос не выполнен", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string? problem = SqlGuard.Validate(sql);
        if (problem != null)
        {
            MessageBox.Show(problem, "Запрос не выполнен", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_from.Visible && _from.Value.Date > _to.Value.Date)
        {
            MessageBox.Show("Дата «с» не может быть позже даты «по».", "Проверьте период",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_current?.TextLabel != null && _current.Sql.Length > 0 && _text.Text.Trim().Length == 0)
        {
            MessageBox.Show($"Введите значение: {_current.TextLabel}.", "Не хватает данных",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _text.Focus();
            return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;
            var table = DocumentRepository.Run(
                sql.Trim().TrimEnd(';'), _from.Value.Date, _to.Value.Date,
                (int)_number.Value, _text.Text.Trim());

            _result = table;
            _grid.DataSource = table;

            string text = $"Строк: {table.Rows.Count}";
            if (table.Rows.Count >= DocumentRepository.MaxRows)
                text += $" (показаны первые {DocumentRepository.MaxRows})";
            if (table.Rows.Count == 0)
                text = "Запрос выполнен, но данных по нему нет.";
            _status.Text = text;

            AuditLogger.Log(_user.Id, "QUERY", Truncate(sql, 500));
        }
        catch (Exception ex)
        {
            AuditLogger.Log(_user.Id, "ERROR", "Запрос: " + Truncate(ex.Message, 300));
            MessageBox.Show(DbErrors.Explain(ex), "Ошибка выполнения запроса",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ExportCsv()
    {
        if (_result == null || _result.Columns.Count == 0)
        {
            MessageBox.Show("Сначала выполните запрос, чтобы было что экспортировать.", "Экспорт",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Экспорт результата",
            Filter = "CSV-файл (*.csv)|*.csv",
            FileName = $"report_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            ExportService.ToCsv(_result, dialog.FileName);
            AuditLogger.Log(_user.Id, "EXPORT", dialog.FileName);
            MessageBox.Show($"Файл сохранён:\n{dialog.FileName}", "Экспорт",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AuditLogger.Log(_user.Id, "ERROR", "Экспорт: " + Truncate(ex.Message, 300));
            MessageBox.Show("Не удалось сохранить файл:\n" + ex.Message, "Ошибка экспорта",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "...";
}