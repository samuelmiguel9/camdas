# Build a partir da raiz do repositório (contexto = raiz do repo, não esta pasta) — precisa enxergar
# os outros projetos referenciados (Domain/Application/Infrastructure/Contracts).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BellucSketch.sln .
COPY VERSION .
COPY src/BellucSketch.Domain/BellucSketch.Domain.csproj src/BellucSketch.Domain/
COPY src/BellucSketch.Contracts/BellucSketch.Contracts.csproj src/BellucSketch.Contracts/
COPY src/BellucSketch.Application/BellucSketch.Application.csproj src/BellucSketch.Application/
COPY src/BellucSketch.Infrastructure/BellucSketch.Infrastructure.csproj src/BellucSketch.Infrastructure/
COPY src/BellucSketch.Api/BellucSketch.Api.csproj src/BellucSketch.Api/
RUN dotnet restore src/BellucSketch.Api/BellucSketch.Api.csproj

COPY src/BellucSketch.Domain/ src/BellucSketch.Domain/
COPY src/BellucSketch.Contracts/ src/BellucSketch.Contracts/
COPY src/BellucSketch.Application/ src/BellucSketch.Application/
COPY src/BellucSketch.Infrastructure/ src/BellucSketch.Infrastructure/
COPY src/BellucSketch.Api/ src/BellucSketch.Api/

RUN dotnet publish src/BellucSketch.Api/BellucSketch.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# Render injeta a variável PORT — o Kestrel precisa escutar exatamente nela (não numa porta fixa).
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet BellucSketch.Api.dll"]
