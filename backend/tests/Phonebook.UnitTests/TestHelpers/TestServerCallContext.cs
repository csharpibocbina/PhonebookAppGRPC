using Grpc.Core;
using Microsoft.AspNetCore.Http;

namespace Phonebook.UnitTests.TestHelpers;

/// <summary>
/// ServerCallContext is abstract, so unit tests need their own small
/// implementation. Grpc.AspNetCore's GetHttpContext() looks for the
/// HttpContext under the "__HttpContext" key in UserState.
/// </summary>
public sealed class TestServerCallContext : ServerCallContext
{
    private readonly Metadata _requestHeaders = [];
    private readonly Metadata _responseTrailers = [];
    private readonly Dictionary<object, object> _userState = new();
    private readonly CancellationToken _cancellationToken;

    private TestServerCallContext(HttpContext httpContext, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _userState["__HttpContext"] = httpContext;
    }

    public static TestServerCallContext Create(HttpContext? httpContext = null, CancellationToken cancellationToken = default) =>
        new(httpContext ?? new DefaultHttpContext(), cancellationToken);

    protected override string MethodCore => "/test.v1.TestService/Method";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "ipv4:127.0.0.1:5000";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore => _requestHeaders;
    protected override CancellationToken CancellationTokenCore => _cancellationToken;
    protected override Metadata ResponseTrailersCore => _responseTrailers;
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore => new(null, new Dictionary<string, List<AuthProperty>>());
    protected override IDictionary<object, object> UserStateCore => _userState;

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
        throw new NotSupportedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
