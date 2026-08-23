# Multi-stage Dockerfile for the POM API.
# Build context is the REPO ROOT (see .github/workflows/ci.yml docker-build job:
#   docker build -f src/Api/Dockerfile -t pom-api:ci .  )
# so the source of all layer projects is available for `dotnet build`.

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the solution and project files first to layer the restore cache.
COPY Pom.sln Directory.Build.props ./
COPY src/Domain/POM.Domain.csproj src/Domain/
COPY src/Application/POM.Application.csproj src/Application/
COPY src/Infrastructure/POM.Infrastructure.csproj src/Infrastructure/
COPY src/Api/POM.Api.csproj src/Api/
COPY src/tests/POM.Domain.Tests/POM.Domain.Tests.csproj src/tests/POM.Domain.Tests/
COPY src/tests/POM.Infrastructure.Tests/POM.Infrastructure.Tests.csproj src/tests/POM.Infrastructure.Tests/
COPY src/tests/POM.Api.Tests/POM.Api.Tests.csproj src/tests/POM.Api.Tests/

RUN dotnet restore Pom.sln

# Copy the rest of the source and publish the API.
COPY src/ ./src/
RUN dotnet publish src/Api/POM.Api.csproj -c Release --no-restore -o /app /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Match docker-compose.yml `expose: 8080`.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "POM.Api.dll"]
