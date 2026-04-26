using System.Globalization;
using Google.Protobuf.WellKnownTypes;
using MaichessDatabaseService.Domain;

namespace MaichessDatabaseService.Grpc;

internal static class StructConvert
{
    internal static Struct ToStruct(DbRecord record)
    {
        Struct s = new();
        s.Fields["id"] = Value.ForString(record.Id);
        foreach ((string key, object? value) in record.Fields)
        {
            s.Fields[key] = ObjectToValue(value);
        }

        return s;
    }

    internal static Dictionary<string, object?> ToDictionary(Struct s)
    {
        var dict = new Dictionary<string, object?>(s.Fields.Count);
        foreach ((string key, Value value) in s.Fields)
        {
            dict[key] = ValueToObject(value);
        }

        return dict;
    }

    private static Value ObjectToValue(object? obj) => obj switch
    {
        null => Value.ForNull(),
        bool b => Value.ForBool(b),
        int i => Value.ForNumber(i),
        long l => Value.ForNumber(l),
        float f => Value.ForNumber(f),
        double d => Value.ForNumber(d),
        string s => Value.ForString(s),
        Guid g => Value.ForString(g.ToString()),
        DateTime dt => Value.ForString(dt.ToString("O", CultureInfo.InvariantCulture)),
        DateTimeOffset dto => Value.ForString(dto.ToString("O", CultureInfo.InvariantCulture)),
        IDictionary<string, object?> dict => Value.ForStruct(DictToStruct(dict)),
        System.Collections.IEnumerable list => Value.ForList(list.Cast<object?>().Select(ObjectToValue).ToArray()),
        _ => Value.ForString(obj.ToString() ?? string.Empty),
    };

    private static object? ValueToObject(Value v) => v.KindCase switch
    {
        Value.KindOneofCase.NullValue => null,
        Value.KindOneofCase.BoolValue => v.BoolValue,
        Value.KindOneofCase.NumberValue => v.NumberValue,
        Value.KindOneofCase.StringValue => v.StringValue,
        Value.KindOneofCase.StructValue => ToDictionary(v.StructValue),
        Value.KindOneofCase.ListValue => v.ListValue.Values.Select(ValueToObject).ToList(),
        _ => null,
    };

    private static Struct DictToStruct(IDictionary<string, object?> dict)
    {
        Struct s = new();
        foreach ((string key, object? value) in dict)
        {
            s.Fields[key] = ObjectToValue(value);
        }

        return s;
    }
}
