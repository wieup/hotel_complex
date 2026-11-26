using HotelComplexApp.Models;

namespace HotelComplexApp.Services;

public static class TableDefs
{
    private const string Name = "l.name";
    private const string RoomDisp =
        "(SELECT h.name FROM hotel h WHERE h.id_hotel = l.id_hotel) || ', №' || l.room_number";
    private const string ClientDisp =
        "COALESCE((SELECT np.last_name || ' ' || np.first_name FROM natural_person np WHERE np.id_client = l.id_client), " +
        "(SELECT le.abbreviation || ' ' || le.name FROM legal_entity le WHERE le.id_client = l.id_client), 'Клиент') " +
        "|| ' (' || l.phone || ')'";
    private const string PersonDisp =
        "l.last_name || ' ' || l.first_name || COALESCE(' ' || l.patronymic, '')";
    private const string OrgDisp = "l.abbreviation || ' ' || l.name";
    private const string ReservationDisp =
        "'Бронь №' || l.id_reservation || ' (заезд ' || to_char(l.check_in_date, 'DD.MM.YYYY') || ')'";
    private const string ContractDisp = "'Договор ' || l.contract_number";
    private const string StaffDisp = "l.last_name || ' ' || l.first_name";

    private static readonly Dictionary<string, TableDef> Defs = Build();

    public static TableDef Get(string table) => Defs[table];

    private static TableDef Simple(string table, string title, string pk, int max = 50) => new()
    {
        Table = table, Title = title, Pk = new[] { pk },
        Columns = new() { Col.Text("name", "Название", max) }
    };

    private static Dictionary<string, TableDef> Build()
    {
        var tables = new List<TableDef>
        {
            //  Гостиничный фонд 
            new()
            {
                Table = "hotel", Title = "Гостиницы", Pk = new[] { "id_hotel" },
                Columns = new()
                {
                    Col.Text("name", "Название", 50),
                    Col.Int("floor_count", "Этажей").Def("5"),
                    Col.Enum("hotel_class", "Класс (звёзд)", "hotel_class", "1", "2", "3", "4", "5").Def("3"),
                    Col.Int("total_rooms", "Всего номеров").Def("50"),
                }
            },
            new()
            {
                Table = "room", Title = "Номера", Pk = new[] { "id_room" },
                Columns = new()
                {
                    Col.Int("room_number", "Номер комнаты").Def("101"),
                    Col.Int("floor", "Этаж").Def("1"),
                    Col.Lookup("id_hotel", "Гостиница", "hotel", "id_hotel", Name),
                    Col.Enum("capacity", "Вместимость", "room_capacity", "1", "2", "3", "4").Def("2"),
                    Col.Dec("price", "Цена за сутки").Def("3000"),
                }
            },
            new()
            {
                Table = "hotel_services", Title = "Службы гостиниц", Pk = new[] { "id_hotelservices" },
                Columns = new()
                {
                    Col.Lookup("id_hotel", "Гостиница", "hotel", "id_hotel", Name),
                    Col.Lookup("id_service", "Услуга", "service", "id_service", Name),
                }
            },

            //  Клиенты
            new()
            {
                Table = "client", Title = "Клиенты (контакты)", Pk = new[] { "id_client" },
                Columns = new()
                {
                    Col.Text("phone", "Телефон", 15).Def("+7"),
                    Col.Text("bank_account", "Банковский счёт", 20).Optional(),
                    Col.Lookup("id_bank", "Банк", "bank", "id_bank", Name).Optional(),
                }
            },
            new()
            {
                Table = "natural_person", Title = "Физические лица", Pk = new[] { "id_naturalperson" },
                Columns = new()
                {
                    Col.Text("last_name", "Фамилия", 25),
                    Col.Text("first_name", "Имя", 25),
                    Col.Text("patronymic", "Отчество", 25).Optional(),
                    Col.Date("birth_date", "Дата рождения").Def("2000-01-01"),
                    Col.Text("series", "Серия паспорта", 4).Optional(),
                    Col.Text("number", "Номер паспорта", 6).Optional(),
                    Col.Date("issue_date", "Дата выдачи").Optional(),
                    Col.Text("issued_by", "Кем выдан", 100).Optional(),
                    Col.Lookup("id_client", "Клиент", "client", "id_client", ClientDisp),
                }
            },
            new()
            {
                Table = "legal_entity", Title = "Организации", Pk = new[] { "id_legalentity" },
                Columns = new()
                {
                    Col.Text("name", "Название", 50),
                    Col.Text("abbreviation", "Аббревиатура (ООО, АО...)", 10),
                    Col.Text("okopf_code", "Код ОКОПФ", 15),
                    Col.Text("inn", "ИНН", 12),
                    Col.Text("legal_address", "Юридический адрес", 150),
                    Col.Lookup("id_client", "Клиент", "client", "id_client", ClientDisp),
                }
            },
            new()
            {
                Table = "bank", Title = "Банки", Pk = new[] { "id_bank" },
                Columns = new()
                {
                    Col.Text("name", "Название", 50),
                    Col.Text("bik", "БИК", 9),
                    Col.Text("correspondent_account", "Корр. счёт", 20),
                }
            },

            // Бронирование 
            new()
            {
                Table = "reservation", Title = "Бронирования", Pk = new[] { "id_reservation" },
                Columns = new()
                {
                    Col.Date("booking_date", "Дата бронирования").Def("today"),
                    Col.Date("check_in_date", "Заезд").Def("today+1"),
                    Col.Date("check_out_date", "Выезд").Def("today+3"),
                    Col.Int("people_count", "Людей").Def("1"),
                    Col.Enum("status", "Статус", "reservation_status",
                        "забронировано", "заселено", "завершено", "отменено").Def("забронировано"),
                    Col.Date("cancellation_date", "Дата отмены").Optional(),
                    Col.Lookup("id_client", "Клиент", "client", "id_client", ClientDisp),
                }
            },
            new()
            {
                Table = "reservation_room", Title = "Номера брони", Pk = new[] { "id_reservationroom" },
                Columns = new()
                {
                    Col.Lookup("id_reservation", "Бронирование", "reservation", "id_reservation", ReservationDisp),
                    Col.Lookup("id_room", "Номер", "room", "id_room", RoomDisp),
                }
            },
            new()
            {
             
                Table = "reservation_guest", Title = "Проживающие по брони",
                Pk = new[] { "id_reservation", "id_naturalperson" },
                Columns = new()
                {
                    Col.Lookup("id_reservation", "Бронирование", "reservation", "id_reservation", ReservationDisp),
                    Col.Lookup("id_naturalperson", "Гость", "natural_person", "id_naturalperson", PersonDisp),
                }
            },
            new()
            {
                Table = "contract", Title = "Договоры", Pk = new[] { "id_contract" },
                Columns = new()
                {
                    Col.Text("contract_number", "Номер договора", 15),
                    Col.Date("conclusion_date", "Дата заключения").Def("today"),
                    Col.Date("start_date", "Начало действия").Def("today"),
                    Col.Date("end_date", "Окончание действия").Def("today+365"),
                    Col.Lookup("id_bank", "Банк", "bank", "id_bank", Name),
                    Col.Lookup("id_legalentity", "Организация", "legal_entity", "id_legalentity", OrgDisp),
                }
            },
            new()
            {
                Table = "contract_reservation", Title = "Привязка броней к договорам",
                Pk = new[] { "id_contractreservation" },
                Columns = new()
                {
                    Col.Lookup("id_contract", "Договор", "contract", "id_contract", ContractDisp),
                    Col.Lookup("id_reservation", "Бронирование", "reservation", "id_reservation", ReservationDisp),
                }
            },

            //  Услуги и жалобы 
            new()
            {
                Table = "service", Title = "Услуги", Pk = new[] { "id_service" },
                Columns = new()
                {
                    Col.Text("name", "Название", 25),
                    Col.Text("description", "Описание").Optional(),
                    Col.Dec("price", "Цена").Def("500"),
                }
            },
            new()
            {
                Table = "client_services", Title = "Оказанные услуги", Pk = new[] { "id_clientservices" },
                Columns = new()
                {
                    Col.Lookup("id_client", "Клиент", "client", "id_client", ClientDisp),
                    Col.Lookup("id_service", "Услуга", "service", "id_service", Name),
                    Col.Date("service_date", "Дата").Def("today"),
                }
            },
            new()
            {
                Table = "complaint", Title = "Жалобы", Pk = new[] { "id_complaint" },
                Columns = new()
                {
                    Col.Lookup("id_client", "Клиент", "client", "id_client", ClientDisp),
                    Col.Lookup("id_reservation", "Бронирование", "reservation", "id_reservation", ReservationDisp).Optional(),
                    Col.Lookup("id_room", "Номер", "room", "id_room", RoomDisp).Optional(),
                    Col.Date("complaint_date", "Дата жалобы").Def("today"),
                    Col.Text("complaint_text", "Текст жалобы"),
                    Col.Enum("status", "Статус", "complaint_status", "новая", "в работе", "решена").Def("новая"),
                }
            },

            //  Кадры 
            new()
            {
                Table = "staff", Title = "Сотрудники", Pk = new[] { "id_staff" },
                Columns = new()
                {
                    Col.Text("last_name", "Фамилия", 25),
                    Col.Text("first_name", "Имя", 25),
                    Col.Text("patronymic", "Отчество", 25).Optional(),
                    Col.Lookup("id_hotel", "Гостиница", "hotel", "id_hotel", Name),
                    Col.Date("birth_date", "Дата рождения").Def("1990-01-01"),
                    Col.Enum("gender", "Пол", "staff_gender", "мужской", "женский"),
                    Col.Text("work_experience", "Трудовой стаж", 50).Def("1 год"),
                    Col.Text("house_number", "Дом", 10),
                    Col.Lookup("id_city", "Город", "city", "id_city", Name),
                    Col.Lookup("id_street", "Улица", "street", "id_street", Name),
                }
            },
            new()
            {
                Table = "employment_record", Title = "Трудовая книжка", Pk = new[] { "id_tk" },
                Columns = new()
                {
                    Col.Lookup("id_staff", "Сотрудник", "staff", "id_staff", StaffDisp),
                    Col.Enum("event_type", "Вид мероприятия", "personnel_event", "прием", "увольнение", "перевод"),
                    Col.Date("event_date", "Дата мероприятия").Def("today"),
                    Col.Date("basis_document_date", "Дата документа-основания").Def("today"),
                    Col.Text("basis_document_number", "Номер документа-основания", 30),
                    Col.Enum("basis_document_type", "Вид документа-основания", "personnel_document", "приказ", "распоряжение"),
                    Col.Text("termination_reason", "Причина прекращения договора", 50).Optional(),
                    Col.Lookup("id_qualification", "Квалификация", "qualification", "id_qualification", Name).Optional(),
                    Col.Lookup("id_profession", "Профессия", "profession", "id_profession", Name).Optional(),
                    Col.Lookup("id_specialization", "Специальность", "specialization", "id_specialization", Name).Optional(),
                    Col.Lookup("id_structuraldivision", "Подразделение", "structural_division", "id_structuraldivision", Name),
                    Col.Lookup("id_position", "Должность", "position", "id_position", Name),
                    Col.Lookup("id_workplace", "Место работы", "work_place", "id_workplace", Name),
                }
            },
            Simple("position", "Должности", "id_position"),
            Simple("profession", "Профессии", "id_profession"),
            Simple("qualification", "Квалификации", "id_qualification"),
            Simple("specialization", "Специальности", "id_specialization"),
            Simple("structural_division", "Структурные подразделения", "id_structuraldivision"),
            Simple("work_place", "Места работы", "id_workplace"),

            //  Адреса 
            Simple("city", "Города", "id_city", 25),
            Simple("street", "Улицы", "id_street", 25),
        };

        return tables.ToDictionary(t => t.Table);
    }
}
