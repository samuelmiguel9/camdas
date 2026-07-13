# Camdas — Desenho livre sobre plantas importadas

Consulte [PRD.md](PRD.md) para arquitetura, modelo de dados e stack tecnológica, e
[TASKS.md](TASKS.md) para o backlog de implementação por fases.

> **Pivot**: o projeto começou como uma ferramenta formal de ratificação de plantas (cotas
> estruturadas + fluxo de revisão/aprovação por perfil). Isso foi removido por completo — o app
> hoje é mais simples: o montador cria um projeto, importa uma planta pronta (PDF ou imagem) e
> desenha livremente por cima (traço raster) em camadas que ele mesmo cria, liga/desliga e
> reordena por prioridade — tipo Paint. Detalhes em TASKS.md e RELATORIO.md.

## Status atual

- **Fases 1 a 6 concluídas** (Domain → Application → Infrastructure → Api → Contracts → Mobile),
  com o fluxo de revisão/aprovação/versionamento e a entidade `Cota` (medida estruturada)
  completamente removidos — nada na UI usava esse recurso.
- **Fase 7 concluída**: limpeza de código morto, correções de UI reportadas em teste no aparelho
  (botão de arrastar, planta cortada na visualização geral, escala da borracha), miniatura da
  planta na lista do projeto e relatório de atualizações em PDF (botão no canto superior da aba
  Projetos). Ver [RELATORIO.md](RELATORIO.md) para o detalhe de cada mudança.

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`winget install Microsoft.DotNet.SDK.8` no
  Windows).

## Build e testes

```powershell
dotnet build Camdas.sln
dotnet test Camdas.sln --logger "console;verbosity=normal"
```

## Estrutura

```
src/Camdas.Domain/                 # Entidades, value objects, enums e regras de negócio (zero dependências externas)
src/Camdas.Contracts/              # DTOs de request/response compartilhados por Api e Mobile (só depende do Domain)
src/Camdas.Application/            # Casos de uso (MediatR), portas (Abstractions/), validações (FluentValidation)
src/Camdas.Infrastructure/         # CamdasDbContext + mapeamentos EF Core, repositórios, storage, conversão PDF
src/Camdas.Api/                    # ASP.NET Core Web API — controllers, JWT, middleware de erros, Program.cs
src/Camdas.Mobile.Core/            # ViewModels, cliente HTTP, renderer SkiaSharp e geração do relatório PDF — testáveis sem Android (net8.0)
src/Camdas.Mobile/                 # App .NET MAUI Android — Views (XAML), MauiProgram.cs, Platforms/
src/Camdas.Web/                    # Visualizador Blazor WebAssembly (somente leitura) — reaproveita Camdas.Mobile.Core
tests/Camdas.Domain.Tests/         # Testes unitários de domínio (xUnit + FluentAssertions)
tests/Camdas.Application.Tests/    # Testes de casos de uso (xUnit + FluentAssertions + NSubstitute)
tests/Camdas.Infrastructure.Tests/ # Smoke tests (EF Core InMemory) + testes de repositório (Sqlite)
tests/Camdas.Api.Tests/            # Testes de integração ponta a ponta (WebApplicationFactory + Sqlite)
tests/Camdas.Mobile.Core.Tests/    # Testes de ViewModel, do renderer SkiaSharp e do relatório PDF (xUnit + NSubstitute)
```

Veja [RELATORIO.md](RELATORIO.md) para o histórico de testes, erros e correções de cada fase.

## Rodando o app Android (Camdas.Mobile)

Requer o workload MAUI + Android SDK + JDK instalados (uma vez só, por máquina):

```powershell
dotnet workload install maui-android
winget install Microsoft.OpenJDK.17
# Baixe o Android SDK Command-line Tools em https://developer.android.com/studio#command-tools,
# extraia em <SDK_ROOT>\cmdline-tools\latest\, depois:
<SDK_ROOT>\cmdline-tools\latest\bin\sdkmanager.bat --sdk_root=<SDK_ROOT> --licenses
<SDK_ROOT>\cmdline-tools\latest\bin\sdkmanager.bat --sdk_root=<SDK_ROOT> "platform-tools" "platforms;android-34" "build-tools;34.0.0"
```

Build de desenvolvimento (Debug, só roda com deploy via cabo/Visual Studio):

```powershell
dotnet build src/Camdas.Mobile/Camdas.Mobile.csproj -f net8.0-android -p:AndroidSdkDirectory=<SDK_ROOT>
```

Build para instalar por fora (sideload, `.apk` copiado direto pro aparelho) — **precisa ser
Release**, senão o app fecha sozinho ao abrir (ver RELATORIO.md, Fase 6):

```powershell
dotnet build src/Camdas.Mobile/Camdas.Mobile.csproj -f net8.0-android -c Release -p:AndroidSdkDirectory=<SDK_ROOT>
```

Antes de instalar num dispositivo/emulador de verdade, ajuste `ConfiguracaoApi.BaseUrl`
(`src/Camdas.Mobile.Core/Services/ConfiguracaoApi.cs`) para o endereço real da Api na intranet — o
padrão (`http://10.0.2.2:5000/`) só funciona no emulador Android apontando para o `localhost` da
máquina de desenvolvimento; num celular físico, use o IP da máquina na rede Wi-Fi.

## Rodando o visualizador web (Camdas.Web)

Além do APK, existe um front-end Blazor WebAssembly (`src/Camdas.Web`) que reaproveita o mesmo
`ApiClient`, os mesmos ViewModels e o mesmo `PlantaOverlayRenderer` do `Camdas.Mobile.Core` — ele só
mostra a planta com as camadas visíveis sobrepostas (somente leitura, não desenha/edita traço pelo
navegador). É útil pra ver o resultado no PC sem precisar instalar o app Android.

Requer o workload `wasm-tools` (uma vez só, por máquina):

```powershell
dotnet workload install wasm-tools
```

Ajuste `ApiBaseUrl` em `src/Camdas.Web/wwwroot/appsettings.json` para o endereço da sua Api (mesma
regra do `ConfiguracaoApi.BaseUrl` do Mobile — IP da máquina na rede, não `localhost`, se for acessar
de outro dispositivo). Com a Api rodando (passo a passo abaixo), suba o visualizador:

```powershell
dotnet run --project src/Camdas.Web
```

Abre em `http://localhost:5150` (ou na porta que o Kestrel escolher). Faça login com o mesmo Id de
usuário (Guid) usado no Swagger, navegue até Projetos → planta desejada.

## Rodando a Api localmente — passo a passo completo

Isso parte do zero (nenhum PostgreSQL instalado ainda). Se você já tem um Postgres rodando, pule
direto para o passo 3.

### 1. Instalar o PostgreSQL

```powershell
winget install PostgreSQL.PostgreSQL.17 --silent --accept-package-agreements --accept-source-agreements
```

O instalador da EDB (o mesmo que o winget baixa) sobe o serviço automaticamente ao final, com um
superusuário `postgres` — **a senha padrão desse instalador silencioso é `postgres`**. O binário
`psql` fica em `C:\Program Files\PostgreSQL\17\bin`.

### 2. Criar o usuário e o banco `camdas`

A connection string padrão em `src/Camdas.Api/appsettings.json` espera usuário `camdas`, senha
`camdas`, banco `camdas`:

```powershell
$env:PATH = "C:\Program Files\PostgreSQL\17\bin;$env:PATH"
$env:PGPASSWORD = "postgres"
psql -U postgres -h localhost -c "CREATE USER camdas WITH PASSWORD 'camdas' CREATEDB;"
psql -U postgres -h localhost -c "CREATE DATABASE camdas OWNER camdas;"
```

Se o seu Postgres já existente usa outro usuário/senha, ajuste a string de conexão (passo 3.1)
em vez de tentar recriar `camdas`.

### 3. Aplicar as migrations

```powershell
dotnet tool install --global dotnet-ef --version 8.0.10
$env:CAMDAS_CONNECTION_STRING = "Host=localhost;Database=camdas;Username=camdas;Password=camdas"
dotnet ef database update --project src/Camdas.Infrastructure --startup-project src/Camdas.Infrastructure
```

Isso cria as tabelas (`Projetos`, `Plantas`, `Camadas`, `Usuarios`, `HistoricoAlteracoes`,
`__EFMigrationsHistory`). Para conferir:

```powershell
$env:PGPASSWORD = "camdas"
psql -U camdas -h localhost -d camdas -c "\dt"
```

#### 3.1. Gerando uma nova migration (se você alterar o modelo)

```powershell
dotnet ef migrations add NomeDaMigration --project src/Camdas.Infrastructure --startup-project src/Camdas.Infrastructure --output-dir Persistence/Migrations
dotnet ef database update --project src/Camdas.Infrastructure --startup-project src/Camdas.Infrastructure
```

Por padrão, o design-time (`CamdasDbContextFactory`) usa a connection string de desenvolvimento
embutida (`Host=localhost;Database=camdas;...`) — sobrescreva com a variável de ambiente
`CAMDAS_CONNECTION_STRING` se seu Postgres local tiver outras credenciais.

### 4. Subir a Api

```powershell
dotnet run --project src/Camdas.Api --launch-profile http
```

`Properties/launchSettings.json` já fixa a porta em `http://localhost:5080` e abre o navegador
automaticamente em `/swagger` (só funciona em modo Development, que é o padrão desse profile).

Para expor a Api pra um celular físico na mesma rede (não só o emulador), rode ligada em todas as
interfaces e libere a porta no firewall (uma vez só, como Administrador):

```powershell
dotnet run --project src/Camdas.Api --urls http://0.0.0.0:5080
New-NetFirewallRule -DisplayName "Camdas Api" -Direction Inbound -LocalPort 5080 -Protocol TCP -Action Allow
```

### 5. Testar o fluxo pelo Swagger

O Swagger abre em **http://localhost:5080/swagger/index.html**. Como ainda não existe login por
credencial, o caminho é:

1. Crie um usuário direto no banco (ainda não há endpoint de cadastro de usuário):
   ```powershell
   $env:PGPASSWORD = "camdas"
   $id = [guid]::NewGuid().ToString()
   psql -U camdas -h localhost -d camdas -c "INSERT INTO ""Usuarios"" (""Id"", ""Nome"", ""Email"", ""Ativo"") VALUES ('$id', 'Ana Montadora', 'ana@empresa.com', true);"
   Write-Output $id
   ```
2. No Swagger, abra **`POST /api/auth/dev-token`** → "Try it out" → cole o `usuarioId` gerado acima
   → Execute. A resposta traz um `token` (JWT).
3. Clique no botão **Authorize** (cadeado, no topo da página) → cole `Bearer <token>` → Authorize.
4. Agora dá pra chamar os demais endpoints autenticado: `POST /api/projetos` (criar projeto),
   `POST /api/plantas` (importar planta — aceita PDF ou imagem via multipart/form-data, com nome,
   descrição e nome do cliente opcionais), `GET /api/plantas/{id}/arquivo` (baixar a imagem da
   planta importada — é o que o app desenha como fundo), `POST /api/plantas/{id}/camadas` (criar
   camada) e `PUT`/`GET /api/plantas/{id}/camadas/{id}/imagem` (salvar/baixar o traço livre — PNG
   com transparência — daquela camada).
