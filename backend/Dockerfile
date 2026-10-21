# Build context is the repository root (docker-compose sets it) so the shared
# /proto contract is available to Grpc.Tools during the build.

# ---- build ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached until a project file changes).
COPY backend/Directory.Build.props backend/Directory.Packages.props backend/global.json backend/
COPY backend/src/Phonebook.Domain/Phonebook.Domain.csproj backend/src/Phonebook.Domain/
COPY backend/src/Phonebook.Application/Phonebook.Application.csproj backend/src/Phonebook.Application/
COPY backend/src/Phonebook.Infrastructure/Phonebook.Infrastructure.csproj backend/src/Phonebook.Infrastructure/
COPY backend/src/Phonebook.Api/Phonebook.Api.csproj backend/src/Phonebook.Api/
RUN dotnet restore backend/src/Phonebook.Api/Phonebook.Api.csproj

COPY proto/ proto/
COPY backend/src/ backend/src/
RUN dotnet publish backend/src/Phonebook.Api/Phonebook.Api.csproj -c Release -o /app --no-restore

# ---- runtime -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# Npgsql probes for Kerberos (GSSAPI) at startup and logs an error without it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .

# 8080: HTTP/1.1 (gRPC-Web, REST/JSON, SignalR, Scalar)
# 8081: HTTP/2 cleartext (native gRPC, e.g. grpcurl)
EXPOSE 8080 8081
USER $APP_UID
ENTRYPOINT ["dotnet", "Phonebook.Api.dll"]
