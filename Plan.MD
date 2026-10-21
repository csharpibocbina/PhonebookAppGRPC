# Phonebook Web App (gRPC) — Implementation Plan

**Stack:** C# / .NET 10 · ASP.NET Core gRPC · SignalR · EF Core · PostgreSQL · JWT (access + refresh) · Scalar API docs · MailKit + PaperCut SMTP · xUnit + FakeItEasy · React 19 + TypeScript (Vite) · Tailwind CSS + shadcn/ui · Docker / docker-compose

**Goal:** A phonebook app where a signed-in user can add, edit, delete, search and list contacts. The backend is a real gRPC service, and the project is built to *teach* gRPC: every design choice below includes a short "how gRPC works here" note.

---

## 1. What we are building

| Area | Features |
|---|---|
| **Auth** | Register, login, refresh token, logout, "who am I". JWT access token (short-lived) + refresh token (long-lived, rotated, revocable). |
| **Phonebook** | Create, get, list (paged + search + sort), update, delete contacts. Each user only sees their own contacts. |
| **API docs** | Scalar UI at `/scalar` showing the REST/JSON view of the gRPC services (via gRPC JSON transcoding), with JWT "Authorize" support. |
| **Frontend** | Modern React UI: public **landing page**, **login / register page**, protected **contacts dashboard** (table/cards, search, add/edit dialog, delete confirm, toasts, dark mode). |
| **Notifications** | **SignalR** pushes events (contact added/updated/deleted, signed in, email sent) to the React app, shown as toasts that auto-hide after **5 seconds**. |
| **Email** | Welcome email on register and a "new sign-in" alert on login, sent over SMTP to **PaperCut SMTP** (a fake inbox you can open in the browser, nothing is really delivered). |
| **Quality** | Unit tests (services, validators, token logic) + integration tests (real gRPC calls against an in-memory test server) + a few frontend component tests. |
| **Hosting** | `docker compose up` starts Postgres, PaperCut SMTP, the API and the web app (served by nginx). |

---

## 2. gRPC in 5 minutes (the mental model for this project)

**gRPC is "call a function on another machine".** You don't design URLs and verbs like REST. You design a **service** with **methods** and **messages** in a `.proto` file, and tools generate the server base class and the client for you.

1. **Contract first (`.proto`).** The `.proto` file is the single source of truth. Example:
   ```proto
   service PhonebookService {
     rpc CreateContact (CreateContactRequest) returns (Contact);
   }
   ```
2. **Protocol Buffers (protobuf).** Messages are serialized to a compact **binary** format, not JSON. Each field has a number (`string name = 1;`) and the number, not the name, goes on the wire. That's why you must never reuse or renumber fields: it's how gRPC stays backward compatible.
3. **Code generation.**
   - C#: `Grpc.Tools` reads the `.proto` at build time and generates `PhonebookService.PhonebookServiceBase` (we override its methods) and message classes.
   - TypeScript: `buf` + `protoc-gen-es` generate typed TS messages and a typed client. Same contract, both sides, no hand-written DTOs.
4. **Transport: HTTP/2.** Every call is an HTTP/2 POST to `/<package>.<Service>/<Method>` (e.g. `/phonebook.v1.PhonebookService/CreateContact`). HTTP/2 gives multiplexing (many calls on one connection) and streaming.
5. **Four call types.** Unary (1 request → 1 response, used for CRUD), server streaming, client streaming, bidirectional streaming. We use **unary** for CRUD and add one **server-streaming** method as a learning stretch (section 4.3).
6. **Metadata = headers.** Auth tokens travel as metadata: `authorization: Bearer <jwt>`. ASP.NET Core's normal `[Authorize]` and JWT middleware work on gRPC methods.
7. **Status codes, not HTTP codes.** Errors are `RpcException(new Status(StatusCode.NotFound, "Contact not found"))`. Codes we'll use: `OK`, `InvalidArgument`, `NotFound`, `AlreadyExists`, `Unauthenticated`, `PermissionDenied`, `Internal`.
8. **Deadlines & cancellation.** The client sets a deadline; the server gets a `CancellationToken` in `ServerCallContext` and passes it to EF Core.
9. **Interceptors = gRPC middleware.** We add a server interceptor for logging + mapping exceptions (e.g. `ValidationException` → `InvalidArgument`), and a client interceptor in React that attaches the token and auto-refreshes it.

### Why browsers need gRPC-Web
Browsers can't control HTTP/2 framing or read trailers, so they can't speak "raw" gRPC. **gRPC-Web** is a small variant that works over normal `fetch`. ASP.NET Core supports it natively (`Grpc.AspNetCore.Web`), so **no Envoy proxy is needed**. The React app uses **Connect-ES** (`@connectrpc/connect-web`) with the gRPC-Web transport.

### Why JSON transcoding (and how Scalar fits)
Scalar (like Swagger) documents **REST/OpenAPI** APIs, and gRPC isn't REST. **gRPC JSON transcoding** lets us annotate each RPC with an HTTP route (`google.api.http`), and ASP.NET Core then exposes the *same* C# method as both:
- gRPC / gRPC-Web: `POST /phonebook.v1.PhonebookService/CreateContact` (binary protobuf)
- REST/JSON: `POST /v1/contacts` (JSON)

An OpenAPI document is generated from those annotations, and **Scalar** renders it at `/scalar`, so you can try every method from the browser and compare it with the gRPC calls. One implementation, three ways to call it. That comparison is the best way to "see" what gRPC is doing.

```
                ┌──────────── ASP.NET Core (Kestrel, HTTP/1.1 + HTTP/2) ───────────┐
React (Connect) │  gRPC-Web  ─┐                                                    │
grpcurl / C#    │  gRPC      ─┼─▶ AuthService / PhonebookService ─▶ EF Core ─▶ Postgres
Scalar / curl   │  REST/JSON ─┘   (one C# class per service)                       │
                │  /scalar  ◀── OpenAPI generated from proto http annotations      │
                └──────────────────────────────────────────────────────────────────┘
```

---

## 3. Solution structure

```
phonebook-grpc/
├─ proto/                              # shared contract (source of truth)
│  ├─ auth/v1/auth.proto
│  ├─ phonebook/v1/phonebook.proto
│  └─ google/api/{annotations,http}.proto
├─ backend/
│  ├─ Phonebook.sln
│  ├─ src/
│  │  ├─ Phonebook.Api/                # gRPC host: Program.cs, Services/, Interceptors/
│  │  ├─ Phonebook.Application/        # use cases, validators, interfaces (no gRPC types)
│  │  ├─ Phonebook.Domain/             # entities: User, Contact, RefreshToken
│  │  └─ Phonebook.Infrastructure/     # EF Core DbContext, migrations, repos, JWT, password hashing
│  └─ tests/
│     ├─ Phonebook.UnitTests/
│     └─ Phonebook.IntegrationTests/
├─ frontend/
│  ├─ buf.gen.yaml                     # TS codegen from ../proto
│  ├─ src/{gen,api,auth,pages,components,hooks,lib}
│  └─ nginx.conf
├─ docker-compose.yml
├─ .env.example
└─ README.md                           # run guide + "gRPC walkthrough" with grpcurl examples
```

**Why layered:** gRPC is just the *transport*. Keeping the gRPC service classes thin (map proto → command → call application layer → map back) makes the business logic unit-testable without any gRPC plumbing, and makes it obvious which code is "gRPC" and which is ordinary C#.

---

## 4. API contract (proto)

### 4.1 `auth/v1/auth.proto`
```proto
syntax = "proto3";
package auth.v1;
option csharp_namespace = "Phonebook.Api.Protos.Auth";
import "google/api/annotations.proto";

service AuthService {
  rpc Register (RegisterRequest) returns (AuthResponse) { option (google.api.http) = { post: "/v1/auth/register" body: "*" }; }
  rpc Login    (LoginRequest)    returns (AuthResponse) { option (google.api.http) = { post: "/v1/auth/login" body: "*" }; }
  rpc Refresh  (RefreshRequest)  returns (AuthResponse) { option (google.api.http) = { post: "/v1/auth/refresh" body: "*" }; }
  rpc Logout   (LogoutRequest)   returns (LogoutResponse) { option (google.api.http) = { post: "/v1/auth/logout" body: "*" }; }
  rpc Me       (MeRequest)       returns (UserProfile)  { option (google.api.http) = { get: "/v1/auth/me" }; }
}

message RegisterRequest { string email = 1; string password = 2; string display_name = 3; }
message LoginRequest    { string email = 1; string password = 2; }
message RefreshRequest  { string refresh_token = 1; }   // optional: falls back to httpOnly cookie
message LogoutRequest   { string refresh_token = 1; }
message LogoutResponse  {}
message MeRequest       {}
message AuthResponse {
  string access_token = 1;
  int64  access_token_expires_at = 2;   // unix seconds
  string refresh_token = 3;             // also set as httpOnly cookie for the browser
  UserProfile user = 4;
}
message UserProfile { string id = 1; string email = 2; string display_name = 3; }
```

### 4.2 `phonebook/v1/phonebook.proto`
```proto
syntax = "proto3";
package phonebook.v1;
option csharp_namespace = "Phonebook.Api.Protos.Phonebook";
import "google/api/annotations.proto";
import "google/protobuf/timestamp.proto";

service PhonebookService {
  rpc CreateContact (CreateContactRequest) returns (Contact)              { option (google.api.http) = { post: "/v1/contacts" body: "*" }; }
  rpc GetContact    (GetContactRequest)    returns (Contact)              { option (google.api.http) = { get: "/v1/contacts/{id}" }; }
  rpc ListContacts  (ListContactsRequest)  returns (ListContactsResponse) { option (google.api.http) = { get: "/v1/contacts" }; }
  rpc UpdateContact (UpdateContactRequest) returns (Contact)              { option (google.api.http) = { put: "/v1/contacts/{id}" body: "*" }; }
  rpc DeleteContact (DeleteContactRequest) returns (DeleteContactResponse){ option (google.api.http) = { delete: "/v1/contacts/{id}" }; }
}

message Contact {
  string id = 1;
  string first_name = 2;
  string last_name = 3;
  string phone_number = 4;
  string email = 5;
  string address = 6;
  string notes = 7;
  bool   is_favorite = 8;
  google.protobuf.Timestamp created_at = 9;
  google.protobuf.Timestamp updated_at = 10;
}
message CreateContactRequest { string first_name = 1; string last_name = 2; string phone_number = 3; string email = 4; string address = 5; string notes = 6; bool is_favorite = 7; }
message GetContactRequest    { string id = 1; }
message UpdateContactRequest { string id = 1; string first_name = 2; string last_name = 3; string phone_number = 4; string email = 5; string address = 6; string notes = 7; bool is_favorite = 8; }
message DeleteContactRequest { string id = 1; }
message DeleteContactResponse {}
message ListContactsRequest {
  int32  page = 1;            // 1-based
  int32  page_size = 2;       // default 20, max 100
  string search = 3;          // matches name / phone / email
  SortBy sort_by = 4;
  bool   favorites_only = 5;
}
enum SortBy { SORT_BY_UNSPECIFIED = 0; SORT_BY_FIRST_NAME = 1; SORT_BY_LAST_NAME = 2; SORT_BY_CREATED_AT = 3; }
message ListContactsResponse { repeated Contact contacts = 1; int32 total_count = 2; int32 page = 3; int32 page_size = 4; }
```

**Learning notes baked into the contract:** field numbers, `repeated` (lists), `enum` with a zero "unspecified" value (proto3 rule), well-known types (`Timestamp`), and empty messages for "void" responses (forward-compatible: fields can be added later).

### 4.3 Stretch: server streaming
`rpc WatchContacts (WatchContactsRequest) returns (stream ContactEvent);` pushes `CREATED/UPDATED/DELETED` events so two open browser tabs update live. gRPC-Web supports server streaming, so this shows streaming end to end. Built after the core is done.

---

## 5. Backend design (.NET 10)

### 5.1 Packages
- `Grpc.AspNetCore` (server + `Grpc.Tools` codegen)
- `Grpc.AspNetCore.Web` (browser support)
- `Microsoft.AspNetCore.Grpc.JsonTranscoding` (REST/JSON view)
- `Microsoft.AspNetCore.Grpc.Swagger` (OpenAPI generation from transcoding annotations) + `Scalar.AspNetCore` (UI)
- `Grpc.AspNetCore.Server.Reflection` (lets `grpcurl` / Postman discover services, dev only)
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `Microsoft.EntityFrameworkCore` + `Npgsql.EntityFrameworkCore.PostgreSQL`
- `FluentValidation`
- `Grpc.HealthCheck` (gRPC health protocol, used by Docker healthcheck)
- SignalR (built into ASP.NET Core, no extra package on the server)
- `MailKit` (SMTP client for sending email to PaperCut)

### 5.2 `Program.cs` outline
```csharp
builder.Services.AddGrpc(o => { o.Interceptors.Add<ExceptionInterceptor>(); o.Interceptors.Add<LoggingInterceptor>(); })
                .AddJsonTranscoding();
builder.Services.AddGrpcSwagger();               // OpenAPI from proto http annotations
builder.Services.AddGrpcReflection();
builder.Services.AddGrpcHealthChecks().AddDbContextCheck<AppDbContext>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...);
builder.Services.AddAuthorization();
builder.Services.AddSignalR();                   // notifications hub
builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();
builder.Services.AddSingleton<IEmailQueue, ChannelEmailQueue>();
builder.Services.AddHostedService<EmailBackgroundService>();
builder.Services.AddCors(...);                    // expose grpc-status, grpc-message headers for gRPC-Web

app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });
app.UseCors(); app.UseAuthentication(); app.UseAuthorization();
app.MapGrpcService<AuthGrpcService>();
app.MapGrpcService<PhonebookGrpcService>();       // class has [Authorize]
app.MapGrpcHealthChecksService();
app.MapHub<NotificationHub>("/hubs/notifications");
if (dev) { app.MapGrpcReflectionService(); app.UseSwagger(); app.MapScalarApiReference(o => o.WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json")); }
```
Kestrel listens with `HttpProtocols.Http1AndHttp2` so one port serves gRPC (HTTP/2), gRPC-Web and REST (HTTP/1.1). In Docker we run h2c (HTTP/2 without TLS) inside the network.

### 5.3 Domain & data
- `User { Id, Email (unique), DisplayName, PasswordHash, CreatedAt }`
- `Contact { Id, OwnerId → User, FirstName, LastName, PhoneNumber, Email?, Address?, Notes?, IsFavorite, CreatedAt, UpdatedAt }` with index on `(OwnerId, LastName, FirstName)`.
- `RefreshToken { Id, UserId, TokenHash, FamilyId, ExpiresAt, CreatedAt, RevokedAt?, ReplacedByTokenId? }`
- EF Core migrations, applied on startup in Development/Docker; seed a demo user + ~10 contacts.

### 5.4 Service flow (example: `UpdateContact`)
1. gRPC method receives `UpdateContactRequest` and `ServerCallContext`.
2. Read user id from `context.GetHttpContext().User` (`sub` claim).
3. Map to `UpdateContactCommand`, validate (FluentValidation: required names, phone format E.164-ish, email format, max lengths).
4. `ContactService.UpdateAsync(userId, command, context.CancellationToken)`. Throws `NotFoundException` if the contact doesn't exist **or belongs to another user** (don't leak existence).
5. Map entity → proto `Contact`, return.
6. `ExceptionInterceptor` maps: `ValidationException` → `InvalidArgument` (with field errors in trailers), `NotFoundException` → `NotFound`, `ConflictException` → `AlreadyExists`, anything else → `Internal` (logged, generic message).

---

## 6. JWT authentication (access + refresh)

| Token | Format | Lifetime | Where it lives in the browser |
|---|---|---|---|
| Access token | JWT, HS256 signed, claims `sub`, `email`, `name`, `jti` | 15 min | In memory only (React state), never localStorage |
| Refresh token | 64 random bytes (base64url), **not** a JWT | 7 days | `httpOnly`, `Secure`, `SameSite=Strict` cookie, path `/auth.v1.AuthService` + `/v1/auth` |

**Flows**
1. **Login/Register** → server verifies password (ASP.NET Core `PasswordHasher<User>`, PBKDF2), issues both tokens, stores the refresh token's **SHA-256 hash** in DB, sets cookie, returns the access token.
2. **Authenticated call** → client interceptor adds `authorization: Bearer <access>` metadata. JWT middleware validates issuer, audience, lifetime, signature; `[Authorize]` on `PhonebookGrpcService` returns `Unauthenticated` if missing/invalid.
3. **Refresh** → on `Unauthenticated` (or shortly before expiry) the client calls `Refresh`. Server finds the token by hash; if valid it **rotates** (revokes old, issues new pair in the same family).
4. **Reuse detection** → if an already-revoked refresh token is presented, the whole token family is revoked (likely theft) and the user must log in again.
5. **Logout** → revoke the refresh token, clear the cookie, client drops the access token.
6. **App reload** → on startup the React app silently calls `Refresh` (cookie is sent automatically); success = logged in, failure = show landing/login.

Config via env: `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey` (≥ 32 bytes, from `.env`, never committed), `Jwt__AccessTokenMinutes`, `Jwt__RefreshTokenDays`. Login is rate-limited (ASP.NET Core rate limiter, e.g. 5/min per IP).

*How gRPC works here:* auth is just metadata + standard ASP.NET Core auth. The browser cookie works because nginx serves the web app and proxies the API on the **same origin**, and gRPC-Web is plain `fetch` with `credentials: "include"`.

---

## 7. Real-time notifications (SignalR) & email (PaperCut SMTP)

### 7.1 Why SignalR next to gRPC?
gRPC answers requests the browser makes. Notifications go the other way: the **server** decides when to tell the browser something happened. gRPC-Web can do that with server streaming (section 4.3), but **SignalR** is the standard ASP.NET Core tool for it: it picks the best transport (WebSockets, falling back to Server-Sent Events or long polling), reconnects automatically, and lets the server target one user (`Clients.User(userId)`) across all their open tabs. Using both is a nice lesson in "the right tool for each direction":

| | gRPC (gRPC-Web) | SignalR |
|---|---|---|
| Who starts it | Client calls a method | Server pushes an event |
| Contract | `.proto`, generated types | Hub method names + TS types we define |
| Used here for | Auth + phonebook CRUD | Toast notifications |

### 7.2 Server side
- `NotificationHub : Hub` mapped at `/hubs/notifications`, protected with `[Authorize]`.
- Browsers can't set an `Authorization` header on a WebSocket, so the SignalR client sends the access token as `?access_token=...`. The JWT setup reads it in `JwtBearerEvents.OnMessageReceived`, only for paths starting with `/hubs`.
- SignalR uses the JWT `sub` claim as the user id (custom `IUserIdProvider`), so `Clients.User(id)` reaches every tab that user has open.
- Application code depends on an `INotificationPublisher` interface, not on SignalR directly. The SignalR implementation uses `IHubContext<NotificationHub, INotificationClient>`. That keeps services testable: in unit tests it's an `A.Fake<INotificationPublisher>()` and we assert `A.CallTo(() => publisher.PublishAsync(userId, A<Notification>.That.Matches(n => n.Type == "ContactCreated"), A<CancellationToken>._)).MustHaveHappenedOnceExactly()`.
- Strongly typed hub client:
  ```csharp
  public interface INotificationClient { Task Notify(NotificationDto notification); }
  public record NotificationDto(string Id, string Type, string Title, string Message, string Level, DateTimeOffset CreatedAt);
  // Level: "success" | "info" | "warning" | "error"
  ```

**Events pushed to the user**

| Type | When | Example toast |
|---|---|---|
| `ContactCreated` | After `CreateContact` succeeds | "Contact added: Maria Santos" |
| `ContactUpdated` | After `UpdateContact` | "Contact updated: Maria Santos" |
| `ContactDeleted` | After `DeleteContact` | "Contact deleted: Maria Santos" |
| `SignedIn` | Login from another tab/device | "New sign-in to your account" |
| `EmailSent` | After the background email job sends | "Welcome email sent to jay@example.com" |

Because the event goes to *all* the user's tabs, a contact added in one tab also refreshes the list in the other (the React handler invalidates the TanStack Query cache). That covers the "live update" idea without needing the gRPC streaming stretch.

### 7.3 Email via PaperCut SMTP
- **PaperCut SMTP** runs as a Docker container. It accepts any email over SMTP and shows it in a web inbox, so you can see real emails without sending anything to the internet.
- `IEmailSender` interface + `SmtpEmailSender` (MailKit). Settings from env: `Smtp__Host=papercut`, `Smtp__Port=25`, `Smtp__From=no-reply@phonebook.local`, no auth or TLS (local only).
- Emails: **welcome email** on register, **new sign-in alert** on login. Simple HTML templates with a plain-text fallback.
- Sending is **not** done inside the gRPC call. The service puts an `EmailMessage` on an in-memory queue (`System.Threading.Channels`), and a `BackgroundService` sends it and then publishes the `EmailSent` notification. The login call stays fast and doesn't fail if the SMTP server is down (failures are logged and retried up to 3 times).

### 7.4 React side
- `@microsoft/signalr` client in `src/notifications/`:
  ```ts
  const connection = new HubConnectionBuilder()
    .withUrl("/hubs/notifications", { accessTokenFactory: () => getAccessToken() })
    .withAutomaticReconnect()
    .build();
  connection.on("Notify", (n: Notification) => {
    toast[n.level](n.title, { description: n.message, duration: 5000 });
    if (n.type.startsWith("Contact")) queryClient.invalidateQueries({ queryKey: ["contacts"] });
  });
  ```
- Connects after login, stops on logout, and reconnects with the fresh token after a refresh.
- **Toasts:** sonner, top-right, each one **shows and auto-hides after 5 seconds**, with an animated progress bar showing the remaining time, pause on hover, close button, color + icon by level, and at most 3 visible at once (the rest queue). Dark-mode aware.
- Optional notification bell in the top bar listing the last 20 notifications of the session.

## 8. Frontend (React + TypeScript)

### 8.1 Tooling
Vite · React 19 · TypeScript (strict) · **Connect-ES** (`@connectrpc/connect`, `@connectrpc/connect-web`, `@bufbuild/protobuf`, codegen via `buf generate`) · **TanStack Query** (server state, caching, optimistic updates) · **React Router** (routes + protected routes) · **react-hook-form + zod** (forms) · **Tailwind CSS v4 + shadcn/ui** (components) · **lucide-react** (icons) · **sonner** (toasts, 5 s auto-hide) · **@microsoft/signalr** (notifications) · **framer-motion** (subtle animations) · Vitest + React Testing Library.

### 8.2 gRPC client layer (`src/api`)
```ts
const transport = createGrpcWebTransport({
  baseUrl: "/api",
  fetch: (input, init) => fetch(input, { ...init, credentials: "include" }),
  interceptors: [authInterceptor],   // adds Bearer token; on Code.Unauthenticated → refresh once → retry
});
export const phonebookClient = createClient(PhonebookService, transport);
export const authClient = createClient(AuthService, transport);
```
Then: `await phonebookClient.createContact({ firstName, lastName, phoneNumber })` returns a typed `Contact`. Errors arrive as `ConnectError` with a `code`, mapped to friendly toasts / inline field errors. Concurrent 401s share a single in-flight refresh.

### 8.3 Pages & UI
- **Landing page** (`/`): hero with headline + CTA ("Get started" / "Sign in"), feature cards (Fast gRPC, Secure JWT, Search & Favorites), a small "How it works" diagram of the gRPC call, footer. Gradient background, glassmorphism cards, responsive.
- **Login / Register** (`/login`, `/register`): centered card, email + password with show/hide toggle, validation messages, loading state on submit, "demo account" quick-fill button, redirect to `/contacts` after success.
- **Contacts dashboard** (`/contacts`, protected): top bar (app name, theme toggle, user menu with logout); toolbar with debounced search, favorites filter, sort dropdown, "Add contact" button; responsive list (table on desktop, cards on mobile) with avatar initials, favorite star toggle, edit/delete actions; pagination; empty state and skeleton loaders.
- **Add/Edit contact**: shared dialog/drawer form with zod validation mirroring backend rules; server `InvalidArgument` field errors shown under the matching inputs.
- **Delete**: confirm dialog, optimistic removal with undo-style toast.
- **Global**: dark/light mode (system default), accessible components (shadcn/Radix), 404 page, error boundary.
- **Optional "gRPC inspector" panel**: a dev drawer listing recent calls (method, status code, duration, payload size) so you can watch the gRPC traffic while using the app.

---

## 9. Testing strategy

**Backend: `Phonebook.UnitTests` (xUnit, **FakeItEasy** for fakes/mocks, Shouldly)**
- FakeItEasy fakes repositories, `ITokenService`, `IPasswordHasher`, `TimeProvider` and `ILogger`: `A.Fake<IContactRepository>()`, `A.CallTo(() => repo.GetByIdAsync(id, ownerId, A<CancellationToken>._)).Returns(contact)`, and `A.CallTo(...).MustHaveHappenedOnceExactly()` to verify interactions.
- `ContactService`: create/update/delete/list happy paths; not-found; cannot touch another user's contact; paging and search logic (EF Core SQLite in-memory provider).
- Validators: required fields, phone/email formats, length limits.
- `TokenService`: access token claims & expiry; refresh token hashing, rotation, reuse detection, expiry, revocation.
- `AuthService`: register duplicate email → conflict; wrong password → unauthenticated; login success.
- `ExceptionInterceptor`: each exception type → correct `StatusCode`.
- Notifications: `ContactService` publishes the right event after create/update/delete and nothing when the call fails (FakeItEasy fake `INotificationPublisher`).
- Email: `AuthService` queues a welcome email on register and a sign-in alert on login (fake `IEmailQueue`); `EmailBackgroundService` retries on SMTP failure and publishes `EmailSent` on success (fake `IEmailSender`).
- gRPC service classes, using a fake `ServerCallContext` (`TestServerCallContext`) to show how to unit-test a gRPC method directly.

**Backend: `Phonebook.IntegrationTests`**
- `WebApplicationFactory<Program>` + `GrpcChannel.ForAddress(..., HttpHandler = factory.Server.CreateHandler())` → real gRPC calls in-process.
- Scenarios: register → login → CRUD round-trip; call without token → `Unauthenticated`; expired/invalid token; refresh rotation; the same call through the **REST transcoded** endpoint returns the same data.
- SignalR: connect a `HubConnection` to the test server with a JWT, create a contact over gRPC, assert the `Notify` event arrives; connecting without a token is rejected.
- Database: SQLite in-memory per test (or Testcontainers PostgreSQL as an option).

**Frontend (Vitest + RTL + MSW-style mock transport)**
- Login form validation & submit, protected-route redirect, contact form validation, auth interceptor refresh-and-retry logic, notification handler shows a toast with a 5 s duration (mocked SignalR connection).

**Target:** `dotnet test` and `npm test` green; coverage report via `coverlet` (aim ≥ 80% on Application layer).

---

## 10. Docker

**`backend/Dockerfile`** (multi-stage): `mcr.microsoft.com/dotnet/sdk:10.0` restore/build/publish → `mcr.microsoft.com/dotnet/aspnet:10.0` runtime, non-root user, `EXPOSE 8080`.

**`frontend/Dockerfile`** (multi-stage): `node:22-alpine` → `npm ci && npm run build` → `nginx:alpine` serving `dist/`.

**`frontend/nginx.conf`**: serve SPA with `try_files ... /index.html`; proxy `/api/` → `http://api:8080/` (gRPC-Web + REST); proxy `/scalar` and `/swagger` for docs; proxy `/hubs/` with WebSocket upgrade headers (`Upgrade`, `Connection: upgrade`) for SignalR. Same origin = no CORS headaches and cookies just work.

**`docker-compose.yml`**
```yaml
services:
  db:   { image: postgres:17-alpine, env_file: .env, volumes: [pgdata:/var/lib/postgresql/data], healthcheck: pg_isready }
  api:  { build: ./backend, env_file: .env, depends_on: { db: { condition: service_healthy }, papercut: { condition: service_started } }, ports: ["8080:8080"],
          healthcheck: grpc health probe }
  papercut: { image: changemakerstudiosus/papercut-smtp:latest, ports: ["37408:80"] }   # SMTP on 25 inside the network, web inbox on the host
  web:  { build: ./frontend, depends_on: [api], ports: ["3000:80"] }
volumes: { pgdata: {} }
```
After `docker compose up --build`:
- App: http://localhost:3000
- Scalar docs: http://localhost:8080/scalar (and via http://localhost:3000/scalar)
- PaperCut inbox (all emails the app sends): http://localhost:37408 (container ports confirmed against the image's docs during implementation)
- Raw gRPC: `grpcurl -plaintext localhost:8080 list`

---

## 11. Implementation phases

| # | Phase | Output |
|---|---|---|
| 1 | Scaffold | Repo layout, solution + projects, Vite app, proto files, codegen working on both sides |
| 2 | Data | Entities, DbContext, migrations, seed data |
| 3 | Phonebook gRPC (no auth yet) | CRUD service, validation, exception interceptor, reflection; test with `grpcurl` |
| 4 | Transcoding + Scalar | REST routes, OpenAPI, Scalar UI with Bearer auth |
| 5 | Auth | Register/login/refresh/logout/me, JWT middleware, `[Authorize]`, rotation + reuse detection, rate limit |
| 6 | Backend tests | Unit + integration suites green |
| 7 | Frontend core | Transport, auth context + interceptor, routing, protected routes |
| 8 | Frontend UI | Landing, login/register, contacts dashboard, dialogs, dark mode, polish |
| 8b | Notifications & email | SignalR hub + JWT for hubs, notification publisher, email queue + PaperCut, React SignalR client + 5 s toasts |
| 9 | Frontend tests | Vitest suites green |
| 10 | Docker | Dockerfiles, nginx, compose, healthchecks, `.env.example` |
| 11 | Docs | README: run guide, architecture diagram, **gRPC walkthrough** (same call via grpcurl, Scalar/REST and the browser, with explanations) |
| 12 | Stretch | `WatchContacts` gRPC server streaming (to compare with SignalR); gRPC inspector panel; GitHub Actions CI |

---

## 12. Assumptions & open questions
- **Database:** PostgreSQL in Docker (SQLite for tests). Tell me if you'd prefer SQL Server or SQLite.
- **Contacts are per-user** (each account has its own phonebook). No admin roles in v1.
- **Contact fields:** first name, last name, phone (required), email, address, notes, favorite. Multiple phone numbers per contact can be a later change (it's a nice protobuf `repeated` exercise).
- **No photo upload** in v1.
- **Repository:** code is committed to https://github.com/csharpibocbina/PhonebookAppGRPC on a feature branch, delivered as pull requests for review.
