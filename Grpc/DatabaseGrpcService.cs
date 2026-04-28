using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Maichess.Database.V1;
using MaichessDatabaseService.Domain;

namespace MaichessDatabaseService.Grpc;

internal sealed class DatabaseGrpcService(IRecordRepository repository) : Database.DatabaseBase
{
    public override async Task<GetResponse> Get(GetRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Collection))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "collection is required"));
        }

        DbRecord record = await repository.GetAsync(request.Collection, request.Id, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, $"{request.Collection}/{request.Id} not found"));
        return new GetResponse { Record = StructConvert.ToStruct(record) };
    }

    public override async Task<ListResponse> List(ListRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Collection))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "collection is required"));
        }

        Dictionary<string, object?> filter = request.Filter is not null
            ? StructConvert.ToDictionary(request.Filter)
            : [];

        IReadOnlyList<DbRecord> records = await repository.ListAsync(
            request.Collection, filter, request.Limit, request.Offset, context.CancellationToken);

        ListResponse response = new();
        response.Records.AddRange(records.Select(StructConvert.ToStruct));
        return response;
    }

    public override async Task<CountResponse> Count(CountRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Collection))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "collection is required"));
        }

        Dictionary<string, object?> filter = request.Filter is not null
            ? StructConvert.ToDictionary(request.Filter)
            : [];

        long count = await repository.CountAsync(request.Collection, filter, context.CancellationToken);
        return new CountResponse { Count = count };
    }

    public override async Task<InsertResponse> Insert(InsertRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Collection))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "collection is required"));
        }

        try
        {
            var fields = StructConvert.ToDictionary(request.Record);
            fields.Remove("id");
            DbRecord record = await repository.InsertAsync(request.Collection, fields, context.CancellationToken);
            return new InsertResponse { Record = StructConvert.ToStruct(record) };
        }
        catch (ReadOnlyViolationException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
        catch (AlreadyExistsException ex)
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, ex.Message));
        }
    }

    public override async Task<UpdateResponse> Update(UpdateRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Collection))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "collection is required"));
        }

        try
        {
            var fields = StructConvert.ToDictionary(request.Fields);
            DbRecord record = await repository.UpdateAsync(
                request.Collection, request.Id, fields, context.CancellationToken);
            return new UpdateResponse { Record = StructConvert.ToStruct(record) };
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (ReadOnlyViolationException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
        catch (AlreadyExistsException ex)
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, ex.Message));
        }
    }

    public override async Task<DeleteResponse> Delete(DeleteRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Collection))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "collection is required"));
        }

        try
        {
            await repository.DeleteAsync(request.Collection, request.Id, context.CancellationToken);
            return new DeleteResponse();
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (ReadOnlyViolationException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
    }

    public override async Task<DeleteWhereResponse> DeleteWhere(DeleteWhereRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Collection))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "collection is required"));
        }

        try
        {
            Dictionary<string, object?> filter = request.Filter is not null
                ? StructConvert.ToDictionary(request.Filter)
                : [];

            long deleted = await repository.DeleteWhereAsync(request.Collection, filter, context.CancellationToken);
            return new DeleteWhereResponse { DeletedCount = deleted };
        }
        catch (ReadOnlyViolationException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
    }
}
