namespace HotelComplexApp.Models;

public enum ColType { Text, Int, Decimal, Date, Enum, Lookup }
public record LookupItem(int Id, string Text)
{
    public override string ToString() => Text;
}

public class ColumnDef
{
    public string Name { get; init; } = "";    
    public string Title { get; init; } = "";    
    public ColType Type { get; init; }
    public bool Required { get; private set; } = true;
    public string? Default { get; private set; }   
    public int MaxLength { get; init; }

    public string? EnumType { get; init; }
    public string[] EnumValues { get; init; } = Array.Empty<string>();

    public string? LookupTable { get; init; }
    public string? LookupKey { get; init; }
    public string? LookupDisplay { get; init; }      

    public ColumnDef Optional() { Required = false; return this; }
    public ColumnDef Def(string value) { Default = value; return this; }
}

// описание таблицы
public class TableDef
{
    public string Table { get; init; } = "";
    public string Title { get; init; } = "";
    public string[] Pk { get; init; } = Array.Empty<string>();
    public List<ColumnDef> Columns { get; init; } = new();
}

public static class Col
{
    public static ColumnDef Text(string name, string title, int max = 0)
        => new() { Name = name, Title = title, Type = ColType.Text, MaxLength = max };

    public static ColumnDef Int(string name, string title)
        => new() { Name = name, Title = title, Type = ColType.Int };

    public static ColumnDef Dec(string name, string title)
        => new() { Name = name, Title = title, Type = ColType.Decimal };

    public static ColumnDef Date(string name, string title)
        => new() { Name = name, Title = title, Type = ColType.Date };

    public static ColumnDef Enum(string name, string title, string enumType, params string[] values)
        => new() { Name = name, Title = title, Type = ColType.Enum, EnumType = enumType, EnumValues = values };

    public static ColumnDef Lookup(string name, string title, string table, string key, string display)
        => new()
        {
            Name = name, Title = title, Type = ColType.Lookup,
            LookupTable = table, LookupKey = key, LookupDisplay = display
        };
}
