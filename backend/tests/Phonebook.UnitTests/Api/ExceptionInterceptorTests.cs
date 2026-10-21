using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Phonebook.Api.Interceptors;
using Phonebook.Application.Common;
using Phonebook.UnitTests.TestHelpers;

namespace Phonebook.UnitTests.Api;

public class ExceptionInterceptorTests
{
    private readonly ExceptionInterceptor _sut = new(NullLogger<ExceptionInterceptor>.Instance);

    public static TheoryData<Exception, StatusCode> Mappings => new()
    {
        { new NotFoundException("missing"), StatusCode.NotFound },
        { new ConflictException("taken"), StatusCode.AlreadyExists },
        { new AuthenticationFailedException("nope"), StatusCode.Unauthenticated },
        { new InvalidOperationException("boom"), StatusCode.Internal },
    };

    [Theory]
    [MemberData(nameof(Mappings))]
    public async Task Maps_exceptions_to_grpc_status_codes(Exception exception, StatusCode expected)
    {
        var ex = await Should.ThrowAsync<RpcException>(() => _sut.UnaryServerHandler<string, string>(
            "request", TestServerCallContext.Create(), (_, _) => throw exception));

        ex.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task Internal_errors_do_not_leak_the_exception_message()
    {
        var ex = await Should.ThrowAsync<RpcException>(() => _sut.UnaryServerHandler<string, string>(
            "request", TestServerCallContext.Create(), (_, _) => throw new InvalidOperationException("connection string xyz")));

        ex.Status.Detail.ShouldNotContain("xyz");
    }

    [Fact]
    public async Task Validation_errors_become_invalid_argument_with_field_errors_in_trailers()
    {
        var validation = new ValidationException([new ValidationFailure("FirstName", "'First Name' must not be empty.")]);

        var ex = await Should.ThrowAsync<RpcException>(() => _sut.UnaryServerHandler<string, string>(
            "request", TestServerCallContext.Create(), (_, _) => throw validation));

        ex.StatusCode.ShouldBe(StatusCode.InvalidArgument);
        var json = ex.Trailers.GetValue(ExceptionInterceptor.ValidationErrorsTrailer);
        json.ShouldNotBeNull();
        var errors = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json)!;
        errors["firstName"].ShouldBe(["'First Name' must not be empty."]);
    }

    [Fact]
    public async Task RpcExceptions_pass_through_untouched()
    {
        var original = new RpcException(new Status(StatusCode.PermissionDenied, "no"));

        var ex = await Should.ThrowAsync<RpcException>(() => _sut.UnaryServerHandler<string, string>(
            "request", TestServerCallContext.Create(), (_, _) => throw original));

        ex.ShouldBeSameAs(original);
    }

    [Fact]
    public async Task Successful_calls_return_the_response()
    {
        var response = await _sut.UnaryServerHandler<string, string>(
            "request", TestServerCallContext.Create(), (req, _) => Task.FromResult(req + "!"));

        response.ShouldBe("request!");
    }
}
