# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /bitcraft

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

COPY --from=build /app/publish .

EXPOSE 8000
ENV ASPNETCORE_URLS=http://+:8000

ENTRYPOINT ["dotnet", "Bitcraft.Api.dll"]
