# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /bitcraft

# Keep repository-wide compiler configuration active inside the container build.
COPY Directory.Build.props ./

# Copy all project files for restore
COPY src/Bitcraft.Domain/Bitcraft.Domain.csproj src/Bitcraft.Domain/
COPY src/Bitcraft.Application/Bitcraft.Application.csproj src/Bitcraft.Application/
COPY src/Bitcraft.Infrastructure/Bitcraft.Infrastructure.csproj src/Bitcraft.Infrastructure/
COPY src/Bitcraft.Api/Bitcraft.Api.csproj src/Bitcraft.Api/

RUN dotnet restore src/Bitcraft.Api/Bitcraft.Api.csproj

# Copy everything and publish
COPY src/ src/
RUN dotnet publish src/Bitcraft.Api/Bitcraft.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    libkrb5-3 \
    libgssapi-krb5-2 \
    curl && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8000
ENV ASPNETCORE_URLS=http://+:8000

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=10 \
    CMD curl -fsS http://127.0.0.1:8000/api/health || exit 1

ENTRYPOINT ["dotnet", "Bitcraft.Api.dll"]
