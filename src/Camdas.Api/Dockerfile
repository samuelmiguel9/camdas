# Build a partir da raiz do repositório (contexto = raiz do repo, não esta pasta) — precisa enxergar
# os outros projetos referenciados (Domain/Application/Infrastructure/Contracts).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Camdas.sln .
COPY src/Camdas.Domain/Camdas.Domain.csproj src/Camdas.Domain/
COPY src/Camdas.Contracts/Camdas.Contracts.csproj src/Camdas.Contracts/
COPY src/Camdas.Application/Camdas.Application.csproj src/Camdas.Application/
COPY src/Camdas.Infrastructure/Camdas.Infrastructure.csproj src/Camdas.Infrastructure/
COPY src/Camdas.Api/Camdas.Api.csproj src/Camdas.Api/
RUN dotnet restore src/Camdas.Api/Camdas.Api.csproj

COPY src/Camdas.Domain/ src/Camdas.Domain/
COPY src/Camdas.Contracts/ src/Camdas.Contracts/
COPY src/Camdas.Application/ src/Camdas.Application/
COPY src/Camdas.Infrastructure/ src/Camdas.Infrastructure/
COPY src/Camdas.Api/ src/Camdas.Api/

RUN dotnet publish src/Camdas.Api/Camdas.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# Render injeta a variável PORT — o Kestrel precisa escutar exatamente nela (não numa porta fixa).
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet Camdas.Api.dll"]
