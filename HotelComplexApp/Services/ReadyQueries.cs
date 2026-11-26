namespace HotelComplexApp.Services;

// запрос для «документы».
public record ReadyQuery(string Title, string Sql, bool UsesPeriod, string? NumberLabel, string Hint,
    string? TextLabel = null)
{
    public override string ToString() => Title;
}

public static class ReadyQueries
{
    public static readonly List<ReadyQuery> All = new()
    {
        // текст вводится вручную
        new ReadyQuery("— Свой запрос (ввод вручную) —", "", true, "Число",
            "В запросе можно использовать параметры @from, @to (даты), @n (число) и @s (текст). Разрешены только запросы SELECT.",
            "Текст"),

        new ReadyQuery("1. Фирмы, забронировавшие не менее N мест за период", """
            SELECT le.abbreviation || ' ' || le.name AS "Организация",
                   SUM(r.people_count) AS "Забронировано мест",
                   COUNT(DISTINCT r.id_reservation) AS "Бронирований"
            FROM legal_entity le
            JOIN contract c ON c.id_legalentity = le.id_legalentity
            JOIN contract_reservation cr ON cr.id_contract = c.id_contract
            JOIN reservation r ON r.id_reservation = cr.id_reservation
            WHERE r.booking_date BETWEEN @from AND @to
              AND r.status <> 'отменено'
            GROUP BY le.id_legalentity, le.abbreviation, le.name
            HAVING SUM(r.people_count) >= @n
            ORDER BY SUM(r.people_count) DESC
            """, true, "Минимум мест", "Период считается по дате бронирования. Число строк внизу - это общее число фирм."),

        new ReadyQuery("2. Постояльцы, заселявшиеся в номера с указанной вместимостью за период", """
            SELECT DISTINCT
                   np.last_name || ' ' || np.first_name || COALESCE(' ' || np.patronymic, '') AS "Постоялец",
                   h.name AS "Гостиница",
                   rm.room_number AS "Номер",
                   rm.capacity::text AS "Вместимость",
                   r.check_in_date AS "Заезд"
            FROM reservation r
            JOIN reservation_guest rg ON rg.id_reservation = r.id_reservation
            JOIN natural_person np ON np.id_naturalperson = rg.id_naturalperson
            JOIN reservation_room rr ON rr.id_reservation = r.id_reservation
            JOIN room rm ON rm.id_room = rr.id_room
            JOIN hotel h ON h.id_hotel = rm.id_hotel
            WHERE r.status IN ('заселено', 'завершено')
              AND r.check_in_date BETWEEN @from AND @to
              AND rm.capacity::text = @n::text
            ORDER BY 1
            """, true, "Вместимость номера (1-4)", "Период считается по дате заезда."),

        new ReadyQuery("3. Свободные номера сейчас: по гостиницам и вместимости", """
            SELECT h.name AS "Гостиница",
                   rm.capacity::text AS "Вместимость",
                   COUNT(*) AS "Свободно номеров"
            FROM room rm
            JOIN hotel h ON h.id_hotel = rm.id_hotel
            WHERE NOT EXISTS (
                SELECT 1
                FROM reservation_room rr
                JOIN reservation r ON r.id_reservation = rr.id_reservation
                WHERE rr.id_room = rm.id_room
                  AND r.status IN ('забронировано', 'заселено')
                  AND CURRENT_DATE >= r.check_in_date
                  AND CURRENT_DATE < r.check_out_date)
            GROUP BY h.id_hotel, h.name, rm.capacity
            ORDER BY h.name, rm.capacity
            """, false, null, "Параметры не нужны: считается на сегодняшний день."),

        new ReadyQuery("4. Занятые сейчас номера, которые освобождаются к указанному сроку", """
            SELECT h.name AS "Гостиница",
                   rm.room_number AS "Номер",
                   r.check_in_date AS "Заселён с",
                   r.check_out_date AS "Освобождается"
            FROM reservation r
            JOIN reservation_room rr ON rr.id_reservation = r.id_reservation
            JOIN room rm ON rm.id_room = rr.id_room
            JOIN hotel h ON h.id_hotel = rm.id_hotel
            WHERE r.status = 'заселено'
              AND CURRENT_DATE >= r.check_in_date
              AND r.check_out_date <= @to
            ORDER BY r.check_out_date, h.name, rm.room_number
            """, true, null, "Используется только дата «по»: номера, которые освободятся не позже неё."),

        new ReadyQuery("5. Недовольные клиенты и их жалобы за период", """
            SELECT COALESCE(np.last_name || ' ' || np.first_name, le.abbreviation || ' ' || le.name) AS "Клиент",
                   c.phone AS "Телефон",
                   cp.complaint_date AS "Дата",
                   cp.complaint_text AS "Жалоба",
                   cp.status::text AS "Статус"
            FROM complaint cp
            JOIN client c ON c.id_client = cp.id_client
            LEFT JOIN natural_person np ON np.id_client = c.id_client
            LEFT JOIN legal_entity le ON le.id_client = c.id_client
            WHERE cp.complaint_date BETWEEN @from AND @to
            ORDER BY cp.complaint_date DESC
            """, true, null, "Если пусто, добавьте жалобы в «Справочники → Услуги и жалобы → Жалобы»."),

        new ReadyQuery("6. Наиболее часто посещающие гостиницу постояльцы (топ-10)", """
            SELECT np.last_name || ' ' || np.first_name AS "Постоялец",
                   COUNT(DISTINCT rg.id_reservation) AS "Визитов"
            FROM natural_person np
            JOIN reservation_guest rg ON rg.id_naturalperson = np.id_naturalperson
            JOIN reservation r ON r.id_reservation = rg.id_reservation
            WHERE r.status IN ('заселено', 'завершено')
            GROUP BY np.id_naturalperson, np.last_name, np.first_name
            ORDER BY COUNT(DISTINCT rg.id_reservation) DESC, 1
            LIMIT 10
            """, false, null, "Параметры не нужны."),

        new ReadyQuery("7. Новые клиенты за период (первое бронирование в периоде)", """
            SELECT COALESCE(np.last_name || ' ' || np.first_name, le.abbreviation || ' ' || le.name) AS "Клиент",
                   c.phone AS "Телефон",
                   MIN(r.booking_date) AS "Первое бронирование"
            FROM client c
            JOIN reservation r ON r.id_client = c.id_client
            LEFT JOIN natural_person np ON np.id_client = c.id_client
            LEFT JOIN legal_entity le ON le.id_client = c.id_client
            GROUP BY c.id_client, c.phone, np.last_name, np.first_name, le.abbreviation, le.name
            HAVING MIN(r.booking_date) BETWEEN @from AND @to
            ORDER BY MIN(r.booking_date)
            """, true, null, "Новым считается клиент, у которого первое бронирование попало в период."),

        new ReadyQuery("8. Доля номеров, бронируемых партнёрами (по договорам), за период", """
            WITH total AS (
                SELECT COUNT(*) AS n FROM room
            ),
            partner AS (
                SELECT COUNT(DISTINCT rr.id_room) AS n
                FROM reservation_room rr
                JOIN reservation r ON r.id_reservation = rr.id_reservation
                JOIN contract_reservation cr ON cr.id_reservation = r.id_reservation
                WHERE r.status <> 'отменено'
                  AND r.check_in_date <= @to
                  AND r.check_out_date >= @from
            )
            SELECT total.n AS "Всего номеров",
                   partner.n AS "Номеров, забронированных партнёрами",
                   ROUND(100.0 * partner.n / NULLIF(total.n, 0), 1) AS "Процент, %"
            FROM total, partner
            """, true, null, "Берутся брони, пересекающиеся с периодом."),

        new ReadyQuery("9. Фирмы с договорами, действующими в период", """
            SELECT le.abbreviation || ' ' || le.name AS "Организация",
                   le.inn AS "ИНН",
                   c.contract_number AS "Договор",
                   c.start_date AS "Начало",
                   c.end_date AS "Окончание"
            FROM contract c
            JOIN legal_entity le ON le.id_legalentity = c.id_legalentity
            WHERE c.start_date <= @to AND c.end_date >= @from
            ORDER BY c.start_date
            """, true, null, "Договор попадает в список, если его срок пересекается с периодом."),

        new ReadyQuery("10. Номера сейчас: свободные, забронированные и занятые", """
            SELECT s.state AS "Состояние", COUNT(*) AS "Количество номеров"
            FROM (
                SELECT rm.id_room,
                       CASE
                           WHEN EXISTS (
                               SELECT 1 FROM reservation_room rr
                               JOIN reservation r ON r.id_reservation = rr.id_reservation
                               WHERE rr.id_room = rm.id_room AND r.status = 'заселено'
                                 AND CURRENT_DATE >= r.check_in_date AND CURRENT_DATE < r.check_out_date)
                               THEN 'Занят'
                           WHEN EXISTS (
                               SELECT 1 FROM reservation_room rr
                               JOIN reservation r ON r.id_reservation = rr.id_reservation
                               WHERE rr.id_room = rm.id_room AND r.status = 'забронировано'
                                 AND CURRENT_DATE >= r.check_in_date AND CURRENT_DATE < r.check_out_date)
                               THEN 'Забронирован'
                           ELSE 'Свободен'
                       END AS state
                FROM room rm
            ) s
            GROUP BY s.state
            ORDER BY s.state
            """, false, null, "Параметры не нужны: считается на сегодняшний день."),

        new ReadyQuery("11. Дополнительные услуги за период: количество и сумма", """
            SELECT s.name AS "Услуга",
                   COUNT(*) AS "Раз оказана",
                   SUM(s.price) AS "Сумма"
            FROM client_services cs
            JOIN service s ON s.id_service = cs.id_service
            WHERE cs.service_date BETWEEN @from AND @to
            GROUP BY s.id_service, s.name
            ORDER BY SUM(s.price) DESC
            """, true, null, "Период считается по дате оказания услуги."),

        // ===== Запросы по конкретному объекту =====

        new ReadyQuery("12. Конкретный номер: характеристики и на какое время он свободен", """
            SELECT h.name AS "Гостиница",
                   rm.room_number AS "Номер",
                   rm.floor AS "Этаж",
                   rm.capacity::text AS "Вместимость",
                   rm.price AS "Цена за сутки",
                   CASE WHEN EXISTS (
                            SELECT 1 FROM reservation_room rr
                            JOIN reservation r ON r.id_reservation = rr.id_reservation
                            WHERE rr.id_room = rm.id_room
                              AND r.status IN ('забронировано', 'заселено')
                              AND CURRENT_DATE >= r.check_in_date AND CURRENT_DATE < r.check_out_date)
                        THEN 'Сейчас занят' ELSE 'Сейчас свободен' END AS "Состояние",
                   (SELECT MIN(r.check_in_date)
                      FROM reservation_room rr
                      JOIN reservation r ON r.id_reservation = rr.id_reservation
                     WHERE rr.id_room = rm.id_room
                       AND r.status IN ('забронировано', 'заселено')
                       AND r.check_in_date >= CURRENT_DATE) AS "Ближайшая бронь с"
            FROM room rm
            JOIN hotel h ON h.id_hotel = rm.id_hotel
            WHERE rm.room_number = @n
            ORDER BY h.name
            """, false, "Номер комнаты", "Если ближайшей брони нет, поле пустое: номер свободен на неопределённый срок. Номер комнаты уникален только в пределах гостиницы."),

        new ReadyQuery("13. Конкретный номер: кем был занят в период", """
            SELECT h.name AS "Гостиница",
                   rm.room_number AS "Номер",
                   COALESCE(np.last_name || ' ' || np.first_name, le.abbreviation || ' ' || le.name) AS "Клиент",
                   r.check_in_date AS "Заезд",
                   r.check_out_date AS "Выезд",
                   r.status::text AS "Статус"
            FROM room rm
            JOIN hotel h ON h.id_hotel = rm.id_hotel
            JOIN reservation_room rr ON rr.id_room = rm.id_room
            JOIN reservation r ON r.id_reservation = rr.id_reservation
            JOIN client c ON c.id_client = r.id_client
            LEFT JOIN natural_person np ON np.id_client = c.id_client
            LEFT JOIN legal_entity le ON le.id_client = c.id_client
            WHERE rm.room_number = @n
              AND r.status <> 'отменено'
              AND r.check_in_date <= @to AND r.check_out_date >= @from
            ORDER BY r.check_in_date
            """, true, "Номер комнаты", "Показаны неотменённые брони, пересекающиеся с периодом."),

        new ReadyQuery("14. Постоялец: сколько раз жил, в каких номерах, когда и на какую сумму", """
            SELECT np.last_name || ' ' || np.first_name || COALESCE(' ' || np.patronymic, '') AS "Постоялец",
                   h.name AS "Гостиница",
                   rm.room_number AS "Номер",
                   r.check_in_date AS "Заезд",
                   r.check_out_date AS "Выезд",
                   rm.price * (r.check_out_date - r.check_in_date) AS "Счёт за проживание"
            FROM natural_person np
            JOIN reservation_guest rg ON rg.id_naturalperson = np.id_naturalperson
            JOIN reservation r ON r.id_reservation = rg.id_reservation
            JOIN reservation_room rr ON rr.id_reservation = r.id_reservation
            JOIN room rm ON rm.id_room = rr.id_room
            JOIN hotel h ON h.id_hotel = rm.id_hotel
            WHERE r.status IN ('заселено', 'завершено')
              AND np.last_name ILIKE '%' || @s || '%'
            ORDER BY np.last_name, r.check_in_date
            """, false, null, "Введите фамилию или её часть. Число визитов равно числу строк внизу.", "Фамилия"),

        new ReadyQuery("15. Номер: оказанные услуги и счёт за них за период проживания", """
            SELECT h.name AS "Гостиница",
                   rm.room_number AS "Номер",
                   COALESCE(np.last_name || ' ' || np.first_name, le.abbreviation || ' ' || le.name) AS "Клиент",
                   s.name AS "Услуга",
                   cs.service_date AS "Дата",
                   s.price AS "Стоимость",
                   SUM(s.price) OVER (PARTITION BY c.id_client) AS "Итого по клиенту"
            FROM room rm
            JOIN hotel h ON h.id_hotel = rm.id_hotel
            JOIN reservation_room rr ON rr.id_room = rm.id_room
            JOIN reservation r ON r.id_reservation = rr.id_reservation
            JOIN client c ON c.id_client = r.id_client
            LEFT JOIN natural_person np ON np.id_client = c.id_client
            LEFT JOIN legal_entity le ON le.id_client = c.id_client
            JOIN client_services cs ON cs.id_client = c.id_client
                                   AND cs.service_date BETWEEN r.check_in_date AND r.check_out_date
            JOIN service s ON s.id_service = cs.id_service
            WHERE rm.room_number = @n
              AND r.status <> 'отменено'
              AND r.check_in_date <= @to AND r.check_out_date >= @from
            ORDER BY cs.service_date
            """, true, "Номер комнаты", "Услуги берутся за даты проживания клиента в этом номере."),

        new ReadyQuery("16. Номер: жалобы за период", """
            SELECT h.name AS "Гостиница",
                   rm.room_number AS "Номер",
                   COALESCE(np.last_name || ' ' || np.first_name, le.abbreviation || ' ' || le.name) AS "Клиент",
                   cp.complaint_date AS "Дата",
                   cp.complaint_text AS "Жалоба",
                   cp.status::text AS "Статус"
            FROM complaint cp
            JOIN room rm ON rm.id_room = cp.id_room
            JOIN hotel h ON h.id_hotel = rm.id_hotel
            JOIN client c ON c.id_client = cp.id_client
            LEFT JOIN natural_person np ON np.id_client = c.id_client
            LEFT JOIN legal_entity le ON le.id_client = c.id_client
            WHERE rm.room_number = @n
              AND cp.complaint_date BETWEEN @from AND @to
            ORDER BY cp.complaint_date DESC
            """, true, "Номер комнаты", "Показаны жалобы, в которых указан этот номер."),

        new ReadyQuery("17. Организация: объём бронирования за период и предпочитаемые номера", """
            SELECT le.abbreviation || ' ' || le.name AS "Организация",
                   h.name AS "Гостиница",
                   rm.capacity::text AS "Вместимость",
                   COUNT(*) AS "Раз бронировала номер",
                   COUNT(DISTINCT r.id_reservation) AS "Бронирований",
                   SUM(r.people_count) AS "Человек (по броням)"
            FROM legal_entity le
            JOIN contract c ON c.id_legalentity = le.id_legalentity
            JOIN contract_reservation cr ON cr.id_contract = c.id_contract
            JOIN reservation r ON r.id_reservation = cr.id_reservation
            JOIN reservation_room rr ON rr.id_reservation = r.id_reservation
            JOIN room rm ON rm.id_room = rr.id_room
            JOIN hotel h ON h.id_hotel = rm.id_hotel
            WHERE le.name ILIKE '%' || @s || '%'
              AND r.status <> 'отменено'
              AND r.booking_date BETWEEN @from AND @to
            GROUP BY le.id_legalentity, le.abbreviation, le.name, h.id_hotel, h.name, rm.capacity
            ORDER BY COUNT(*) DESC
            """, true, null, "Введите название организации или его часть. Сверху самые частые варианты номеров.", "Название организации"),
    };
}
