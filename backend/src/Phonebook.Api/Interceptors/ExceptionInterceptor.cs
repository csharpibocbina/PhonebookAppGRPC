using System.Text.Json;
using FluentValidation;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Phonebook.Application.Common;

namespace Phonebook.Api.Interceptors;

/// <summary>
/// Interceptors are gRPC's middleware: they wrap every call. This one turns
/// our C# exceptions into gRPC status codes, so services can just throw.
///
/// gRPC errors are a status code + message (not an HTTP status). Extra data
/// goes in "trailers", metadata sent after the response. Validation errors are
/// put in the "validation-errors" trailer as JSON: { "firstName": ["..."] }.
/// </summary>
public sealed class ExceptionInterceptor(ILogger<ExceptionInterceptor> logger) : Interceptor
{
    public const string ValidationErrorsTrailer = "validation-errors";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (Exception ex) when (ex is not RpcException)
        {
            throw ToRpcException(ex, context);
        }
    }

    internal RpcException ToRpcException(Exception exception, ServerCallContext context)
    {
        switch (exception)
        {
            case ValidationException validation:
                var errors = validation.Errors
                    .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                var trailers = new Metadata { { ValidationErrorsTrailer, JsonSerializer.Serialize(errors) } };
                var message = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
                return new RpcException(new Status(StatusCode.InvalidArgument, message), trailers);

            case NotFoundException:
                return new RpcException(new Status(StatusCode.NotFound, exception.Message));

            case ConflictException:
                return new RpcException(new Status(StatusCode.AlreadyExists, exception.Message));

            case AuthenticationFailedException:
                return new RpcException(new Status(StatusCode.Unauthenticated, exception.Message));

            case OperationCanceledException when context.CancellationToken.IsCancellationRequested:
                return new RpcException(new Status(StatusCode.Cancelled, "The call was cancelled."));

            default:
                logger.LogError(exception, "Unhandled error in {Method}", context.Method);
                return new RpcException(new Status(StatusCode.Internal, "Something went wrong. Please try again."));
        }
    }
}
