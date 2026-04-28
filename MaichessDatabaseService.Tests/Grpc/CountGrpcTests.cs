using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Maichess.Database.V1;
using MaichessDatabaseService.Domain;
using MaichessDatabaseService.Grpc;
using MaichessDatabaseService.Tests.Support;
using NSubstitute;
using Xunit;

namespace MaichessDatabaseService.Tests.Grpc;

public sealed class CountGrpcTests
{
    [Fact]
    public async Task Count_EmptyFilter_CallsRepositoryWithEmptyDict()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.CountAsync("items", Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(5L);

        DatabaseGrpcService svc = new(repo);
        CountResponse response = await svc.Count(
            new CountRequest { Collection = "items" },
            TestServerCallContext.Create());

        Assert.Equal(5L, response.Count);
        await repo.Received(1).CountAsync(
            "items",
            Arg.Is<IReadOnlyDictionary<string, object?>>(d => d.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Count_NonEmptyFilter_PassesFilterToRepository()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.CountAsync("items", Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(2L);

        Struct filter = new();
        filter.Fields["color"] = Value.ForString("red");

        DatabaseGrpcService svc = new(repo);
        CountResponse response = await svc.Count(
            new CountRequest { Collection = "items", Filter = filter },
            TestServerCallContext.Create());

        Assert.Equal(2L, response.Count);
        await repo.Received(1).CountAsync(
            "items",
            Arg.Is<IReadOnlyDictionary<string, object?>>(d => d.ContainsKey("color") && (string?)d["color"] == "red"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Count_ReadOnlyMode_IsAllowed()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.CountAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(0L);

        DatabaseGrpcService svc = new(repo);
        CountResponse response = await svc.Count(
            new CountRequest { Collection = "items" },
            TestServerCallContext.Create());

        Assert.Equal(0L, response.Count);
    }

    [Fact]
    public async Task Count_EmptyCollection_ReturnsInvalidArgument()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        DatabaseGrpcService svc = new(repo);

        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Count(
                new CountRequest { Collection = string.Empty },
                TestServerCallContext.Create()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }
}
