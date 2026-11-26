using System.Data;
using HotelComplexApp.Data;
using HotelComplexApp.Models;
using HotelComplexApp.Services;

namespace HotelComplexApp;

// Универсальное окно справочника: таблица, поиск, сортировка, добавление, изменение, удаление.
// Работает с любой таблицей по её описанию (TableDef); доступные кнопки зависят от роли.
public class TableForm : Form
{
    private readonly TableDef _def;
    private readonly AppUser _user;
    private readonly string _ops;                  // разрешённые операции, например "CRU"

    private readonly DataGridView _grid = new();
    private readonly TextBox _search = new();
    private readonly Label _count = new();
    private DataView? _view;

    public TableForm(TableDef def)
    {
        _def = def;
        _user = Session.Current!;
        _ops = Permissions.TableOps(def.Table, _user.Role);

        Text = $"{def.Title} — {Permissions.RoleTitle(_user.Role)}";
        Size = new Size(1150, 650);
        MinimumSize = new Size(720, 420);
        StartPosition = FormStartPosition.CenterParent;

        // Таблица
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.DataBindingComplete += (_, _) => FormatColumns();
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _ops.Contains('U')) EditRecord();
        };

        // Строка с количеством записей
        _count.Dock = DockStyle.Bottom;
        _count.Height = 28;
        _count.TextAlign = ContentAlignment.MiddleLeft;
        _count.Padding = new Padding(10, 0, 0, 0);

        // Верхняя панель: кнопки и поиск
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(8, 8, 8, 0)
        };

        if (_ops.Contains('C')) top.Controls.Add(MakeButton("Добавить", AddRecord));
        if (_ops.Contains('U')) top.Controls.Add(MakeButton("Изменить", EditRecord));
        if (_ops.Contains('D')) top.Controls.Add(MakeButton("Удалить", DeleteRecord));
        top.Controls.Add(MakeButton("Обновить", LoadData));

        top.Controls.Add(new Label
        {
            Text = "Поиск:",
            AutoSize = true,
            Margin = new Padding(24, 10, 4, 0)
        });
        _search.Width = 280;
        _search.Margin = new Padding(0, 6, 0, 0);
        _search.TextChanged += (_, _) => ApplyFilter();
        top.Controls.Add(_search);

        // Порядок важен: сначала центральная область
        Controls.Add(_grid);
        Controls.Add(_count);
        Controls.Add(top);

        Shown += (_, _) => LoadData();
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var b = new Button { Text = text, Width = 110, Height = 34, Margin = new Padding(0, 0, 8, 0) };
        b.Click += (_, _) => onClick();
        return b;
    }

    // ---------- Загрузка, формат, поиск ----------

    private void LoadData()
    {
        try
        {
            var table = TableRepository.Load(_def);
            _view = table.DefaultView;
            _grid.DataSource = _view;       // сортировка по заголовкам работает автоматически
            ApplyFilter();
        }
        catch (Exception ex)
        {
            AuditLogger.Log(_user.Id, "ERROR", $"Загрузка {_def.Table}: {ex.Message}");
            MessageBox.Show("Не удалось загрузить данные:\n" + DbErrors.Explain(ex), "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void FormatColumns()
    {
        foreach (DataGridViewColumn col in _grid.Columns)
        {
            if (col.Name.StartsWith("__"))
            {
                col.Visible = false;        // служебные столбцы (ключи)
                continue;
            }
            if (col.ValueType == typeof(DateTime)) col.DefaultCellStyle.Format = "dd.MM.yyyy";
            else if (col.ValueType == typeof(decimal)) col.DefaultCellStyle.Format = "N2";
        }

        // Скрытые служебные столбцы стоят первыми, из-за этого текущая ячейка могла остаться пустой.
        // Ставим её на первый видимый столбец первой строки.
        if (_grid.CurrentCell == null && _grid.Rows.Count > 0)
        {
            var firstVisible = _grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.Visible);
            if (firstVisible != null)
                _grid.CurrentCell = _grid.Rows[0].Cells[firstVisible.Index];
        }
    }

    private void ApplyFilter()
    {
        if (_view == null) return;

        string text = _search.Text.Trim();
        try
        {
            if (text.Length == 0)
            {
                _view.RowFilter = "";
            }
            else
            {
                string esc = text.Replace("'", "''")
                                 .Replace("[", "[[]")
                                 .Replace("%", "[%]")
                                 .Replace("*", "[*]");
                var parts = _def.Columns.Select(
                    c => $"Convert([{c.Title}], 'System.String') LIKE '%{esc}%'");
                _view.RowFilter = string.Join(" OR ", parts);
            }
        }
        catch
        {
            _view.RowFilter = "";
        }

        _count.Text = $"Записей: {_view.Count}";
    }

    // Выбранная строка: сначала текущая, если её нет - первая из выделенных
    private DataRowView? Selected()
    {
        DataGridViewRow? gridRow = _grid.CurrentRow;
        if (gridRow == null && _grid.SelectedRows.Count > 0)
            gridRow = _grid.SelectedRows[0];
        return gridRow?.DataBoundItem as DataRowView;
    }

    private object?[] KeysOf(DataRowView row)
        => _def.Pk.Select((_, i) => (object?)row[$"__pk{i}"]).ToArray();

    // ---------- Операции ----------

    private void AddRecord()
    {
        if (!_ops.Contains('C')) return;     // проверка прав перед выполнением

        try
        {
            using var form = new RecordForm(_def, null, values => TableRepository.Insert(_def, values));
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                AuditLogger.Log(_user.Id, "INSERT", _def.Table);
                LoadData();
            }
        }
        catch (Exception ex)
        {
            ShowFormError("добавления", ex);
        }
    }

    private void EditRecord()
    {
        if (!_ops.Contains('U')) return;

        var row = Selected();
        if (row == null)
        {
            MessageBox.Show("Выберите запись в таблице.", "Изменение",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var keys = KeysOf(row);
            using var form = new RecordForm(_def, row, values => TableRepository.Update(_def, keys, values));
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                AuditLogger.Log(_user.Id, "UPDATE", $"{_def.Table}: {string.Join(", ", keys)}");
                LoadData();
            }
        }
        catch (Exception ex)
        {
            ShowFormError("изменения", ex);
        }
    }

    private void DeleteRecord()
    {
        if (!_ops.Contains('D')) return;

        var row = Selected();
        if (row == null)
        {
            MessageBox.Show("Выберите запись в таблице.", "Удаление",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var answer = MessageBox.Show(
            "Удалить выбранную запись?\nДействие нельзя отменить.",
            "Подтверждение удаления",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        var keys = KeysOf(row);
        try
        {
            TableRepository.Delete(_def, keys);
            AuditLogger.Log(_user.Id, "DELETE", $"{_def.Table}: {string.Join(", ", keys)}");
            LoadData();
        }
        catch (Exception ex)
        {
            AuditLogger.Log(_user.Id, "ERROR", $"Удаление {_def.Table}: {ex.Message}");
            MessageBox.Show(DbErrors.Explain(ex), "Не удалось удалить",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Показывает настоящий текст ошибки, чтобы было понятно, что пошло не так
    private void ShowFormError(string action, Exception ex)
    {
        AuditLogger.Log(_user.Id, "ERROR", $"Окно {action} {_def.Table}: {ex.Message}");
        MessageBox.Show(
            $"Не удалось открыть окно {action}:\n{ex.GetType().Name}: {ex.Message}",
            "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}