# BellucSketch — Desenho livre sobre plantas importadas

O montador cria um projeto, importa uma planta pronta (PDF ou imagem) e desenha livremente por cima
(traço raster) em camadas que ele mesmo cria, liga/desliga e reordena por prioridade — tipo Paint.
Consulte [TASKS.md](TASKS.md) para o backlog de implementação por fases e
[RELATORIO.md](RELATORIO.md) para o histórico detalhado de cada uma.

## Status atual

- **Fases 1 a 12 concluídas** — Domain → Application → Infrastructure → Api → Contracts → Mobile,
  hardening, deploy na nuvem (Render + Supabase), upgrade para SkiaSharp 3.x, pan/zoom por gesto +
  ferramenta de ícones técnicos, fluxo de edição colaborativa (Web solicita, Android aprova),
  ferramenta de seleção de cota com OCR (Google ML Kit) e o rebranding para **BellucSketch**. Ver
  [RELATORIO.md](RELATORIO.md) para o detalhe fase a fase e [TASKS.md](TASKS.md) para o checklist.
- **Produção**: a Api roda publicada no Render (HTTPS automático), com banco e armazenamento de
  arquivos no Supabase — ver [GUIA_DEPLOY_RENDER.md](GUIA_DEPLOY_RENDER.md). O app Android/Web
  aponta pra ela por padrão (endereço fixo, não configurável em runtime); o passo a passo de rodar
  a Api localmente abaixo é só para **desenvolvimento**.
- **Pendência conhecida**: `tests/BellucSketch.Mobile.Core.Tests/ViewModels/PlantaViewModelTests.cs`
  referencia uma propriedade renomeada (`CamadaSelecionadaParaEdicao` → `CamadaEmEdicaoId`) e quebra
  `dotnet test BellucSketch.sln` (a solução inteira) — ver RELATORIO.md, Fase 10. Os projetos
  individuais compilam e os demais testes passam isoladamente.

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`winget install Microsoft.DotNet.SDK.8` no
  Windows) — usado por Domain/Application/Infrastructure/Contracts/Api/Mobile.Core/Web.
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (`winget install Microsoft.DotNet.SDK.9`) —
  só o app `BellucSketch.Mobile` (`net9.0-android`) precisa dele; ver seção "Rodando o app Android" abaixo
  para os demais pré-requisitos específicos do Android.

## Build e testes

```powershell
dotnet build BellucSketch.sln
dotnet test BellucSketch.sln --logger "console;verbosity=normal"
```

## Estrutura

```
src/BellucSketch.Domain/                 # Entidades, value objects, enums e regras de negócio (zero dependências externas)
src/BellucSketch.Contracts/              # DTOs de request/response compartilhados por Api e Mobile (só depende do Domain)
src/BellucSketch.Application/            # Casos de uso (MediatR), portas (Abstractions/), validações (FluentValidation)
src/BellucSketch.Infrastructure/         # BellucSketchDbContext + mapeamentos EF Core, repositórios, storage, conversão PDF
src/BellucSketch.Api/                    # ASP.NET Core Web API — controllers, JWT, middleware de erros, Program.cs
src/BellucSketch.Mobile.Core/            # ViewModels, cliente HTTP e renderer SkiaSharp — testáveis sem Android (net8.0)
src/BellucSketch.Mobile/                 # App .NET MAUI Android (net9.0-android, "mestre") — Views (XAML), MauiProgram.cs, Platforms/
src/BellucSketch.Web/                    # Visualizador Blazor WebAssembly — reaproveita BellucSketch.Mobile.Core; pode propor edições de camada (aprovação fica com o Android, ver RELATORIO.md Fase 8.2)
tests/BellucSketch.Domain.Tests/         # Testes unitários de domínio (xUnit + FluentAssertions)
tests/BellucSketch.Application.Tests/    # Testes de casos de uso (xUnit + FluentAssertions + NSubstitute)
tests/BellucSketch.Infrastructure.Tests/ # Smoke tests (EF Core InMemory) + testes de repositório (Sqlite)
tests/BellucSketch.Api.Tests/            # Testes de integração ponta a ponta (WebApplicationFactory + Sqlite)
tests/BellucSketch.Mobile.Core.Tests/    # Testes de ViewModel e do renderer SkiaSharp (xUnit + NSubstitute) — ver pendência conhecida em "Status atual"
```

Veja [RELATORIO.md](RELATORIO.md) para o histórico de testes, erros e correções de cada fase.

## Rodando o app Android (BellucSketch.Mobile)

Requer o workload MAUI (net9.0) + Android SDK + JDK instalados (uma vez só, por máquina):

```powershell
dotnet workload install maui-android
winget install Microsoft.OpenJDK.17
# Baixe o Android SDK Command-line Tools em https://developer.android.com/studio#command-tools,
# extraia em <SDK_ROOT>\cmdline-tools\latest\, depois:
<SDK_ROOT>\cmdline-tools\latest\bin\sdkmanager.bat --sdk_root=<SDK_ROOT> --licenses
<SDK_ROOT>\cmdline-tools\latest\bin\sdkmanager.bat --sdk_root=<SDK_ROOT> "platform-tools" "platforms;android-35" "build-tools;35.0.0"
```

Build de desenvolvimento (Debug, só roda com deploy via cabo/Visual Studio):

```powershell
dotnet build src/BellucSketch.Mobile/BellucSketch.Mobile.csproj -f net9.0-android -p:AndroidSdkDirectory=<SDK_ROOT>
```

Build para instalar por fora (sideload, `.apk` copiado direto pro aparelho) — **precisa ser
Release**, senão o app fecha sozinho ao abrir (ver RELATORIO.md, Fase 6):

```powershell
dotnet build src/BellucSketch.Mobile/BellucSketch.Mobile.csproj -f net9.0-android -c Release -p:AndroidSdkDirectory=<SDK_ROOT>
```

O app já aponta por padrão para a Api publicada no Render — `ConfiguracaoApi.BaseUrl`
(`src/BellucSketch.Mobile.Core/Services/ConfiguracaoApi.cs`) é uma constante fixa, não algo que se
configura em runtime (isso foi removido, ver RELATORIO.md Fase 10). Só edite esse arquivo (e gere um
novo build) se quiser apontar para uma Api sua — local (`http://10.0.2.2:5000/` no emulador, IP da
máquina na rede Wi-Fi num celular físico) ou publicada em outro lugar (ver
[GUIA_DEPLOY_INTRANET.md](GUIA_DEPLOY_INTRANET.md)).

## Rodando o visualizador web (BellucSketch.Web)

Além do APK, existe um front-end Blazor WebAssembly (`src/BellucSketch.Web`) que reaproveita o mesmo
`ApiClient`, os mesmos ViewModels e o mesmo `PlantaOverlayRenderer` do `BellucSketch.Mobile.Core` — mostra
a planta com as camadas visíveis sobrepostas e permite propor mudanças em camada (visibilidade,
opacidade, bloqueio, ordem e exclusão), mas não desenha/edita o traço em si pelo navegador. Excluir
camada pela Web fica pendente de aprovação por um técnico no app Android (o "mestre") — ver
RELATORIO.md, Fase 8.2. É útil pra ver o resultado no PC sem precisar instalar o app Android.

Requer o workload `wasm-tools` (uma vez só, por máquina):

```powershell
dotnet workload install wasm-tools
```

Ajuste `ApiBaseUrl` em `src/BellucSketch.Web/wwwroot/appsettings.json` para o endereço da sua Api (mesma
regra do `ConfiguracaoApi.BaseUrl` do Mobile — IP da máquina na rede, não `localhost`, se for acessar
de outro dispositivo). Com a Api rodando (passo a passo abaixo), suba o visualizador:

```powershell
dotnet run --project src/BellucSketch.Web
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

A connection string padrão em `src/BellucSketch.Api/appsettings.json` espera usuário `camdas`, senha
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
dotnet ef database update --project src/BellucSketch.Infrastructure --startup-project src/BellucSketch.Infrastructure
```

Isso cria as tabelas (`Projetos`, `Plantas`, `Camadas`, `Usuarios`, `HistoricoAlteracoes`,
`__EFMigrationsHistory`). Para conferir:

```powershell
$env:PGPASSWORD = "camdas"
psql -U camdas -h localhost -d camdas -c "\dt"
```

#### 3.1. Gerando uma nova migration (se você alterar o modelo)

```powershell
dotnet ef migrations add NomeDaMigration --project src/BellucSketch.Infrastructure --startup-project src/BellucSketch.Infrastructure --output-dir Persistence/Migrations
dotnet ef database update --project src/BellucSketch.Infrastructure --startup-project src/BellucSketch.Infrastructure
```

Por padrão, o design-time (`BellucSketchDbContextFactory`) usa a connection string de desenvolvimento
embutida (`Host=localhost;Database=camdas;...`) — sobrescreva com a variável de ambiente
`CAMDAS_CONNECTION_STRING` se seu Postgres local tiver outras credenciais.

### 4. Subir a Api

```powershell
dotnet run --project src/BellucSketch.Api --launch-profile http
```

`Properties/launchSettings.json` já fixa a porta em `http://localhost:5080` e abre o navegador
automaticamente em `/swagger` (só funciona em modo Development, que é o padrão desse profile).

Para expor a Api pra um celular físico na mesma rede (não só o emulador), rode ligada em todas as
interfaces e libere a porta no firewall (uma vez só, como Administrador):

```powershell
dotnet run --project src/BellucSketch.Api --urls http://0.0.0.0:5080
New-NetFirewallRule -DisplayName "BellucSketch Api" -Direction Inbound -LocalPort 5080 -Protocol TCP -Action Allow
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
