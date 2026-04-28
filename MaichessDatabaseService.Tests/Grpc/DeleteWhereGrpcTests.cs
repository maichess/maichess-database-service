using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Maichess.Database.V1;
using MaichessDatabaseService.Domain;
using MaichessDatabaseService.Grpc;
using MaichessDatabaseService.Tests.Support;
using NSubstitute;
using Xunit;

namespace MaichessDatabaseService.Tests.Grpc;

public sealed class DeleteWhereGrpcTests
{
    [Fact]
    public async Task DeleteWhere_EmptyFilter_CallsRepositoryWithEmptyDict()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.DeleteWhereAsync("items", Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(3L);

        DatabaseGrpcService svc = new(repo);
        DeleteWhereResponse response = await svc.DeleteWhere(
            new DeleteWhereRequest { Collection = "items" },
            TestServerCallContext.Create());

        Assert.Equal(3L, response.DeletedCount);
        await repo.Received(1).DeleteWhereAsync(
            "items",
            Arg.Is<IReadOnlyDictionary<string, object?>>(d => d.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteWhere_NonEmptyFilter_PassesFilterToRepository()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.DeleteWhereAsync("items", Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(1L);

        Struct filter = new();
        filter.Fields["status"] = Value.ForString("active");

        DatabaseGrpcService svc = new(repo);
        await svc.DeleteWhere(
            new DeleteWhereRequest { Collection = "items", Filter = filter },
            TestServerCallContext.Create());

        await repo.Received(1).DeleteWhereAsync(
            "items",
            Arg.Is<IReadOnlyDictionary<string, object?>>(d => d.ContainsKey("status") && (string?)d["status"] == "active"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteWhere_ReadOnlyMode_ReturnsPermissionDenied()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.When(r => r.DeleteWhereAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>()))
            .Throw(new ReadOnlyViolationException());

        DatabaseGrpcService svc = new(repo);
        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.DeleteWhere(
                new DeleteWhereRequest { Collection = "items" },
                TestServerCallContext.Create()));

        Assert.Equal(StatusCode.PermissionDenied, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteWhere_EmptyCollection_ReturnsInvalidArgument()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        DatabaseGrpcService svc = new(repo);

        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.DeleteWhere(
                new DeleteWhereRequest { Collection = string.Empty },
                TestServerCallContext.Create()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }
}
