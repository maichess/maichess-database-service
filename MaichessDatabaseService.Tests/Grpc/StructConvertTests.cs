using Google.Protobuf.WellKnownTypes;
using MaichessDatabaseService.Domain;
using MaichessDatabaseService.Grpc;
using Xunit;

namespace MaichessDatabaseService.Tests.Grpc;

public sealed class StructConvertTests
{
    [Fact]
    public void ToStruct_MapsIdAndFields()
    {
        DbRecord record = new("id1", new Dictionary<string, object?> { ["name"] = "Alice" });
        Struct s = StructConvert.ToStruct(record);

        Assert.Equal("id1", s.Fields["id"].StringValue);
        Assert.Equal("Alice", s.Fields["name"].StringValue);
    }

    [Fact]
    public void ToStruct_NullFieldValue_MapsToNullValue()
    {
        DbRecord record = new("x", new Dictionary<string, object?> { ["field"] = null });
        Struct s = StructConvert.ToStruct(record);

        Assert.Equal(Value.KindOneofCase.NullValue, s.Fields["field"].KindCase);
    }

    [Fact]
    public void ToStruct_BoolFieldValue_MapsToBool()
    {
        DbRecord record = new("x", new Dictionary<string, object?> { ["flag"] = true });
        Struct s = StructConvert.ToStruct(record);

        Assert.True(s.Fields["flag"].BoolValue);
    }

    [Fact]
    public void ToStruct_IntFieldValue_MapsToNumber()
    {
        DbRecord record = new("x", new Dictionary<string, object?> { ["count"] = 42 });
        Struct s = StructConvert.ToStruct(record);

        Assert.Equal(42, s.Fields["count"].NumberValue);
    }

    [Fact]
    public void ToStruct_LongFieldValue_MapsToNumber()
    {
        DbRecord record = new("x", new Dictionary<string, object?> { ["big"] = 1234567890123L });
        Struct s = StructConvert.ToStruct(record);

        Assert.Equal(1234567890123L, s.Fields["big"].NumberValue);
    }

    [Fact]
    public void ToStruct_FloatFieldValue_MapsToNumber()
    {
        DbRecord record = new("x", new Dictionary<string, object?> { ["rate"] = 1.5f });
        Struct s = StructConvert.ToStruct(record);

        Assert.Equal(1.5f, s.Fields["rate"].NumberValue);
    }

    [Fact]
    public void ToStruct_DoubleFieldValue_MapsToNumber()
    {
        DbRecord record = new("x", new Dictionary<string, object?> { ["score"] = 3.14 });
        Struct s = StructConvert.ToStruct(record);

        Assert.Equal(3.14, s.Fields["score"].NumberValue);
    }

    [Fact]
    public void ToStruct_GuidFieldValue_MapsToString()
    {
        Guid g = Guid.NewGuid();
        DbRecord record = new("x", new Dictionary<string, object?> { ["guid"] = g });
        Struct s = StructConvert.ToStruct(record);

        Assert.Equal(g.ToString(), s.Fields["guid"].StringValue);
    }

    [Fact]
    public void ToStruct_DateTimeFieldValue_MapsToString()
    {
        DateTime dt = new(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        DbRecord record = new("x", new Dictionary<string, object?> { ["ts"] = dt });
        Struct s = StructConvert.ToStruct(record);

        Assert.Contains("2024", s.Fields["ts"].StringValue);
    }

    [Fact]
    public void ToStruct_DateTimeOffsetFieldValue_MapsToString()
    {
        DateTimeOffset dto = new(2024, 1, 15, 10, 0, 0, TimeSpan.Zero);
        DbRecord record = new("x", new Dictionary<string, object?> { ["ts"] = dto });
        Struct s = StructConvert.ToStruct(record);

        Assert.Contains("2024", s.Fields["ts"].StringValue);
    }

    [Fact]
    public void ToStruct_DictionaryFieldValue_MapsToStruct()
    {
        var nested = new Dictionary<string, object?> { ["inner"] = "value" };
        DbRecord record = new("x", new Dictionary<string, object?> { ["obj"] = nested });
        Struct s = StructConvert.ToStruct(record);

        Assert.Equal(Value.KindOneofCase.StructValue, s.Fields["obj"].KindCase);
        Assert.Equal("value", s.Fields["obj"].StructValue.Fields["inner"].StringValue);
    }

    [Fact]
    public void ToStruct_ListFieldValue_MapsToList()
    {
        var list = new List<object?> { "a", "b" };
        DbRecord record = new("x", new Dictionary<string, object?> { ["arr"] = list });
        Struct s = StructConvert.ToStruct(record);

        Assert.Equal(Value.KindOneofCase.ListValue, s.Fields["arr"].KindCase);
        Assert.Equal(2, s.Fields["arr"].ListValue.Values.Count);
    }

    [Fact]
    public void ToStruct_UnknownType_MapsToString()
    {
        DbRecord record = new("x", new Dictionary<string, object?> { ["obj"] = new object() });
        Struct s = StructConvert.ToStruct(record);

        Assert.Equal(Value.KindOneofCase.StringValue, s.Fields["obj"].KindCase);
    }

    [Fact]
    public void ToDictionary_StringValue_MapsToString()
    {
        Struct s = new();
        s.Fields["name"] = Value.ForString("Bob");

        Dictionary<string, object?> dict = StructConvert.ToDictionary(s);

        Assert.Equal("Bob", dict["name"]);
    }

    [Fact]
    public void ToDictionary_NullValue_MapsToNull()
    {
        Struct s = new();
        s.Fields["field"] = Value.ForNull();

        Dictionary<string, object?> dict = StructConvert.ToDictionary(s);

        Assert.Null(dict["field"]);
    }

    [Fact]
    public void ToDictionary_BoolValue_MapsToBool()
    {
        Struct s = new();
        s.Fields["flag"] = Value.ForBool(true);

        Dictionary<string, object?> dict = StructConvert.ToDictionary(s);

        Assert.Equal(true, dict["flag"]);
    }

    [Fact]
    public void ToDictionary_NumberValue_MapsToDouble()
    {
        Struct s = new();
        s.Fields["num"] = Value.ForNumber(99.5);

        Dictionary<string, object?> dict = StructConvert.ToDictionary(s);

        Assert.Equal(99.5, dict["num"]);
    }

    [Fact]
    public void ToDictionary_StructValue_MapsToNestedDictionary()
    {
        Struct inner = new();
        inner.Fields["x"] = Value.ForNumber(1);

        Struct s = new();
        s.Fields["nested"] = Value.ForStruct(inner);

        Dictionary<string, object?> dict = StructConvert.ToDictionary(s);

        Assert.IsType<Dictionary<string, object?>>(dict["nested"]);
    }

    [Fact]
    public void ToDictionary_ListValue_MapsToList()
    {
        Struct s = new();
        s.Fields["arr"] = Value.ForList(Value.ForString("a"), Value.ForString("b"));

        Dictionary<string, object?> dict = StructConvert.ToDictionary(s);

        Assert.IsAssignableFrom<IEnumerable<object?>>(dict["arr"]);
    }

    [Fact]
    public void ToDictionary_NoneKind_MapsToNull()
    {
        Struct s = new();
        s.Fields["empty"] = new Value();

        Dictionary<string, object?> dict = StructConvert.ToDictionary(s);

        Assert.Null(dict["empty"]);
    }
}
