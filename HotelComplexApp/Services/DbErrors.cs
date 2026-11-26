using Npgsql;

namespace HotelComplexApp.Services;

public static class DbErrors
{
    private static readonly Dictionary<string, string> ConstraintMessages = new()
    {
        ["ck_client_bank_pair"] = "Банковский счёт и банк нужно указывать вместе: либо оба поля, либо ни одного.",
        ["ck_person_document_complete"] = "Паспортные данные нужно заполнить полностью (серия, номер, дата выдачи, кем выдан) или не заполнять совсем.",
        ["ck_document_date"] = "Дата выдачи документа не может быть раньше даты рождения.",
        ["ck_contract_period"] = "Дата окончания договора не может быть раньше даты начала.",
        ["ck_booking_dates"] = "Дата бронирования не может быть позже даты заезда.",
        ["ck_stay_dates"] = "Дата выезда должна быть позже даты заезда.",
        ["ck_cancellation_status"] = "Дата отмены указывается только для брони со статусом «отменено», и наоборот.",
        ["ck_cancellation_deadline"] = "Бронь можно отменить не позднее чем за 7 дней до заезда.",
        ["ck_dismissal_reason"] = "При увольнении нужно указать причину прекращения трудового договора.",
        ["client_phone_check"] = "Телефон: от 7 до 14 цифр, в начале может быть «+».",
        ["client_bank_account_check"] = "Банковский счёт должен состоять ровно из 20 цифр.",
        ["natural_person_series_check"] = "Серия паспорта: 4 цифры.",
        ["natural_person_number_check"] = "Номер паспорта: 6 цифр.",
        ["hotel_floor_count_check"] = "Количество этажей должно быть больше нуля.",
        ["hotel_total_rooms_check"] = "Количество номеров должно быть больше нуля.",
        ["room_price_check"] = "Цена номера должна быть больше нуля.",
        ["room_floor_check"] = "Этаж должен быть больше нуля.",
        ["room_room_number_check"] = "Номер комнаты должен быть больше нуля.",
        ["reservation_people_count_check"] = "Количество людей должно быть больше нуля.",
        ["service_price_check"] = "Цена услуги не может быть отрицательной.",
        ["bank_bik_check"] = "БИК: ровно 9 цифр.",
        ["bank_correspondent_account_check"] = "Корреспондентский счёт: ровно 20 цифр.",
        ["legal_entity_inn_check"] = "ИНН: 10 или 12 цифр.",
    };

    public static string Explain(Exception ex)
    {
        if (ex is PostgresException pg)
        {
            switch (pg.SqlState)
            {
                case "23505":
                    return "Такая запись уже существует (значение должно быть уникальным).";
                case "23503":
                    return "Нарушена связь между таблицами: запись используется в других таблицах " +
                           "или ссылается на несуществующую запись.";
                case "23514":
                    return ConstraintMessages.TryGetValue(pg.ConstraintName ?? "", out var msg)
                        ? msg
                        : $"Данные не проходят проверку (ограничение «{pg.ConstraintName}»).";
                case "23502":
                    return $"Не заполнено обязательное поле «{pg.ColumnName}».";
                case "22001":
                    return "Значение слишком длинное для этого поля.";
                case "22P02":
                case "22007":
                case "22008":
                    return "Значение имеет неверный формат.";
                case "P0001":
                    return pg.MessageText;
                case "42601":
                    return "Синтаксическая ошибка в запросе: " + pg.MessageText;
                case "42703":
                    return "В запросе указан несуществующий столбец: " + pg.MessageText;
                case "42P01":
                    return "В запросе указана несуществующая таблица: " + pg.MessageText;
                case "57014":
                    return "Запрос выполнялся слишком долго и был остановлен.";
                case "25006":
                    return "В этом окне разрешено только чтение данных.";
                default:
                    return $"Ошибка базы данных ({pg.SqlState}): {pg.MessageText}";
            }
        }

        return ex.Message;
    }
}
