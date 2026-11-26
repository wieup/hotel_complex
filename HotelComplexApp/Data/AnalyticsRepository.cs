using Npgsql;
using NpgsqlTypes;

namespace HotelComplexApp.Data;

public record ChartPoint(string Label, double Value);

public class AnalyticsData
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    // показатели
    public int Bookings { get; set; }
    public int Cancelled { get; set; }
    public int Complaints { get; set; }
    public decimal StayRevenue { get; set; }
    public decimal ServicesRevenue { get; set; }

    // Данные графиков
    public List<ChartPoint> BookingsByMonth { get; set; } = new();
    public List<ChartPoint> RevenueByMonth { get; set; } = new();
    public List<ChartPoint> OccupancyByHotel { get; set; } = new();
    public List<ChartPoint> Statuses { get; set; } = new();
    public List<ChartPoint> Kinds { get; set; } = new();
    public List<ChartPoint> ServicesRevenueList { get; set; } = new();
}

public static class AnalyticsRepository
{

    // Карточки: бронирования, отмены, выручка за проживание, выручка за услуги, жалобы
    private const string KpiSql = """
        SELECT
            (SELECT COUNT(*) FROM reservation r
              WHERE r.check_in_date BETWEEN @from AND @to) AS bookings,
            (SELECT COUNT(*) FROM reservation r
              WHERE r.check_in_date BETWEEN @from AND @to AND r.status = 'отменено') AS cancelled,
            (SELECT COALESCE(SUM(rm.price * (r.check_out_date - r.check_in_date)), 0)
               FROM reservation r
               JOIN reservation_room rr ON rr.id_reservation = r.id_reservation
               JOIN room rm ON rm.id_room = rr.id_room
              WHERE r.check_in_date BETWEEN @from AND @to AND r.status <> 'отменено') AS stay_revenue,
            (SELECT COALESCE(SUM(s.price), 0)
               FROM client_services cs
               JOIN service s ON s.id_service = cs.id_service
              WHERE cs.service_date BETWEEN @from AND @to) AS services_revenue,
            (SELECT COUNT(*) FROM complaint c
              WHERE c.complaint_date BETWEEN @from AND @to) AS complaints
        """;

    // число бронирований по месяцам заезда
    private const string BookingsByMonthSql = """
        SELECT to_char(r.check_in_date, 'YYYY-MM') AS month, COUNT(*) AS cnt
        FROM reservation r
        WHERE r.check_in_date BETWEEN @from AND @to
        GROUP BY 1
        ORDER BY 1
        """;

    //выручка за проживание по месяцам 
    private const string RevenueByMonthSql = """
        SELECT to_char(r.check_in_date, 'YYYY-MM') AS month,
               SUM(rm.price * (r.check_out_date - r.check_in_date)) AS revenue
        FROM reservation r
        JOIN reservation_room rr ON rr.id_reservation = r.id_reservation
        JOIN room rm ON rm.id_room = rr.id_room
        WHERE r.check_in_date BETWEEN @from AND @to
          AND r.status <> 'отменено'
        GROUP BY 1
        ORDER BY 1
        """;

    //загрузка номеров по гостиницам
    private const string OccupancySql = """
        SELECT h.name,
               LEAST(100.0, ROUND(
                   100.0 * SUM(CASE WHEN r.id_reservation IS NULL THEN 0
                                    ELSE GREATEST(0, LEAST(r.check_out_date, @to + 1)
                                                     - GREATEST(r.check_in_date, @from)) END)
                   / NULLIF(COUNT(DISTINCT rm.id_room) * (@to - @from + 1), 0), 1)) AS pct
        FROM hotel h
        JOIN room rm ON rm.id_hotel = h.id_hotel
        LEFT JOIN reservation_room rr ON rr.id_room = rm.id_room
        LEFT JOIN reservation r ON r.id_reservation = rr.id_reservation
             AND r.status <> 'отменено'
             AND r.check_out_date > @from
             AND r.check_in_date <= @to
        GROUP BY h.id_hotel, h.name
        ORDER BY h.name
        """;

    // бронирования по статусам
    private const string StatusesSql = """
        SELECT r.status::text, COUNT(*) AS cnt
        FROM reservation r
        WHERE r.check_in_date BETWEEN @from AND @to
        GROUP BY r.status
        ORDER BY COUNT(*) DESC
        """;

    //изические лица и организации (брони по договорам)
    private const string KindsSql = """
        SELECT CASE WHEN EXISTS (SELECT 1 FROM contract_reservation cr
                                  WHERE cr.id_reservation = r.id_reservation)
                    THEN 'Организации (по договорам)'
                    ELSE 'Физические лица' END AS kind,
               COUNT(*) AS cnt
        FROM reservation r
        WHERE r.check_in_date BETWEEN @from AND @to
          AND r.status <> 'отменено'
        GROUP BY 1
        ORDER BY 2 DESC
        """;

    // выручка по дополнительным услугам
    private const string ServicesSql = """
        SELECT s.name, SUM(s.price) AS revenue
        FROM client_services cs
        JOIN service s ON s.id_service = cs.id_service
        WHERE cs.service_date BETWEEN @from AND @to
        GROUP BY s.id_service, s.name
        ORDER BY SUM(s.price) DESC
        LIMIT 8
        """;

  
    // загрузка
    public static AnalyticsData Load(DateTime? from, DateTime? to)
    {
        using var conn = Db.Open();

        var (f, t) = ResolvePeriod(conn, from, to);
        var data = new AnalyticsData { From = f, To = t };

        using (var cmd = Command(conn, KpiSql, f, t))
        using (var r = cmd.ExecuteReader())
        {
            if (r.Read())
            {
                data.Bookings = Convert.ToInt32(r.GetValue(0));
                data.Cancelled = Convert.ToInt32(r.GetValue(1));
                data.StayRevenue = Convert.ToDecimal(r.GetValue(2));
                data.ServicesRevenue = Convert.ToDecimal(r.GetValue(3));
                data.Complaints = Convert.ToInt32(r.GetValue(4));
            }
        }

        data.BookingsByMonth = Points(conn, BookingsByMonthSql, f, t);
        data.RevenueByMonth = Points(conn, RevenueByMonthSql, f, t);
        data.OccupancyByHotel = Points(conn, OccupancySql, f, t);
        data.Statuses = Points(conn, StatusesSql, f, t);
        data.Kinds = Points(conn, KindsSql, f, t);
        data.ServicesRevenueList = Points(conn, ServicesSql, f, t);

        return data;
    }

    private static (DateTime From, DateTime To) ResolvePeriod(
        NpgsqlConnection conn, DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue)
            return (from.Value.Date, to.Value.Date);

        using var cmd = new NpgsqlCommand(
            "SELECT MIN(check_in_date), MAX(check_out_date) FROM reservation", conn);
        using var r = cmd.ExecuteReader();
        if (r.Read() && !r.IsDBNull(0) && !r.IsDBNull(1))
        {
            return (r.GetFieldValue<DateOnly>(0).ToDateTime(TimeOnly.MinValue),
                    r.GetFieldValue<DateOnly>(1).ToDateTime(TimeOnly.MinValue));
        }

        // В базе нет бронирований
        return (DateTime.Today.AddYears(-1), DateTime.Today);
    }

    private static NpgsqlCommand Command(NpgsqlConnection conn, string sql, DateTime from, DateTime to)
    {
        var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.Date)
            { Value = DateOnly.FromDateTime(from) });
        cmd.Parameters.Add(new NpgsqlParameter("to", NpgsqlDbType.Date)
            { Value = DateOnly.FromDateTime(to) });
        return cmd;
    }

    private static List<ChartPoint> Points(NpgsqlConnection conn, string sql, DateTime from, DateTime to)
    {
        var list = new List<ChartPoint>();
        using var cmd = Command(conn, sql, from, to);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            string label = r.IsDBNull(0) ? "—" : r.GetString(0);
            double value = r.IsDBNull(1) ? 0 : Convert.ToDouble(r.GetValue(1));
            list.Add(new ChartPoint(label, value));
        }
        return list;
    }
}
