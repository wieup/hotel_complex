using System.Data;
using System.Globalization;
using HotelComplexApp.Data;
using HotelComplexApp.Models;
using HotelComplexApp.Services;

namespace HotelComplexApp;

// Окно добавления / изменения записи. Поля создаются автоматически по описанию таблицы.
public class RecordForm : Form
{
    private static readonly CultureInfo Ru = new("ru-RU");

    private readonly TableDef _def;
    private readonly DataRowView? _row;                       // null = добавление
    private readonly Action<Dictionary<string, object?>> _save;
    private readonly Dictionary<string, Control> _inputs = new();

    public Dictionary<string, object?> Values { get; } = new();

    public RecordForm(TableDef def, DataRowView? row, Action<Dictionary<string, object?>> save)
    {
        _def = def;
        _row = row;
        _save = save;

        Text = (row == null ? "Добавление: " : "Изменение: ") + def.Title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(600, Math.Min(680, 110 + def.Columns.Count * 38));

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true,
            Padding = new Padding(12, 12, 12, 0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var c in def.Columns)
        {
            var label = new Label
            {
                Text = c.Title + (c.Required ? " *" : ""),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 6, 8)
            };

            var input = CreateInput(c);
            input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            input.Margin = new Padding(0, 4, 18, 4);
            _inputs[c.Name] = input;

            int r = table.RowCount;
            table.RowCount = r + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(label, 0, r);
            table.Controls.Add(input, 1, r);
        }

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 10, 12, 0)
        };
        var btnSave = new Button { Text = "Сохранить", Width = 120, Height = 36 };
        var btnCancel = new Button { Text = "Отмена", Width = 120, Height = 36, DialogResult = DialogResult.Cancel };
        btnSave.Click += (_, _) => OnSave();
        buttons.Controls.Add(btnSave);
        buttons.Controls.Add(btnCancel);

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        Controls.Add(table);
        Controls.Add(buttons);
        AppSettings.Apply(this);
    }

    // ---------- Создание полей ввода ----------

    private Control CreateInput(ColumnDef c)
    {
        object? raw = _row?[$"__c_{c.Name}"];
        bool hasRaw = raw != null && raw != DBNull.Value;

        switch (c.Type)
        {
            case ColType.Date:
            {
                var dtp = new DateTimePicker
                {
                    Format = DateTimePickerFormat.Short,
                    ShowCheckBox = !c.Required
                };
                if (hasRaw)
                {
                    dtp.Value = Convert.ToDateTime(raw);
                    dtp.Checked = true;
                }
                else if (_row != null)
                {
                    dtp.Checked = false;                      // при изменении значения не было
                }
                else if (c.Required || c.Default != null)
                {
                    dtp.Value = DefaultDate(c.Default);       // значение по умолчанию
                    dtp.Checked = true;
                }
                else
                {
                    dtp.Checked = false;
                }
                return dtp;
            }

            case ColType.Enum:
            {
                var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                cb.Items.AddRange(c.EnumValues);
                string? selected = hasRaw ? raw!.ToString() : c.Default;
                int idx = selected != null ? Array.IndexOf(c.EnumValues, selected) : -1;
                if (idx < 0 && c.Required && c.EnumValues.Length > 0) idx = 0;
                cb.SelectedIndex = idx;
                return cb;
            }

            case ColType.Lookup:
            {
                var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 450 };
                if (!c.Required) cb.Items.Add(new LookupItem(-1, "— не выбрано —"));
                foreach (var item in TableRepository.LoadLookup(c)) cb.Items.Add(item);

                int selectIndex = cb.Items.Count > 0 ? 0 : -1;
                if (hasRaw)
                {
                    int id = Convert.ToInt32(raw);
                    for (int i = 0; i < cb.Items.Count; i++)
                        if (cb.Items[i] is LookupItem li && li.Id == id) { selectIndex = i; break; }
                }
                cb.SelectedIndex = selectIndex;
                return cb;
            }

            default:   // Text, Int, Decimal
            {
                var tb = new TextBox();
                if (c.MaxLength > 0) tb.MaxLength = c.MaxLength;

                if (hasRaw)
                {
                    tb.Text = c.Type == ColType.Decimal
                        ? Convert.ToDecimal(raw, CultureInfo.InvariantCulture).ToString("0.00", Ru)
                        : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
                }
                else if (_row == null)
                {
                    tb.Text = c.Default ?? "";
                }
                return tb;
            }
        }
    }

    // Значения по умолчанию для дат: "today", "today+7", "today-5" или "2000-01-01"
    private static DateTime DefaultDate(string? d)
    {
        if (string.IsNullOrEmpty(d) || d == "today") return DateTime.Today;
        if (d.StartsWith("today+") && int.TryParse(d[6..], out var plus)) return DateTime.Today.AddDays(plus);
        if (d.StartsWith("today-") && int.TryParse(d[6..], out var minus)) return DateTime.Today.AddDays(-minus);
        return DateTime.TryParse(d, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? dt
            : DateTime.Today;
    }

    // ---------- Проверка введённых данных ----------

    private bool Collect()
    {
        Values.Clear();

        foreach (var c in _def.Columns)
        {
            var ctl = _inputs[c.Name];
            object? v = null;

            switch (c.Type)
            {
                case ColType.Text:
                {
                    string s = ((TextBox)ctl).Text.Trim();
                    if (s.Length == 0)
                    {
                        if (c.Required) return Fail(c, "обязательное поле", ctl);
                    }
                    else
                    {
                        if (c.MaxLength > 0 && s.Length > c.MaxLength)
                            return Fail(c, $"не более {c.MaxLength} символов", ctl);
                        v = s;
                    }
                    break;
                }

                case ColType.Int:
                {
                    string s = ((TextBox)ctl).Text.Trim();
                    if (s.Length == 0)
                    {
                        if (c.Required) return Fail(c, "обязательное поле", ctl);
                    }
                    else if (!int.TryParse(s, NumberStyles.Integer, Ru, out int n))
                    {
                        return Fail(c, "введите целое число", ctl);
                    }
                    else
                    {
                        v = n;
                    }
                    break;
                }

                case ColType.Decimal:
                {
                    string s = ((TextBox)ctl).Text.Trim().Replace('.', ',');
                    if (s.Length == 0)
                    {
                        if (c.Required) return Fail(c, "обязательное поле", ctl);
                    }
                    else if (!decimal.TryParse(s, NumberStyles.Number, Ru, out decimal d))
                    {
                        return Fail(c, "введите число, например 3500,50", ctl);
                    }
                    else
                    {
                        v = d;
                    }
                    break;
                }

                case ColType.Date:
                {
                    var dtp = (DateTimePicker)ctl;
                    if (!(dtp.ShowCheckBox && !dtp.Checked))
                        v = DateOnly.FromDateTime(dtp.Value);
                    break;
                }

                case ColType.Enum:
                {
                    var cb = (ComboBox)ctl;
                    if (cb.SelectedItem == null)
                    {
                        if (c.Required) return Fail(c, "выберите значение", ctl);
                    }
                    else
                    {
                        v = cb.SelectedItem.ToString();
                    }
                    break;
                }

                case ColType.Lookup:
                {
                    var cb = (ComboBox)ctl;
                    var item = cb.SelectedItem as LookupItem;
                    if (item == null || item.Id < 0)
                    {
                        if (c.Required) return Fail(c, "выберите значение из списка", ctl);
                    }
                    else
                    {
                        v = item.Id;
                    }
                    break;
                }
            }

            Values[c.Name] = v;
        }

        return true;
    }

    private bool Fail(ColumnDef c, string message, Control ctl)
    {
        MessageBox.Show($"Поле «{c.Title}»: {message}.", "Проверьте данные",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        ctl.Focus();
        return false;
    }

    private void OnSave()
    {
        if (!Collect()) return;

        try
        {
            _save(Values);
            DialogResult = DialogResult.OK;   // успех - закрываем окно
        }
        catch (Exception ex)
        {
            // При ошибке окно остаётся открытым, введённые данные не теряются
            AuditLogger.Log(Session.Current?.Id, "ERROR", $"{_def.Table}: {ex.Message}");
            MessageBox.Show(DbErrors.Explain(ex), "Ошибка сохранения",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
