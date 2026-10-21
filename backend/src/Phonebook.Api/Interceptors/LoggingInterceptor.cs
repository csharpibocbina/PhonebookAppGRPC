using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Phonebook.Api.Interceptors;

/// <summary>Logs every unary call: method name, status code and duration.</summary>
public sealed class LoggingInterceptor(ILogger<LoggingInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var stopwatch = Stopwatch.StartNew();
        var status = StatusCode.OK;
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException ex)
        {
            status = ex.StatusCode;
            throw;
        }
        finally
        {
            // context.Method looks like "/phonebook.v1.PhonebookService/ListContacts"
            logger.LogInformation("gRPC {Method} -> {Status} in {Elapsed} ms",
                context.Method, status, stopwatch.ElapsedMilliseconds);
        }
    }
}
