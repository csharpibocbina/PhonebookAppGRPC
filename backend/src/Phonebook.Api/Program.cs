using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Phonebook.Api.Auth;
using Phonebook.Api.Hubs;
using Phonebook.Api.Interceptors;
using Phonebook.Api.Services;
using Phonebook.Application;
using Phonebook.Application.Auth;
using Phonebook.Application.Notifications;
using Phonebook.Infrastructure;
using Phonebook.Infrastructure.Auth;
using Phonebook.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------------------------------------------------------------------------
// Application + infrastructure (plain C#: EF Core, JWT, email)
// ---------------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(config);
builder.Services.AddSingleton<RefreshTokenCookie>();

// ---------------------------------------------------------------------------
// gRPC
// ---------------------------------------------------------------------------
builder.Services
    .AddGrpc(options =>
    {
        // Interceptors run around every call, outermost first.
        options.Interceptors.Add<LoggingInterceptor>();
        options.Interceptors.Add<ExceptionInterceptor>();
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    })
    // JSON transcoding: each rpc with a google.api.http option also becomes a
    // REST endpoint (e.g. GET /v1/contacts) that accepts and returns JSON.
    .AddJsonTranscoding();

// Lets tools like grpcurl and Postman ask the server "which services do you have?".
builder.Services.AddGrpcReflection();

// Standard gRPC health service (grpc.health.v1.Health/Check) + an HTTP /healthz.
builder.Services.AddGrpcHealthChecks().AddDbContextCheck<AppDbContext>();

// ---------------------------------------------------------------------------
// OpenAPI from the proto annotations, shown by Scalar at /scalar
// ---------------------------------------------------------------------------
builder.Services.AddGrpcSwagger();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Phonebook API",
        Version = "v1",
        Description = "The REST/JSON view of the Phonebook gRPC services, generated with gRPC JSON transcoding. " +
                      "Every endpoint here is the same C# method that gRPC and gRPC-Web clients call.",
    });
    var xml = Path.Combine(AppContext.BaseDirectory, "Phonebook.Api.xml");
    if (File.Exists(xml))
        c.IncludeGrpcXmlComments(xml, includeControllerXmlComments: true);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the accessToken from /v1/auth/login.",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

// ---------------------------------------------------------------------------
// Auth: JWT bearer tokens for gRPC, REST and SignalR
// ---------------------------------------------------------------------------
var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep "sub", "email", "name" as-is
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = JwtTokenService.CreateSigningKey(jwt.SigningKey),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
        };
        options.Events = new JwtBearerEvents
        {
            // Browsers can't add headers to a WebSocket, so the SignalR client
            // sends the token as ?access_token=... Only accept that on /hubs.
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = token;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

// Slows down password guessing on the auth service.
var authPermitLimit = config.GetValue("RateLimiting:AuthPermitLimit", 20);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1) }));
});

// ---------------------------------------------------------------------------
// SignalR notifications
// ---------------------------------------------------------------------------
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();
builder.Services.AddSingleton<INotificationPublisher, SignalRNotificationPublisher>();

// Only needed when the React dev server runs on a different origin without its proxy.
var corsOrigins = config.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    // gRPC-Web returns status in these headers; the browser must be allowed to read them.
    .WithExposedHeaders("grpc-status", "grpc-message", "grpc-status-details-bin", "validation-errors")));

var app = builder.Build();

// ---------------------------------------------------------------------------
// HTTP pipeline
// ---------------------------------------------------------------------------
// gRPC-Web: lets browsers call gRPC services over HTTP/1.1 with fetch().
RefreshTokenCookie.UseGrpcWebDetection(app);
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGrpcService<AuthGrpcService>().RequireRateLimiting("auth");
app.MapGrpcService<PhonebookGrpcService>();
app.MapGrpcHealthChecksService();
app.MapGrpcReflectionService();
app.MapHealthChecks("/healthz");
app.MapHub<NotificationHub>(NotificationHub.Path);

if (config.GetValue("ApiDocs:Enabled", true))
{
    app.UseSwagger();                       // /swagger/v1/swagger.json
    app.MapScalarApiReference(options =>    // /scalar
    {
        options.Title = "Phonebook API";
        options.OpenApiRoutePattern = "/swagger/{documentName}/swagger.json";
        options.AddPreferredSecuritySchemes("Bearer");
    });
}

app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync(config.GetValue("Database:SeedDemoData", true));
}

app.Run();

// Makes Program visible to WebApplicationFactory in the integration tests.
public partial class Program;
