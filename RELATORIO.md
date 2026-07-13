# Relatório de Execução — Camdas

Este documento acompanha, fase a fase, o que foi implementado, quais testes existem e o que cada um
verifica, e quais erros reais apareceram no caminho (com a causa raiz e a correção). É atualizado
sempre que uma fase é concluída ou alterada. Para a visão de arquitetura/modelo de dados, veja
[PRD.md](PRD.md); para o backlog checklist, veja [TASKS.md](TASKS.md).

**Estado atual: Fases 1 a 8 concluídas e verificadas (62/62 testes passando + build Release do APK
Android instalado e testado num aparelho físico). A entidade `Cota` (medida estruturada) e todo o
fluxo de revisão/aprovação/versionamento que existiam nas primeiras versões do projeto foram
removidos por completo na Fase 8 — não sobrou nenhum uso deles na UI atual. Fase 7 (hardening/
deploy) segue com os itens de guia de deploy/instalação em aberto.**

---

## Visão geral do placar de testes

| Projeto de teste | O que cobre | Quantidade | Status |
|---|---|---|---|
| `Camdas.Domain.Tests` | Entidades e regras de negócio puras | 20 | ✅ |
| `Camdas.Application.Tests` | Casos de uso (handlers MediatR) com mocks | 6 | ✅ |
| `Camdas.Mobile.Core.Tests` | ViewModels do app Android, renderer SkiaSharp e geração do relatório PDF | 22 | ✅ |
| `Camdas.Infrastructure.Tests` | Mapeamento EF Core + repositórios | 7 | ✅ |
| `Camdas.Api.Tests` | Integração ponta a ponta via HTTP | 7 | ✅ |
| **Total** | | **62** | ✅ |

Além dos testes automatizados, a Fase 6 também foi verificada por um **build Release real do APK
Android** (`com.companyname.camdas.mobile-Signed.apk`, ~49 MB, 0 erros), instalado via `adb install`
num Samsung Galaxy A15 físico e testado de ponta a ponta (login → lista de projetos → desenhar sobre
uma camada).

Rodar tudo: `dotnet test Camdas.sln --logger "console;verbosity=normal"`.

---

## Fase 1 — `Camdas.Domain`

**O que foi construído.** O núcleo do domínio, sem nenhuma dependência externa (nem EF Core, nem
JSON, nem nada): agregado raiz `Planta` (orquestra `Camada` — construtores/mutadores `internal`, só
acessíveis através da própria `Planta`, para não permitir que ninguém de fora burle as
invariantes), entidades `Usuario` e `Projeto`, e a entidade independente `HistoricoAlteracao`.
`Camada` guarda `ImagemRasterCaminho` (o traço livre/bitmap desenhado por cima da planta) e sua
ordem de prioridade entre as demais camadas.

**Testes.** Cobrem, entidade por entidade:
- `PlantaTests`: camadas são criadas livremente pelo usuário (sem padrão fixo); reordenar renumera
  em ordem crescente; camada bloqueada não aceita atualizar a imagem raster; remoção de camada.
- `UsuarioTests`, `ProjetoTests`, `HistoricoAlteracaoTests`: validações de construção e as regras de
  cada entidade isoladamente.

**Nota histórica (versão anterior do projeto).** Nas primeiras versões, o domínio também incluía
uma entidade `Cota` (medida estruturada ponto-a-ponto, com value objects `Ponto2D`/`Medida`) e um
fluxo completo de revisão/aprovação/versionamento de planta. Nenhum dos dois chegou a ser usado
pela UI do app — o produto final é mais simples (desenho livre por cima da planta importada) — e
ambos foram removidos por completo na Fase 8, junto com seus testes.

---

## Fase 2 — `Camdas.Application`

**O que foi construído.** Casos de uso em MediatR (`IRequest`/`IRequestHandler`), um arquivo por
caso de uso reunindo Command/Query + Validator (FluentValidation) + Handler ("vertical slice"):
Projetos (criar/listar/obter/renomear/remover), Plantas (importar/obter/listar por projeto/obter
arquivo), Camadas (criar/reordenar/alternar visibilidade/bloquear/desbloquear/atualizar e obter
imagem raster) e Histórico. As portas (`Abstractions/`) definem o que a Infrastructure precisará
implementar depois: `IProjetoRepository`, `IPlantaRepository`, `IUsuarioRepository`,
`IHistoricoRepository`, `IUnitOfWork`, `IArquivoStorage`, `IUsuarioContext`, `IClock`.

**Ajuste de design feito durante a implementação.** O plano original prealocava
`ICamadaRepository`. Isso foi corrigido ao perceber que viola a regra de DDD "um repositório por
agregado raiz" — Camada só existe dentro do agregado `Planta`, então só `Planta` (e `Projeto`) têm
repositório próprio.

**Testes (com NSubstitute).** Não usam banco nem HTTP — mockam as portas e verificam:
- `CriarProjetoCommandHandlerTests`: projeto criado é associado ao usuário autenticado.
- `ImportarPlantaCommandHandlerTests`: importação normal grava histórico; falha com
  `RecursoNaoEncontradoException` se o projeto não existe; um PDF é convertido para imagem antes de
  salvar (via `IConversorPdfParaImagem`).
- `AtualizarImagemCamadaCommandHandlerTests`: salva o arquivo via `IArquivoStorage`, atualiza a
  camada e grava histórico; propaga `DomainException` se a camada está bloqueada.

Nenhum bug de código foi encontrado nesta fase — os testes passaram de primeira.

---

## Fase 3 — `Camdas.Infrastructure`

**O que foi construído.** `CamdasDbContext` (EF Core) com mapeamento Fluent API completo: a coleção
do agregado (`Planta.Camadas`) mapeada via backing field, já que a propriedade pública só expõe um
wrapper somente-leitura; enums sempre como `string` no banco. Provider escolhido:
**PostgreSQL**. Depois: migration inicial, repositórios concretos (`PlantaRepositoryEfCore` sempre
carrega o agregado completo via `Include`), `ArquivoStorageEmDisco` (disco local ou caminho de rede,
com `AbrirAsync` pra servir o arquivo de volta, validando que o caminho fica dentro da raiz de
armazenamento), e `ConversorPdfParaImagemPdfium` (PDFium via pacote `PDFtoImage`) para transformar a
1ª página de um PDF importado em PNG.

**Testes.**
- Smoke tests com o provider **EF Core InMemory**: provam que o mapeamento "fecha" (roda sem
  exceção de configuração) e faz round-trip de Planta+Camadas, Usuario/Projeto e
  HistoricoAlteracao.
- Testes de repositório com **Sqlite** (motor relacional real, sem precisar de Docker/Postgres
  rodando): `ProjetoRepositoryEfCore`, `PlantaRepositoryEfCore` (salva e recarrega o agregado
  completo em um `DbContext` novo, simulando uma unidade de trabalho separada),
  `HistoricoRepositoryEfCore`, `UsuarioRepositoryEfCore`.

**Bugs reais encontrados e corrigidos (achados pelos próprios testes de repositório, não pelos de
InMemory — esse é o motivo de ter dois níveis de teste aqui).**
1. **EF Core escolhe o construtor errado ao materializar? Não — checado antes de codar.** Antes de
   mapear, pesquisei (`WebSearch`) e confirmei: o EF Core prioriza o construtor com **menos**
   parâmetros na hora de reidratar uma entidade do banco. Como toda entidade já tinha um construtor
   privado sem parâmetros (comentado "`// EF Core`"), isso já estava certo sem precisar de nenhuma
   configuração extra — mas só ficaria confirmado com o teste de repositório rodando de verdade.
2. **`DbUpdateConcurrencyException` ao adicionar uma nova Cota numa Camada já existente.** Ao salvar
   uma `Planta` com uma `Cota` nova dentro de uma `Camada` que já tinha sido persistida antes, o EF
   Core tentava um `UPDATE` na tabela `Cotas` que afetava 0 linhas. Duas causas relacionadas, as duas
   corrigidas:
   - `Ponto2D`/`Medida` (owned types) só tinham construtor parametrizado — sem um construtor vazio,
     o EF não montava corretamente o snapshot de "valores originais" desses VOs ao ler via `Include`,
     e isso disparava um `UPDATE` espúrio. Corrigido adicionando construtor privado sem parâmetros a
     ambos (mesmo padrão das entidades).
   - Separadamente, por convenção, uma chave `Guid` "não vazia" é tratada pelo EF Core como
     possivelmente já existente no banco (`ValueGeneratedOnAdd`), o que pode fazer uma entidade filha
     recém-criada (mas com Id já atribuído pelo próprio `Entity.Id` no construtor) ser marcada como
     `Modified` em vez de `Added`. Corrigido configurando `ValueGenerated.Never` para a propriedade
     `Id` de **toda** entidade, globalmente, em `CamdasDbContext.OnModelCreating` — nossos Ids nunca
     são gerados pelo banco.

---

## Fase 4 — `Camdas.Api`

**O que foi construído.** ASP.NET Core Web API: `Program.cs` compõe toda a injeção de dependência
(Application + Infrastructure), autenticação JWT Bearer simples (`[Authorize]`, sem perfis/roles —
qualquer usuário autenticado pode usar qualquer endpoint), middleware global de erros
(`TratadorDeExcecoesGlobal : IExceptionHandler`, mapeia `DomainException` → 400,
`RecursoNaoEncontradoException` → 404, erro de validação → 400 com detalhe por campo), controllers
para cada agregado/caso de uso (projetos, plantas — incluindo importação e servir os bytes da
imagem — e camadas, incluindo criar/reordenar e receber/servir o traço raster), e Swagger com
suporte a Bearer token.

**Pendência sinalizada de propósito.** Não existe login por credencial ainda — `Usuario` não tem
campo de senha no domínio. `POST /api/auth/dev-token` emite um token para qualquer `UsuarioId`
existente **sem verificar senha**, documentado no código como placeholder de desenvolvimento/teste,
"não usar em produção". Implementar login real (hash de senha) é um próximo passo, ainda não feito.

**Testes (via `WebApplicationFactory` + Sqlite — sobem a Api real, contra HTTP).**
1. Fluxo completo: login (dev-token) → criar projeto → importar planta → `GET .../arquivo` devolve
   exatamente os bytes salvos → criar camada → `PUT .../imagem` de camada → `GET .../imagem`
   devolve exatamente os mesmos bytes enviados → histórico reflete a timeline na ordem certa.
2. Camada sem imagem ainda → `GET .../imagem` retorna 404.
3. Projeto inexistente → 404.
4. Requisição sem token → 401.

**Esta foi a fase com mais bugs reais encontrados — todos só apareceram rodando os testes de
integração de verdade (HTTP + JWT), nenhum teria aparecido num teste unitário isolado:**

1. **JWT: "signature key was not found" mesmo com token recém-emitido pelo próprio servidor.**
   `TokenValidationParameters` era montado lendo `builder.Configuration` diretamente e cedo demais em
   `Program.cs` (antes do host terminar de montar), enquanto `IJwtTokenGenerator` lia a mesma
   configuração tardiamente via `IOptions<JwtOptions>` (resolvido pelo DI só na primeira
   requisição) — os dois podiam ver snapshots de configuração diferentes. Corrigido com o padrão
   oficial "options que dependem de outro serviço":
   `services.AddOptions<JwtBearerOptions>(...).Configure<IOptions<JwtOptions>>(...)`, fazendo os dois
   lados lerem exatamente a mesma config, no mesmo momento.
2. **JWT: claim "sub" sumindo, gerando 401 mesmo com token válido.** Por padrão, o
   `JwtBearerHandler` do .NET remapeia claims curtas do JWT (`sub`) para as URIs longas de
   `ClaimTypes` (`ClaimTypes.NameIdentifier`) — um comportamento antigo, não óbvio, herdado do
   WS-Federation. `UsuarioContextHttp` procurava literalmente por `"sub"` e nunca achava, lançando
   "usuário não autenticado" mesmo com o token certo. Corrigido com
   `bearerOptions.MapInboundClaims = false;`.
3. **Enum como string no corpo JSON dava 400.** `[FromForm]` (usado na importação de planta) aceita
   enum por nome nativamente, mas o `System.Text.Json` usado no `[FromBody]` não converte
   string → enum sem um conversor explícito — o valor do enum falhava a desserialização. Corrigido
   registrando `JsonStringEnumConverter` em `AddControllers().AddJsonOptions(...)`. Isso também
   exigiu registrar o mesmo conversor no `JsonSerializerOptions` usado pelos testes, para poder ler
   de volta a resposta da Api.

Cada um desses bugs foi diagnosticado empiricamente: capturando o cabeçalho `WWW-Authenticate` da
resposta (que o `JwtBearerHandler` preenche com o motivo exato da rejeição) e, quando isso não bastou,
usando o próprio endpoint de emissão de token do servidor em vez de gerar o token "à mão" no teste —
isso eliminou a hipótese de divergência entre config do teste e config do servidor e apontou direto
para o bug real.

### Addendum — rodando a Api de verdade (fora dos testes)

Os testes de integração da Fase 4 usam `WebApplicationFactory` + Sqlite, então nunca precisaram de um
PostgreSQL real nem de um jeito de "só rodar" a Api fora do xUnit. Ao fazer isso pela primeira vez,
apareceram duas lacunas que os testes não cobriam:

- **Faltava `Properties/launchSettings.json`.** O projeto `Camdas.Api` nunca tinha sido rodado com
  `dotnet run` fora dos testes — só existia porque foi escrito à mão (não veio de
  `dotnet new webapi`), então esse arquivo nunca foi gerado. Sem ele, `dotnet run` sobe em portas
  Kestrel padrão sem abrir o Swagger automaticamente. Criado fixando a porta em `5080`,
  `ASPNETCORE_ENVIRONMENT=Development` e `launchBrowser`/`launchUrl` apontando para `/swagger`.
- **Nenhum PostgreSQL instalado.** Resolvido instalando `PostgreSQL.PostgreSQL.17` via winget
  (instalador silencioso, usuário `postgres`/senha padrão `postgres`), criando o usuário/banco
  `camdas` esperado pela connection string padrão, e aplicando as migrations — as tabelas foram
  conferidas via `psql \dt`.
- **Servir a Api pra fora do `localhost` (para o app Mobile num celular físico na mesma rede)**
  exige `dotnet run --urls http://0.0.0.0:<porta>` (Kestrel só aceita conexões externas se estiver
  ligado em `0.0.0.0`/todas as interfaces, não só `localhost`) **e** uma regra de firewall liberando
  a porta de entrada (`New-NetFirewallRule ... -Direction Inbound -LocalPort <porta> -Action Allow`,
  precisa de PowerShell como Administrador).

Com isso, o fluxo completo foi validado manualmente pelo Swagger em
`http://localhost:5080/swagger`, além dos testes automatizados. O passo a passo fica documentado no
[README.md](README.md).

---

## Fase 5 — `Camdas.Contracts`

**O que foi construído.** Um projeto novo, dependendo só de `Camdas.Domain`, para ser a "linguagem
comum" entre a Api e o app Mobile sem que o Mobile precise referenciar MediatR/FluentValidation/
EF Core. Isso motivou um refactor: os DTOs de resposta (`ProjetoDto`, `PlantaDto`, `CamadaDto`,
`HistoricoDto`) e o mapeador `Mapeamentos` (extension methods `ParaDto()`) que viviam dentro de
`Camdas.Application` foram movidos para `Camdas.Contracts` (`Mapeamentos` passou de `internal` para
`public`, já que agora é consumido entre assemblies). Os records de request que estavam soltos
dentro dos controllers da Api também foram movidos para lá, e um novo `CriarProjetoRequest` foi
criado para o controller parar de vincular o corpo da requisição HTTP diretamente ao
`CriarProjetoCommand` interno da Application.

**Testes.** Nenhum teste novo (é um projeto de DTOs puros) — mas os testes existentes das fases
anteriores foram todos re-executados depois do refactor e continuaram passando de primeira, o que dá
confiança de que a extração de tipos entre assemblies foi feita sem quebrar nada.

**Bugs.** Nenhum — o build já pegou os poucos `using` que ficaram desatualizados antes mesmo de
rodar qualquer teste.

---

## Fase 6 — `Camdas.Mobile` (.NET MAUI / Android)

**O que foi construído.** O app Android acabou virando **dois projetos**, não um — decisão tomada
assim que percebi que um projeto MAUI (`net8.0-android`, `OutputType=Exe`) não pode ser referenciado
por um projeto de teste `net8.0` comum (incompatibilidade de TFM para esse sentido de referência).
Para que ViewModels e lógica de desenho fossem testáveis sem precisar de emulador Android, ficou:

- **`Camdas.Mobile.Core`** (`net8.0`, biblioteca "plain", zero dependência de MAUI): ViewModels
  (`LoginViewModel`, `ProjetosViewModel`, `PlantasDoProjetoViewModel`, `PlantaViewModel`,
  `CamadaEdicaoViewModel`, `HistoricoViewModel`, todos com CommunityToolkit.Mvvm),
  `IApiClient`/`ApiClient` (cliente HTTP tipado consumindo `Camdas.Contracts`), `ITokenStore` (só a
  interface), `TokenAuthHandler` (anexa o Bearer token em toda requisição), `PlantaOverlayRenderer`
  (desenha a imagem base da planta + o traço raster de cada camada visível, com SkiaSharp puro, sem
  depender de Android) e `Relatorios/` (changelog versionado + `RelatorioPdfService`, geração do PDF
  com QuestPDF).
- **`Camdas.Mobile`** (`net8.0-android`, escafoldado com `dotnet new maui`): as Views (XAML) +
  code-behind, `PlantaCanvasView` (conecta o `PlantaOverlayRenderer` do Core a um `SKCanvasView` e
  adiciona o toque: desenhar/apagar na camada ativa), conversores XAML, `MauiProgram.cs`
  (composição de DI), `AppShell` (navegação em pilha) e `TokenStoreSecureStorage` (única peça que
  precisava mesmo estar aqui, por depender do `SecureStorage` do MAUI Essentials).

Fluxo de telas: Login (por Id de usuário, replicando o `dev-token` da Api) → Projetos (criar/editar/
excluir, botão "Relatório" no canto superior) → Plantas do projeto (com importação de PDF/imagem
via `FilePicker`, nome/descrição/cliente opcionais, miniatura da planta composta) → Planta (canvas
com a imagem da planta + traço livre de cada camada visível, toggle visível/bloqueada, botões ▲/▼
de prioridade) → edição isolada de uma camada por vez (toolbar de desenho: cor, espessura, apagar,
limpar, salvar) → Histórico (linha do tempo), acessível a partir da tela da Planta.

**Achado real durante o desenho do fluxo (não um bug de código, uma lacuna de escopo).** Ao desenhar
a tela "Plantas do projeto", percebi que a Api **não tinha** um endpoint para listar as plantas de um
projeto (só existia importar e obter por Id). Adicionado `GET /api/projetos/{projetoId}/plantas`
(novo `ListarPlantasPorProjetoQuery` na Application, reaproveitando
`IPlantaRepository.ListarPorProjetoAsync`, que já existia desde a Fase 2 mas nunca tinha sido
exposto), com um teste de integração cobrindo.

**Testes (em `Camdas.Mobile.Core.Tests`, sem nenhuma dependência de Android/emulador).**
- ViewModels: caminho feliz de cada tela + casos de erro específicos (login com Id inválido não
  chama a Api; criar projeto com nome vazio não chama a Api; salvar desenho sem nada desenhado
  mostra mensagem em vez de chamar a Api) — todos usando NSubstitute para mockar `IApiClient`.
- `PlantaOverlayRendererTests`: renderiza um `SKBitmap` de verdade e confere os **pixels** — imagem
  base desenhada primeiro; traço raster de camada visível aparece por cima; traço de camada oculta
  não aparece. Isso só é possível porque `PlantaOverlayRenderer` foi mantido puro (SkiaSharp
  funciona em qualquer host .NET, não só em Android).
- `RelatorioPdfServiceTests` (adicionado na Fase 8): o PDF gerado começa com a assinatura de arquivo
  `%PDF-` e não é vazio; o changelog começa pela versão 1.0.

### Simplificações conscientes (não são bugs)
Sem undo por traço (só "Limpar camada" inteira). Sem zoom/pan no canvas — o bitmap de cada camada é
normalizado pro tamanho atual do canvas na primeira vez que é tocado. O login continua sendo o
mecanismo provisório `dev-token` da Fase 4.

**Atrito de ambiente/infraestrutura (distinto de bugs de código) — vale registrar porque não é óbvio
e provavelmente vai se repetir em outra máquina limpa:**

1. **Faltava o toolchain Android inteiro.** `dotnet workload install maui-android` instala os
   pacotes .NET (bindings, runtime), mas **não** instala o Android SDK (adb, build-tools,
   plataformas) nem um JDK — normalmente o Visual Studio instala isso por trás das cortinas. Resolvido
   manualmente: `winget install Microsoft.OpenJDK.17`, download do Android Command-line Tools
   (`commandlinetools-win-*.zip` do repositório oficial do Google), `sdkmanager --licenses` (aceite
   automatizado via stdin), e `sdkmanager "platform-tools" "platforms;android-34" "build-tools;34.0.0"`.
   O primeiro `dotnet build` já funcionou apontando `-p:AndroidSdkDirectory=<caminho>`.
2. **`XARDF7024: Access to the path ... is denied`** num arquivo dentro de `obj/.../assets/`. Causa:
   o repositório está dentro de uma pasta sincronizada pelo OneDrive, e um build Android gera/mexe em
   centenas de arquivos pequenos rapidamente — o OneDrive prendeu um lock passageiro em um deles no
   meio do processo. Resolvido limpando `obj/`/`bin/` e rodando de novo (o lock não se repetiu); se
   voltar a acontecer em outra máquina, mover a saída de build (`BaseIntermediateOutputPath`) para
   fora da pasta sincronizada resolve de forma permanente.
3. **Tráfego HTTP bloqueado por padrão no Android (API 28+).** A Api roda em HTTP simples na
   intranet (HTTPS ainda não foi configurado). A partir da API 28, o Android bloqueia
   "cleartext traffic" por padrão — sem correção, **toda chamada da Api falharia silenciosamente** em
   qualquer dispositivo/emulador real, mesmo com o app funcionando perfeitamente em teoria. Corrigido
   com `android:usesCleartextTraffic="true"` no `AndroidManifest.xml`, com uma nota de que o caminho
   correto em produção é publicar a Api com HTTPS (CA interna) e remover essa permissão depois.

**Bugs de XAML pegos por autorrevisão antes mesmo de compilar (o compilador MAUI não acusa esse tipo
de erro — ele só vira comportamento errado em runtime):**

4. `IsVisible` bindado direto contra um objeto usando um conversor feito para `bool`
   (`InvertedBoolConverter`) — o binding compila sem erro, mas o resultado em tela estaria sempre
   errado. Corrigido com um conversor dedicado (`ObjetoNuloParaBoolConverter`, com
   `ConverterParameter="inverso"` para o caso contrário).
5. `StringFormat="{0:Visível;Oculta}"` para formatar um `bool` — essa sintaxe com `;` só existe para
   formatação condicional de **números** (seções positivo/negativo/zero); para `bool`, o MAUI ignora o
   format string e mostraria literalmente "True"/"False". Corrigido com um conversor próprio
   (`BoolParaTextoConverter`, `ConverterParameter="TextoSeVerdadeiro;TextoSeFalso"`).

**Bugs reais encontrados só ao testar num aparelho físico (não apareciam nos testes automatizados
nem no build — só rodando o APK de verdade num celular):**

6. **APK instalado manualmente abria e fechava na hora**, com o Android gerando um *tombstone*
   (crash nativo) e a mensagem `Abort message: 'No assemblies found in
   .../.__override__' ... Assuming this is part of Fast Deployment'`. Causa: builds **Debug** do
   .NET MAUI usam "Fast Deployment" — o APK gerado não embute as DLLs geradas, espera que a
   ferramenta de deploy (Visual Studio, ou `dotnet build -t:Install` via cabo) as envie separado ao
   instalar. Sideload manual (copiar o `.apk` e instalar direto, sem esse passo) nunca recebe as
   DLLs, e o runtime aborta ao constatar que não há nenhuma. Corrigido gerando um build **Release**
   (`-c Release`), que sempre embute todos os assemblies dentro do próprio APK. Diagnosticado
   conectando o celular por USB (Depuração USB) e lendo `adb logcat -s DEBUG:F` — o *tombstone* trazia
   a mensagem de abort exata.
7. **App abria, chegava até o login, mas caía com `FATAL EXCEPTION` logo depois de entrar.** O log
   (`adb logcat -s AndroidRuntime:E`) mostrou: `System.Exception: Global routes currently cannot be
   the only page on the stack, so absolute routing to global routes is not supported`, na linha
   `Shell.Current.GoToAsync("//ProjetosPage")` de `LoginPage`. Causa: `"//"` é navegação
   **absoluta**, só suportada quando o destino é uma tela declarada como raiz (`ShellContent`) no
   `AppShell.xaml` — só `LoginPage` é raiz nesse app; todas as outras telas (incluindo
   `ProjetosPage`) são registradas via `Routing.RegisterRoute` para navegação relativa (empilhada).
   Esse `"//"` era inconsistente com o resto do código, que já usava rotas relativas em todo lugar.
   Corrigido trocando para `Shell.Current.GoToAsync(nameof(ProjetosPage))` (sem `"//"`).
8. **App funcionando no emulador não conseguia falar com a Api a partir de um celular físico.**
   `ConfiguracaoApi.BaseUrl` estava fixo em `http://10.0.2.2:5000/` — `10.0.2.2` é um alias especial
   que só existe *dentro* do emulador Android (mapeia pro `localhost` da máquina host); não significa
   nada pra um celular de verdade. Corrigido trocando para o IP da máquina na rede Wi-Fi
   (`Get-NetIPAddress`), com três pré-requisitos adicionais descobertos na hora: celular e PC
   precisam estar na mesma rede, a Api precisa estar ligada em todas as interfaces (`dotnet run
   --urls http://0.0.0.0:5080`, não só `localhost`, que só aceita conexões da própria máquina), e o
   Firewall do Windows precisa de uma regra liberando a porta de entrada (exige PowerShell como
   Administrador — não é uma ação que se automatiza sem elevação).

---

## Fase 7 — Hardening e entrega (em andamento)

**Logging estruturado (Serilog).** Adicionado `Serilog.AspNetCore` + `Serilog.Enrichers.Environment`
ao `Camdas.Api`, configurado inteiramente via `appsettings.json` (seção `Serilog`, sem nada hard-coded
em `Program.cs`): console + arquivo com rolling diário (`logs/camdas-.log`, 14 dias de retenção).
`app.UseSerilogRequestLogging()` loga automaticamente cada requisição HTTP (método, rota, status,
duração). `TratadorDeExcecoesGlobal` passou a logar toda exceção capturada: `Warning` para
`DomainException`/`RecursoNaoEncontradoException`/erro de validação (esperado, já vira uma resposta
HTTP tratada) e `Error` com stack trace completo só para o 500 genérico (o único caso realmente
inesperado). Nenhum bug encontrado — mudança aditiva, os testes existentes continuaram passando de
primeira.

**Testes end-to-end do fluxo completo.** `tests/Camdas.Api.Tests/PlantaFluxoCompletoTests.cs` cobre
importar → `GET arquivo` (bytes exatos) → cotar → `PUT`/`GET imagem` de camada (bytes exatos) →
histórico na ordem certa, além de 401/404. Nenhum bug de código foi encontrado — os testes passaram
de primeira.

**Atrito de ambiente encontrado ao rodar a suíte neste ambiente (não é bug de código).** Instâncias
antigas de `Camdas.Api.exe` (de execuções manuais anteriores via `dotnet run`, fora do xUnit) ficaram
rodando em segundo plano e travaram `dotnet build`/`dotnet test` com erros de arquivo em uso
(`MSB3027`/`CS2012`, DLL do próprio `Camdas.Api`/`Camdas.Application` bloqueada). Resolvido
localizando o processo pelo PID indicado na própria mensagem de erro do MSBuild
(`Get-Process -Id <pid>` + `Stop-Process`) antes de compilar de novo — vale lembrar disso se aparecer
de novo em outra sessão de desenvolvimento.

Itens ainda pendentes da Fase 7: guia de deploy na intranet, guia de instalação do APK, e revisão
final de segurança.

---

## Fase 8 — Limpeza de código morto e correções reportadas em teste no aparelho

**O que motivou esta fase.** Depois de testar o app num aparelho físico por um tempo, três
problemas de UI foram reportados e, junto com eles, pedido explícito de limpar código que sobrou
das versões anteriores do projeto.

**1. Remoção completa da entidade `Cota`.** `Cota` (medida estruturada ponto-a-ponto, com os value
objects `Ponto2D`/`Medida`/`UnidadeMedida`) era um recurso herdado da primeira versão do projeto
(a ferramenta formal de ratificação). Depois do pivô para desenho livre, nenhuma tela do app Mobile
chamava mais nenhum endpoint de Cota — foi confirmado por busca em todo o código antes de remover.
Removida em todas as camadas: `Camdas.Domain` (entidade + value objects + os 3 valores de enum
`TipoAcaoHistorico.Cota*`), `Camdas.Application` (pasta `Cotas/` inteira), `Camdas.Infrastructure`
(mapeamento EF Core, `.Include` no repositório), `Camdas.Api` (`CotasController`),
`Camdas.Contracts` (`CotaDto`, `AdicionarCotaRequest`/`EditarCotaRequest`, `Camada.Cotas` do
`CamadaDto`) e `Camdas.Mobile.Core`/`Camdas.Mobile` (`IApiClient`/`ApiClient`,
`PlantaOverlayRenderer` sem mais desenhar linha de cota). A migration foi recriada do zero (6
tabelas em vez de 7).

**2. Testes órfãos apagados.** As pastas `tests/Camdas.Domain.Tests/ValueObjects/` (testava
`Ponto2D`/`Medida`) e `tests/Camdas.Application.Tests/Cotas/` foram apagadas inteiras. Os demais
arquivos de teste que só tinham *trechos* sobre Cota (`PlantaTests`, `HistoricoAlteracaoTests`,
`CamdasDbContextTests`, `RepositoriosEfCoreTests`, `PlantaFluxoCompletoTests`,
`PlantaViewModelTests`, `PlantaOverlayRendererTests`, `HistoricoViewModelTests`) foram ajustados em
vez de apagados — o resto do teste continuava válido. Resultado: de 69 testes (dos quais vários
cobriam só Cota) para 62 testes, todos cobrindo comportamento que existe de verdade hoje.

**3. Botão de arrastar (drag-and-drop) não funcionava.** O `DragGestureRecognizer`/
`DropGestureRecognizer` nativo do MAUI, usado para reordenar a prioridade das camadas, não
respondia de forma confiável a toque no Android (comportamento de arrastar em telas touch nesses
controles nativos é conhecido por ser inconsistente entre plataformas). Trocado por uma solução
mais simples e previsível: botões "▲"/"▼" por camada, chamando
`PlantaViewModel.MoverCamadaAsync(camada, paraCima)` (troca de posição com a vizinha imediata,
servidor renumera em ordem crescente e devolve a lista atualizada) — mesma API de reordenação já
existente (`PUT .../camadas/ordem`), só mudou como o gesto é capturado na UI.

**4. Planta cortada na visualização geral.** `PlantaPage.xaml` dividia o espaço vertical em partes
praticamente iguais entre o canvas da planta e a lista de camadas (`RowDefinitions="Auto,*,Auto,*,
Auto"`). Corrigido dando um peso de 7:2 ao canvas em relação à lista
(`RowDefinitions="Auto,7*,Auto,2*,Auto"`), já que a planta é o conteúdo principal da tela e a lista
de camadas só precisa de um espaço menor e rolável.

**5. Espessura da borracha numa escala maior.** O `Slider` de espessura tinha um teto fixo de 24px,
compartilhado entre o traço normal e a borracha — apagar uma área grande exigia passar o dedo várias
vezes. Corrigido com um teto dinâmico (`CamadaEdicaoViewModel.EspessuraMaxima`, calculado a partir
de `ModoApagar`): 24 no modo desenho, 120 no modo borracha, com a espessura atual sendo reduzida
automaticamente se ultrapassar o novo teto ao trocar de modo (evita um `Value > Maximum` inválido no
`Slider`).

**6. Miniatura da planta na lista do projeto.** Adicionado `PlantaListItemViewModel`, que carrega em
segundo plano (sem travar a lista) a imagem base + todas as camadas com traço, compõe com o mesmo
`PlantaOverlayRenderer` já usado na tela principal, reduz para uma miniatura de 160px e expõe como
`byte[]` PNG — a conversão para `ImageSource` fica isolada na camada de UI
(`BytesParaImageSourceConverter`), já que `Camdas.Mobile.Core` não tem dependência de MAUI.

**7. Relatório de atualizações em PDF.** Novo botão "Relatório" no canto superior da aba Projetos.
Gera um PDF com QuestPDF (`RelatorioPdfService`, em `Camdas.Mobile.Core`, testável sem Android) a
partir de um changelog versionado (`HistoricoVersoes`, em ordem crescente a partir da 1.0), cada
entrada com data/hora, o que foi feito e quais bugs foram corrigidos em teste. O PDF é salvo em
`FileSystem.CacheDirectory` e aberto no visualizador padrão do Android via `Launcher.OpenAsync` — o
app não embute um leitor de PDF próprio.

**Verificação.** `dotnet build Camdas.sln` limpo e `dotnet test Camdas.sln` com 62/62 testes
passando, cobrindo os itens 1, 2, 6 e 7 acima com testes automatizados; os itens 3, 4 e 5 são
mudanças de UI/XAML, verificadas visualmente (sem teste automatizado de layout/gesto — não há
suíte de UI automation neste projeto).

## Como este relatório é mantido

Cada vez que uma fase é concluída (ou revisada), a seção correspondente aqui é atualizada com: o que
foi construído, o que os testes daquela fase cobrem especificamente, e — o mais importante — qualquer
bug real encontrado no caminho, com causa raiz e correção. O objetivo é que este arquivo sirva de
histórico honesto do que funcionou de primeira e do que precisou de investigação, não só uma lista de
tarefas concluídas.
