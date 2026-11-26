using System.Drawing.Imaging;
using System.Globalization;
using System.Windows.Forms.DataVisualization.Charting;
using HotelComplexApp.Data;
using HotelComplexApp.Models;

namespace HotelComplexApp;

// Раздел «Аналитика»: дашборд с показателями и графиками по данным БД.
// Есть фильтр по периоду, автообновление данных и экспорт графиков в PNG.
public class AnalyticsForm : Form
{
    private static readonly CultureInfo Ru = new("ru-RU");

    private readonly AppUser _user;

    // Фильтры и управление
    private readonly ComboBox _preset = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    private readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    private readonly CheckBox _auto = new() { Text = "Автообновление", AutoSize = true, Checked = true };
    private readonly ComboBox _interval = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
    private readonly Label _period = new() { AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
    private readonly Label _updated = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly System.Windows.Forms.Timer _timer = new();

    // Карточки показателей
    private Label _kBookings = null!, _kCancelled = null!, _kStay = null!, _kServices = null!, _kComplaints = null!;

    // Графики
    private readonly Chart _cMonth;
    private readonly Chart _cRevenue;
    private readonly Chart _cOccupancy;
    private readonly Chart _cStatus;
    private readonly Chart _cKind;
    private readonly Chart _cServices;
    private readonly (Chart Chart, string Name)[] _charts;

    private bool _ready;
    private bool _loading;

    public AnalyticsForm()
    {
        _user = Session.Current!;

        Text = "Аналитика — гостиничный комплекс";
        Size = new Size(1400, 860);
        MinimumSize = new Size(1000, 650);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(244, 246, 249);

        // ----- Графики -----
        _cMonth = CreateChart("Бронирования по месяцам", SeriesChartType.Column,
            "#VALX: #VALY{N0} бронирований", Color.SteelBlue);
        _cRevenue = CreateChart("Выручка за проживание по месяцам, ₽", SeriesChartType.Line,
            "#VALX: #VALY{N0} ₽", Color.SeaGreen);
        _cOccupancy = CreateChart("Загрузка номеров по гостиницам, %", SeriesChartType.Bar,
            "#VALX: #VALY{0.0} %", Color.DarkOrange);
        _cStatus = CreateChart("Бронирования по статусам", SeriesChartType.Pie,
            "#VALX: #VALY{N0}", Color.Empty);
        _cKind = CreateChart("Клиенты: физические лица и организации", SeriesChartType.Doughnut,
            "#VALX: #VALY{N0}", Color.Empty);
        _cServices = CreateChart("Выручка по дополнительным услугам, ₽", SeriesChartType.Bar,
            "#VALX: #VALY{N0} ₽", Color.MediumPurple);

        _cOccupancy.ChartAreas[0].AxisY.Maximum = 100;
        _cOccupancy.ChartAreas[0].AxisY.Minimum = 0;

        _charts = new[]
        {
            (_cMonth, "01_bookings_by_month"),
            (_cRevenue, "02_revenue_by_month"),
            (_cOccupancy, "03_occupancy_by_hotel"),
            (_cStatus, "04_bookings_by_status"),
            (_cKind, "05_client_kinds"),
            (_cServices, "06_services_revenue"),
        };

        // Правая кнопка мыши на графике: сохранить его
        foreach (var (chart, name) in _charts)
        {
            var menu = new ContextMenuStrip();
            var chartRef = chart;
            var nameRef = name;
            menu.Items.Add("Сохранить график как PNG…", null, (_, _) => SaveChartDialog(chartRef, nameRef));
            chart.ContextMenuStrip = menu;
        }

        // ----- Панель фильтров -----
        _preset.Items.AddRange(new object[]
        {
            "Всё время", "Последние 30 дней", "Последние 90 дней",
            "Текущий год", "Прошлый год", "Свой период"
        });
        _from.Value = DateTime.Today.AddMonths(-3);
        _to.Value = DateTime.Today;
        _from.Enabled = _to.Enabled = false;

        _interval.Items.AddRange(new object[] { "5 сек", "10 сек", "30 сек", "60 сек" });
        _interval.SelectedIndex = 1;
        _timer.Interval = 10_000;

        var btnRefresh = new Button { Text = "Обновить", Width = 100, Height = 30 };
        var btnDashboard = new Button { Text = "Сохранить дашборд (PNG)", Width = 190, Height = 30 };
        var btnAll = new Button { Text = "Экспорт всех графиков…", Width = 190, Height = 30 };
        btnRefresh.Click += async (_, _) => await RefreshAsync(manual: true);
        btnDashboard.Click += (_, _) => SaveDashboardDialog();
        btnAll.Click += (_, _) => ExportAllCharts();

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(10, 8, 10, 4)
        };
        filters.Controls.Add(Caption("Период:", 0));
        filters.Controls.Add(_preset);
        filters.Controls.Add(Caption("с", 8));
        filters.Controls.Add(_from);
        filters.Controls.Add(Caption("по", 8));
        filters.Controls.Add(_to);
        filters.Controls.Add(_auto);
        filters.Controls.Add(_interval);
        filters.Controls.Add(btnRefresh);
        filters.Controls.Add(btnDashboard);
        filters.Controls.Add(btnAll);

        var info = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(10, 0, 10, 6) };
        info.Controls.Add(_period);
        _updated.Margin = new Padding(24, 0, 0, 0);
        info.Controls.Add(_updated);

        // ----- Карточки показателей -----
        var kpi = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 5,
            RowCount = 1,
            Padding = new Padding(6, 0, 6, 0)
        };
        for (int i = 0; i < 5; i++) kpi.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        kpi.Controls.Add(MakeKpi("Бронирований за период", out _kBookings), 0, 0);
        kpi.Controls.Add(MakeKpi("Отменено", out _kCancelled), 1, 0);
        kpi.Controls.Add(MakeKpi("Выручка за проживание", out _kStay), 2, 0);
        kpi.Controls.Add(MakeKpi("Выручка от доп. услуг", out _kServices), 3, 0);
        kpi.Controls.Add(MakeKpi("Жалоб за период", out _kComplaints), 4, 0);

        // ----- Сетка графиков 3 x 2 -----
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(6)
        };
        for (int i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.Controls.Add(_cMonth, 0, 0);
        grid.Controls.Add(_cRevenue, 1, 0);
        grid.Controls.Add(_cOccupancy, 2, 0);
        grid.Controls.Add(_cStatus, 0, 1);
        grid.Controls.Add(_cKind, 1, 1);
        grid.Controls.Add(_cServices, 2, 1);

        // ----- Общая раскладка -----
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // фильтры
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // период и время обновления
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // карточки     
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));      // графики
        root.Controls.Add(filters, 0, 0);
        root.Controls.Add(info, 0, 1);
        root.Controls.Add(kpi, 0, 2);
        root.Controls.Add(grid, 0, 3);
        Controls.Add(root);

        // ----- События -----
        _preset.SelectedIndexChanged += async (_, _) =>
        {
            bool custom = _preset.SelectedItem?.ToString() == "Свой период";
            _from.Enabled = _to.Enabled = custom;
            if (_ready) await RefreshAsync(manual: false);
        };
        _from.ValueChanged += async (_, _) =>
        {
            if (_ready && IsCustomPeriod()) await RefreshAsync(manual: false);
        };
        _to.ValueChanged += async (_, _) =>
        {
            if (_ready && IsCustomPeriod()) await RefreshAsync(manual: false);
        };

        _auto.CheckedChanged += (_, _) => _timer.Enabled = _auto.Checked;
        _interval.SelectedIndexChanged += (_, _) =>
        {
            string text = _interval.SelectedItem?.ToString() ?? "10 сек";
            if (int.TryParse(text.Split(' ')[0], out int seconds))
                _timer.Interval = seconds * 1000;
        };
        _timer.Tick += async (_, _) => await RefreshAsync(manual: false);

        Shown += async (_, _) =>
        {
            _ready = true;
            _timer.Enabled = _auto.Checked;
            await RefreshAsync(manual: true);
        };
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _timer.Dispose();
        };

        _preset.SelectedIndex = 0;   // «Всё время»
    }

    //  Создание элементов 

    private static Label Caption(string text, int leftMargin) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(leftMargin, 8, 4, 0)
    };

    private static Control MakeKpi(string caption, out Label valueLabel)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(4),
            Padding = new Padding(8, 6, 8, 6)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var cap = new Label
        {
            Text = caption,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Font = new Font("Segoe UI", 9)
        };
        valueLabel = new Label
        {
            Text = "—",
            AutoSize = true,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 40, 40),
            Margin = new Padding(3, 4, 3, 3)
        };

        panel.Controls.Add(cap, 0, 0);
        panel.Controls.Add(valueLabel, 0, 1);
        return panel;
    }

    private static Chart CreateChart(string title, SeriesChartType type, string tooltip, Color color)
    {
        bool circular = type == SeriesChartType.Pie || type == SeriesChartType.Doughnut;
        bool horizontal = type == SeriesChartType.Bar;

        var chart = new Chart
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(4)
        };

        var area = new ChartArea("main") { BackColor = Color.White };
        if (!circular)
        {
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisY.MajorGrid.LineColor = Color.Gainsboro;
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 8);
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 8);
            if (horizontal) area.AxisX.IsReversed = true;   // первая строка сверху
        }
        chart.ChartAreas.Add(area);

        chart.Titles.Add(new Title(title, Docking.Top, new Font("Segoe UI", 10, FontStyle.Bold),
            Color.FromArgb(40, 40, 40)) { Name = "title" });
        chart.Titles.Add(new Title("Нет данных за выбранный период", Docking.Bottom,
            new Font("Segoe UI", 9, FontStyle.Italic), Color.Gray) { Name = "empty", Visible = false });

        var series = new Series("data")
        {
            ChartType = type,
            IsValueShownAsLabel = true,
            ToolTip = tooltip
        };

        if (circular)
        {
            series.Palette = ChartColorPalette.Pastel;
            series.Label = "#PERCENT{P0}";
            series.LegendText = "#VALX";
            chart.Legends.Add(new Legend("legend") { Docking = Docking.Bottom, Font = new Font("Segoe UI", 8) });
            series.Legend = "legend";
        }
        else
        {
            series.Color = color;
            series.LabelFormat = "N0";
            if (type == SeriesChartType.Line)
            {
                series.BorderWidth = 3;
                series.MarkerStyle = MarkerStyle.Circle;
                series.MarkerSize = 8;
            }
        }

        chart.Series.Add(series);
        return chart;
    }

    private static void Fill(Chart chart, List<ChartPoint> points)
    {
        var series = chart.Series[0];
        series.Points.Clear();
        foreach (var p in points) series.Points.AddXY(p.Label, p.Value);
        chart.Titles["empty"].Visible = points.Count == 0;
    }

    // ---------- Период и обновление ----------

    private bool IsCustomPeriod() => _preset.SelectedItem?.ToString() == "Свой период";

    // Возвращает false, если период указан неверно. null в from и to означает «всё время».
    private bool TryReadPeriod(out DateTime? from, out DateTime? to)
    {
        var today = DateTime.Today;
        from = null;
        to = null;

        switch (_preset.SelectedItem?.ToString())
        {
            case "Последние 30 дней":
                from = today.AddDays(-29);
                to = today;
                break;
            case "Последние 90 дней":
                from = today.AddDays(-89);
                to = today;
                break;
            case "Текущий год":
                from = new DateTime(today.Year, 1, 1);
                to = new DateTime(today.Year, 12, 31);
                break;
            case "Прошлый год":
                from = new DateTime(today.Year - 1, 1, 1);
                to = new DateTime(today.Year - 1, 12, 31);
                break;
            case "Свой период":
                from = _from.Value.Date;
                to = _to.Value.Date;
                if (from > to) return false;
                break;
        }
        return true;
    }

    private async Task RefreshAsync(bool manual)
    {
        if (_loading || IsDisposed) return;

        if (!TryReadPeriod(out var from, out var to))
        {
            if (manual)
                MessageBox.Show("Дата «с» не может быть позже даты «по».", "Проверьте период",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _loading = true;
        try
        {
            // Запросы выполняются в фоне, чтобы окно не зависало
            var data = await Task.Run(() => AnalyticsRepository.Load(from, to));
            if (IsDisposed) return;
            ApplyData(data);
        }
        catch (Exception ex)
        {
            AuditLogger.Log(_user.Id, "ERROR", "Аналитика: " + Truncate(ex.Message, 300));
            if (manual)
                MessageBox.Show("Не удалось загрузить данные для аналитики:\n" + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            else if (!IsDisposed)
                _updated.Text = "Ошибка обновления данных";
        }
        finally
        {
            _loading = false;
        }
    }

    private void ApplyData(AnalyticsData d)
    {
        _period.Text = $"Период: {d.From:dd.MM.yyyy} – {d.To:dd.MM.yyyy}";

        _kBookings.Text = d.Bookings.ToString("N0", Ru);
        _kCancelled.Text = d.Bookings == 0
            ? "0"
            : $"{d.Cancelled} ({100.0 * d.Cancelled / d.Bookings:0.#}%)";
        _kStay.Text = Money(d.StayRevenue);
        _kServices.Text = Money(d.ServicesRevenue);
        _kComplaints.Text = d.Complaints.ToString("N0", Ru);

        Fill(_cMonth, d.BookingsByMonth);
        Fill(_cRevenue, d.RevenueByMonth);
        Fill(_cOccupancy, d.OccupancyByHotel);
        Fill(_cStatus, d.Statuses);
        Fill(_cKind, d.Kinds);
        Fill(_cServices, d.ServicesRevenueList);

        _updated.Text = $"Обновлено: {DateTime.Now:HH:mm:ss}";
    }

    private static string Money(decimal value) => value.ToString("N0", Ru) + " ₽";

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";

    // ---------- Экспорт графиков ----------

    private void SaveChartDialog(Chart chart, string defaultName)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Сохранить график",
            Filter = "Изображение PNG (*.png)|*.png",
            FileName = defaultName + ".png"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            chart.SaveImage(dialog.FileName, ChartImageFormat.Png);
            AuditLogger.Log(_user.Id, "EXPORT", "График: " + dialog.FileName);
            MessageBox.Show($"График сохранён:\n{dialog.FileName}", "Экспорт",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowExportError(ex);
        }
    }

    private void ExportAllCharts()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Выберите папку для сохранения графиков",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            foreach (var (chart, name) in _charts)
                chart.SaveImage(Path.Combine(dialog.SelectedPath, name + ".png"), ChartImageFormat.Png);

            string dashboardPath = Path.Combine(dialog.SelectedPath, "dashboard.png");
            RenderDashboard(dashboardPath);

            AuditLogger.Log(_user.Id, "EXPORT", "Все графики: " + dialog.SelectedPath);
            MessageBox.Show($"Сохранено {_charts.Length} графиков и общий дашборд в папку:\n{dialog.SelectedPath}",
                "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowExportError(ex);
        }
    }

    private void SaveDashboardDialog()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Сохранить дашборд",
            Filter = "Изображение PNG (*.png)|*.png",
            FileName = $"dashboard_{DateTime.Now:yyyyMMdd_HHmm}.png"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            RenderDashboard(dialog.FileName);
            AuditLogger.Log(_user.Id, "EXPORT", "Дашборд: " + dialog.FileName);
            MessageBox.Show($"Дашборд сохранён:\n{dialog.FileName}", "Экспорт",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowExportError(ex);
        }
    }

    // Собирает один рисунок: заголовок, период, показатели и все шесть графиков
    private void RenderDashboard(string path)
    {
        const int cellW = 640, cellH = 400, pad = 12, header = 150;
        const int cols = 3, rows = 2;

        using var bitmap = new Bitmap(cols * cellW + (cols + 1) * pad, header + rows * cellH + (rows + 1) * pad);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.White);

        using var titleFont = new Font("Segoe UI", 20, FontStyle.Bold);
        using var textFont = new Font("Segoe UI", 12);
        using var kpiFont = new Font("Segoe UI", 13, FontStyle.Bold);

        g.DrawString("Аналитика: гостиничный комплекс", titleFont, Brushes.Black, pad, 12);
        g.DrawString($"{_period.Text}     Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}",
            textFont, Brushes.DimGray, pad, 58);
        g.DrawString(
            $"Бронирований: {_kBookings.Text}     Отменено: {_kCancelled.Text}     " +
            $"Выручка за проживание: {_kStay.Text}     Выручка от услуг: {_kServices.Text}     " +
            $"Жалоб: {_kComplaints.Text}",
            kpiFont, Brushes.Black, pad, 98);

        for (int i = 0; i < _charts.Length; i++)
        {
            int col = i % cols;
            int row = i / cols;
            int x = pad + col * (cellW + pad);
            int y = header + pad + row * (cellH + pad);

            using var ms = new MemoryStream();
            _charts[i].Chart.SaveImage(ms, ChartImageFormat.Png);
            ms.Position = 0;
            using var img = Image.FromStream(ms);

            // Вписываем график в ячейку, сохраняя пропорции
            float scale = Math.Min((float)cellW / img.Width, (float)cellH / img.Height);
            int w = (int)(img.Width * scale);
            int h = (int)(img.Height * scale);
            g.DrawImage(img, x + (cellW - w) / 2, y + (cellH - h) / 2, w, h);
            g.DrawRectangle(Pens.Gainsboro, x, y, cellW, cellH);
        }

        bitmap.Save(path, ImageFormat.Png);
    }

    private void ShowExportError(Exception ex)
    {
        AuditLogger.Log(_user.Id, "ERROR", "Экспорт графиков: " + Truncate(ex.Message, 300));
        MessageBox.Show("Не удалось сохранить файл:\n" + ex.Message, "Ошибка экспорта",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
