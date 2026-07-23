# Relatório de Execução — BellucSketch

Este documento acompanha, fase a fase, o que foi implementado, quais testes existem e o que cada um
verifica, e quais erros reais apareceram no caminho (com a causa raiz e a correção). É atualizado
sempre que uma fase é concluída ou alterada. Para o backlog checklist, veja [TASKS.md](TASKS.md).

**Estado atual: Fases 1 a 12 concluídas.** A entidade `Cota` (medida estruturada) e todo o fluxo de
revisão/aprovação/versionamento por perfil foram removidos por completo na Fase 8 — não sobrou
nenhum uso deles na UI atual. Isso é diferente do fluxo de **edição colaborativa** (Web solicita, Android aprova)
descrito na Fase 8.2 abaixo, que é uma feature nova e continua ativa hoje. A Api saiu do PC local e
está publicada no Render (HTTPS automático) com banco/armazenamento no Supabase — ver Fase 8.1 e
[GUIA_DEPLOY_RENDER.md](GUIA_DEPLOY_RENDER.md); o app não pergunta mais IP de servidor (Fase 10). A
ferramenta de seleção de cota com OCR (Fase 11), cogitada como "próximo passo" ao fim da Fase 10, já
foi implementada. O projeto inteiro foi renomeado de Camdas para **BellucSketch** na Fase 12 (este
documento usa o nome atual em todas as fases, mesmo nas anteriores à renomeação, para não confundir
com o código de hoje — os nomes literais no código *durante* cada fase histórica eram `Camdas.*`).
Fases 9 a 12 foram verificadas manualmente num aparelho físico, sem testes automatizados novos — ver
pendência do `dotnet test BellucSketch.sln` quebrado por `PlantaViewModelTests.cs`, ainda não
corrigida (não é causada por nenhuma dessas fases, ver Fase 10). Fase 7 (hardening) segue com o guia
de deploy na intranet como caminho alternativo ao Render — ver
[GUIA_DEPLOY_INTRANET.md](GUIA_DEPLOY_INTRANET.md).

---

## Visão geral do placar de testes

| Projeto de teste | O que cobre | Quantidade (Fase 8) | Status |
|---|---|---|---|
| `BellucSketch.Domain.Tests` | Entidades e regras de negócio puras | 20 | ✅ |
| `BellucSketch.Application.Tests` | Casos de uso (handlers MediatR) com mocks | 6 | ✅ |
| `BellucSketch.Mobile.Core.Tests` | ViewModels do app Android, renderer SkiaSharp e geração do relatório PDF | 22 | ⚠️ ver nota |
| `BellucSketch.Infrastructure.Tests` | Mapeamento EF Core + repositórios | 7 | ✅ |
| `BellucSketch.Api.Tests` | Integração ponta a ponta via HTTP | 7 | ✅ |
| **Total (na época)** | | **62** | — |

> **Contagem desatualizada.** A tabela acima reflete o placar no fim da Fase 8. Desde então:
> `RelatorioPdfServiceTests` foi apagado junto com a feature que testava (ver Fase 8.2), e
> `PlantaViewModelTests.cs:102` (`BellucSketch.Mobile.Core.Tests`) quebra a build da solução inteira
> desde a Fase 10 (referencia `PlantaViewModel.CamadaSelecionadaParaEdicao`, propriedade renomeada
> para `CamadaEmEdicaoId`) — por isso não há uma contagem nova e verificada para reportar aqui. Rodar
> `dotnet test BellucSketch.sln` hoje falha por causa desse arquivo antes de chegar a rodar qualquer
> teste; os demais quatro projetos de teste compilam e passam isoladamente.

Além dos testes automatizados, a Fase 6 também foi verificada por um **build Release real do APK
Android** (`com.companyname.camdas.mobile-Signed.apk`, ~49 MB, 0 erros), instalado via `adb install`
num Samsung Galaxy A15 físico e testado de ponta a ponta (login → lista de projetos → desenhar sobre
uma camada).

Rodar tudo: `dotnet test BellucSketch.sln --logger "console;verbosity=normal"`.

---

## Fase 1 — `BellucSketch.Domain`

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

## Fase 2 — `BellucSketch.Application`

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

## Fase 3 — `BellucSketch.Infrastructure`

**O que foi construído.** `BellucSketchDbContext` (EF Core) com mapeamento Fluent API completo: a coleção
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
     `Id` de **toda** entidade, globalmente, em `BellucSketchDbContext.OnModelCreating` — nossos Ids nunca
     são gerados pelo banco.

---

## Fase 4 — `BellucSketch.Api`

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

- **Faltava `Properties/launchSettings.json`.** O projeto `BellucSketch.Api` nunca tinha sido rodado com
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

## Fase 5 — `BellucSketch.Contracts`

**O que foi construído.** Um projeto novo, dependendo só de `BellucSketch.Domain`, para ser a "linguagem
comum" entre a Api e o app Mobile sem que o Mobile precise referenciar MediatR/FluentValidation/
EF Core. Isso motivou um refactor: os DTOs de resposta (`ProjetoDto`, `PlantaDto`, `CamadaDto`,
`HistoricoDto`) e o mapeador `Mapeamentos` (extension methods `ParaDto()`) que viviam dentro de
`BellucSketch.Application` foram movidos para `BellucSketch.Contracts` (`Mapeamentos` passou de `internal` para
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

## Fase 6 — `BellucSketch.Mobile` (.NET MAUI / Android)

**O que foi construído.** O app Android acabou virando **dois projetos**, não um — decisão tomada
assim que percebi que um projeto MAUI (`net8.0-android`, `OutputType=Exe`) não pode ser referenciado
por um projeto de teste `net8.0` comum (incompatibilidade de TFM para esse sentido de referência).
Para que ViewModels e lógica de desenho fossem testáveis sem precisar de emulador Android, ficou:

- **`BellucSketch.Mobile.Core`** (`net8.0`, biblioteca "plain", zero dependência de MAUI): ViewModels
  (`LoginViewModel`, `ProjetosViewModel`, `PlantasDoProjetoViewModel`, `PlantaViewModel`,
  `CamadaEdicaoViewModel`, `HistoricoViewModel`, todos com CommunityToolkit.Mvvm),
  `IApiClient`/`ApiClient` (cliente HTTP tipado consumindo `BellucSketch.Contracts`), `ITokenStore` (só a
  interface), `TokenAuthHandler` (anexa o Bearer token em toda requisição), `PlantaOverlayRenderer`
  (desenha a imagem base da planta + o traço raster de cada camada visível, com SkiaSharp puro, sem
  depender de Android) e `Relatorios/` (changelog versionado + `RelatorioPdfService`, geração do PDF
  com QuestPDF).
- **`BellucSketch.Mobile`** (`net8.0-android`, escafoldado com `dotnet new maui`): as Views (XAML) +
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

**Testes (em `BellucSketch.Mobile.Core.Tests`, sem nenhuma dependência de Android/emulador).**
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
ao `BellucSketch.Api`, configurado inteiramente via `appsettings.json` (seção `Serilog`, sem nada hard-coded
em `Program.cs`): console + arquivo com rolling diário (`logs/camdas-.log`, 14 dias de retenção).
`app.UseSerilogRequestLogging()` loga automaticamente cada requisição HTTP (método, rota, status,
duração). `TratadorDeExcecoesGlobal` passou a logar toda exceção capturada: `Warning` para
`DomainException`/`RecursoNaoEncontradoException`/erro de validação (esperado, já vira uma resposta
HTTP tratada) e `Error` com stack trace completo só para o 500 genérico (o único caso realmente
inesperado). Nenhum bug encontrado — mudança aditiva, os testes existentes continuaram passando de
primeira.

**Testes end-to-end do fluxo completo.** `tests/BellucSketch.Api.Tests/PlantaFluxoCompletoTests.cs` cobre
importar → `GET arquivo` (bytes exatos) → cotar → `PUT`/`GET imagem` de camada (bytes exatos) →
histórico na ordem certa, além de 401/404. Nenhum bug de código foi encontrado — os testes passaram
de primeira.

**Atrito de ambiente encontrado ao rodar a suíte neste ambiente (não é bug de código).** Instâncias
antigas de `BellucSketch.Api.exe` (de execuções manuais anteriores via `dotnet run`, fora do xUnit) ficaram
rodando em segundo plano e travaram `dotnet build`/`dotnet test` com erros de arquivo em uso
(`MSB3027`/`CS2012`, DLL do próprio `BellucSketch.Api`/`BellucSketch.Application` bloqueada). Resolvido
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
objects `Ponto2D`/`Medida`/`UnidadeMedida`) não tinha nenhum consumidor na UI atual — nenhuma tela do
app Mobile chamava mais nenhum endpoint de Cota, confirmado por busca em todo o código antes de
remover.
Removida em todas as camadas: `BellucSketch.Domain` (entidade + value objects + os 3 valores de enum
`TipoAcaoHistorico.Cota*`), `BellucSketch.Application` (pasta `Cotas/` inteira), `BellucSketch.Infrastructure`
(mapeamento EF Core, `.Include` no repositório), `BellucSketch.Api` (`CotasController`),
`BellucSketch.Contracts` (`CotaDto`, `AdicionarCotaRequest`/`EditarCotaRequest`, `Camada.Cotas` do
`CamadaDto`) e `BellucSketch.Mobile.Core`/`BellucSketch.Mobile` (`IApiClient`/`ApiClient`,
`PlantaOverlayRenderer` sem mais desenhar linha de cota). A migration foi recriada do zero (6
tabelas em vez de 7).

**2. Testes órfãos apagados.** As pastas `tests/BellucSketch.Domain.Tests/ValueObjects/` (testava
`Ponto2D`/`Medida`) e `tests/BellucSketch.Application.Tests/Cotas/` foram apagadas inteiras. Os demais
arquivos de teste que só tinham *trechos* sobre Cota (`PlantaTests`, `HistoricoAlteracaoTests`,
`BellucSketchDbContextTests`, `RepositoriosEfCoreTests`, `PlantaFluxoCompletoTests`,
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
(`BytesParaImageSourceConverter`), já que `BellucSketch.Mobile.Core` não tem dependência de MAUI.

**7. Relatório de atualizações em PDF.** Novo botão "Relatório" no canto superior da aba Projetos.
Gera um PDF com QuestPDF (`RelatorioPdfService`, em `BellucSketch.Mobile.Core`, testável sem Android) a
partir de um changelog versionado (`HistoricoVersoes`, em ordem crescente a partir da 1.0), cada
entrada com data/hora, o que foi feito e quais bugs foram corrigidos em teste. O PDF é salvo em
`FileSystem.CacheDirectory` e aberto no visualizador padrão do Android via `Launcher.OpenAsync` — o
app não embute um leitor de PDF próprio.

> **Removido depois (ver Fase 8.2).** Esse botão/serviço não existe mais no app — `RelatorioPdfService`,
> `HistoricoVersoes` e `AtualizacaoVersao` foram apagados junto com o pacote QuestPDF, substituídos por
> uma checagem automática de versão nova via GitHub Releases.

**Verificação.** `dotnet build BellucSketch.sln` limpo e `dotnet test BellucSketch.sln` com 62/62 testes
passando, cobrindo os itens 1, 2, 6 e 7 acima com testes automatizados; os itens 3, 4 e 5 são
mudanças de UI/XAML, verificadas visualmente (sem teste automatizado de layout/gesto — não há
suíte de UI automation neste projeto).

---

## Fase 8.2 — Fluxo de edição colaborativa (Web solicita, Android aprova) e mais operações de Camada

**Não documentada fase a fase originalmente** — reconstruída aqui a partir do código atual e do
histórico de commits (`8b2e43f`, `479b89f`, entre outros), sem os detalhes de bugs/causa raiz que as
demais fases registram, porque não houve um acompanhamento em tempo real dessa sessão.

**O que foi construído.** A Web (`BellucSketch.Web`, antes só um visualizador somente-leitura) ganhou
a capacidade de propor mudanças de camada, sem aplicá-las direto: `EdicaoPendenteCamada` (entidade
nova, status `Pendente|Aprovada|Rejeitada`) guarda o tipo de operação
(`TipoOperacaoEdicaoPendente`: visibilidade/opacidade/bloqueio/reordenar/excluir), os dados antes/
depois (JSON) e quem pediu + o motivo. Casos de uso novos em `Application/EdicoesPendentes/`
(`SolicitarEdicaoCamada`, `ListarEdicoesPendentes`, `AprovarEdicaoCamada`, `RejeitarEdicaoCamada`) e
um controller dedicado (`EdicoesPendentesController`). Aprovar aplica a mudança de verdade na
Planta/Camada; até lá, nada muda no mestre (Android).

A decisão de **quando** isso é necessário fica inteiramente do lado do cliente, via uma porta nova
sem equivalente na Api (`IPlataformaEdicao`): `PlataformaEdicaoDireta` (Android, "mestre") sempre
aplica direto; `PlataformaEdicaoWeb` só exige aprovação para **excluir camada** — visibilidade,
opacidade, bloqueio e reordenar continuam livres na Web, por não mexerem no traço em si. Uma nova
tela no Android (`RevisaoEdicoesPage`) lista as pendências de uma planta para aprovar/rejeitar (com
motivo obrigatório na rejeição); a Web mostra um indicador (⏳) na camada/planta com pendência.

De quebra, `Camada` ganhou três recursos que não existiam nas fases documentadas: `Opacidade`
(slider, `DefinirOpacidadeCamada`), `BloqueioAlpha` (`BloquearAlphaCamada`/`DesbloquearAlphaCamada` —
trava a transparência do traço já pintado, independente do bloqueio normal de edição) e
`DuplicarCamada` (copia nome/opacidade/visibilidade/traço para uma camada nova logo abaixo).

O botão "Relatório" em PDF (Fase 8, item 7) foi removido nesse meio-tempo — `RelatorioPdfService`/
`HistoricoVersoes`/`AtualizacaoVersao` e o pacote QuestPDF saíram do projeto. No lugar, ganhou uma
checagem automática de atualização: `IVerificadorAtualizacao`/`VerificadorAtualizacaoGitHub` consulta
`GET /repos/samuelmiguel9/camdas/releases/latest` da API pública do GitHub e compara com a versão
embutida (arquivo `VERSION` na raiz do repo), mostrando um aviso "Nova versão disponível" com link de
download na tela de Projetos quando há uma release mais nova — depende de haver, de fato, uma
GitHub Release publicada com a tag `v<VERSION>` (passo manual, não automatizado pelo build).

**Testes.** Nenhum teste automatizado novo para o fluxo de edição pendente (verificado manualmente,
Web pedindo e Android aprovando/rejeitando). A checagem de atualização e as novas operações de
Camada também não têm cobertura de teste dedicada neste momento.

---

## Fase 9 — Upgrade pro SkiaSharp 3.x

**O que motivou esta fase.** Atualizar `SkiaSharp`/`SkiaSharp.Views.Maui.Controls` pra 3.119.4 (a
versão anterior ficava presa numa API mais antiga) e o `BellucSketch.Mobile` de `net8.0-android` pra
`net9.0-android`, acompanhando o SDK.

**Bugs reais encontrados e corrigidos no caminho** (branch `upgrade-skiasharp-3x`):

1. **SIGSEGV nativo ao redimensionar a superfície pra 0×0.** Durante transições de foco/layout
   (teclado abrindo, S Pen pairando perto da tela, fragment sendo recriado), o Android podia colapsar
   a `SKCanvasView` pra tamanho 0 por um instante. `ObterImagemBaseEscalada` chamava
   `ImagemBase.Resize` com destino 0×0, gerando um `SKBitmap` com buffer de pixels inválido — o
   `DrawBitmap` seguinte crashava nativo dentro do SkiaSharp. Corrigido com um guard em
   `OnPaintSurface` (`if (e.Info.Width <= 0 || e.Info.Height <= 0) return;`) e encode direto do
   `SKBitmap` (sem passar por `SKImage.FromBitmap`) ao salvar PNG.
2. **SIGSEGV ao desenhar um bitmap já liberado/inválido.** No SkiaSharp 3.x, `SKCanvas.DrawBitmap`
   empacota o bitmap num `SKImage` internamente (`sk_image_new_from_bitmap`) — se o bitmap já foi
   liberado (`Handle` nulo) ou está sem pixels alocados, isso derruba o app com SIGSEGV sem exceção
   gerenciável nenhuma pra capturar. Acontecia em redesenhos disparados por transições de foco/
   navegação, quando um bitmap era trocado/liberado em paralelo. Corrigido centralizando toda checagem
   em `PlantaOverlayRenderer.PodeDesenhar(SKBitmap?)` (`Handle != IntPtr.Zero && Width/Height > 0 &&
   ReadyToDraw`), chamado antes de qualquer `DrawBitmap` no app inteiro.
3. **Crash com zoom alto numa planta grande.** O `SKCanvasView` da tela de visualização era inflado
   pra `imagemBase.Width × zoom` (pra rolar dentro do `ScrollView`) — acima de ~100 MB de superfície o
   Android recusa desenhar (`RecordingCanvas.throwIfCannotDraw`) e derruba o app. Corrigido com um teto
   de zoom calculado a partir de um orçamento de bytes e dimensão máxima por lado
   (`ZoomMaximoSeguro`), aplicado antes de qualquer inflação de superfície (superado pela Fase 10, que
   removeu esse mecanismo de inflar-e-rolar por completo).
4. **Crash pairando a S Pen sem tocar (Galaxy Tab A, Android 8.1).** `ACTION_HOVER_ENTER` derrubava o
   app com SIGSEGV nativo dentro de `libSkiaSharp.so` — parece bug da própria lib ao processar hover
   em versões antigas do Android. Suprimido registrando um `IOnHoverListener` nativo que consome o
   evento sem repassar adiante, já que o app nunca usou hover pra nada.

**Verificação.** Nenhum desses quatro tinha reprodução automatizada (são todos crashes nativos
dependentes de timing/hardware específico, reproduzidos em teste manual no aparelho físico
mencionados nos commits) — corrigidos e confirmados sem reaparecer em uso normal após cada fix.
`dotnet build BellucSketch.sln` seguiu limpo em cada commit.

---

## Fase 10 — Inversão pan/desenho, zoom por pinça, ferramenta de ícones e limpeza de config obsoleta

**O que motivou esta fase.** Uma sequência longa de pedidos de UX (curva suave no traço, trava de
desenho fora da planta, inverter o padrão pan/desenho, zoom por pinça, resolução do PDF importado) e
uma feature nova grande (ferramenta de ícones técnicos padronizados), todos testados ao vivo num
Samsung Galaxy Tab A conectado por cabo (`adb install -r`) durante a sessão.

**1. Traço anguloso → curva suave.** O desenho livre conectava os pontos do toque com retas simples
(`DrawLine` cru) — uma curva rápida do dedo virava uma sequência de "quinas" visíveis. Corrigido com
Bézier quadrática entre os pontos médios de pontos consecutivos (técnica padrão de suavização), tanto
no desenho ao vivo (`PlantaCanvasView.OnTouch`) quanto no replay do histórico (`DesenharTraco`, usado
por desfazer/refazer/recarregar).

**2. Trava ao mexer no zoom/pan durante a edição.** A composição inteira (imagem base + todas as
camadas, em resolução nativa) era redesenhada a cada pixel de arrasto do zoom/pan — pesado o
suficiente pra travar num aparelho mais fraco. Corrigido cacheando a composição num único bitmap
(`_composicaoNativaCache` em `PlantaCanvasView`), só refeita quando o conteúdo muda de verdade (traço,
camada, imagem base) — nunca só por zoom/pan.

**3. A saga do pan/pinça — três iterações até funcionar de verdade.** Pedido: inverter o padrão (por
padrão o toque ajusta a visualização; um botão liga o desenho) e adicionar zoom por pinça de dois
dedos, igual em qualquer tela.
   - *Primeira tentativa*: `PinchGestureRecognizer` nativo do MAUI numa `ScrollView` ancestral. Não
     funcionou de forma confiável — o `SKCanvasView` com `EnableTouchEvents` já reivindica o fluxo de
     toque inteiro a partir do primeiro dedo, então o recognizer nunca reconhecia o segundo dedo de
     forma consistente (a pinça ora ficava fixa num ponto, ora simplesmente parava de responder).
   - *Causa raiz real, encontrada só depois*: `GerenciarPan` (o código de arrastar com 1 dedo) não
     filtrava por `SKTouchEventArgs.Id` — o SEGUNDO dedo de uma pinça também gerava eventos
     Pressed/Moved ali (o SkiaSharp reporta cada ponteiro separadamente) e o `Pressed` dele
     sobrescrevia a âncora do arrasto com a posição do segundo dedo, causando o "salto pro canto".
   - *Solução final*: zoom por pinça reimplementado inteiro sobre o toque bruto do SkiaSharp (sem
     `PinchGestureRecognizer` nenhum), rastreando até 2 ponteiros por `Id` num único lugar
     (`PlantaCanvasView.AtualizarPinca`) — regra fixa "1 dedo arrasta, 2 dedos dão zoom", ancorado no
     ponto médio real entre os dois dedos. Isso também expôs que a visualização geral (`ScrollView`
     com `Orientation="Both"`) intercepta e "rouba" o gesto pra rolagem nativa assim que detecta
     arrasto — antes do segundo dedo ser reconhecido. Corrigido unificando visualização e edição no
     mesmo mecanismo (canvas de tamanho fixo + `PanX`/`PanY`, rolagem nativa sempre desligada), em vez
     de inflar o canvas pra rolar dentro do `ScrollView` (mecanismo antigo, existia desde a Fase 6/9).

**4. Traço/texto fora dos limites da planta.** Um toque na margem em volta da planta (visível com
zoom baixo ou perto da borda) desenhava ali mesmo, sem nenhuma trava. Corrigido travando o ponto
convertido dentro de `[0, ImagemBase.Width] × [0, ImagemBase.Height]` antes de desenhar/escrever —
trava em vez de ignorar o toque, pra o traço continuar até a borda em vez de "sumir" de repente.

**5. Resolução do PDF importado.** `ConversorPdfParaImagemPdfium` convertia sem especificar DPI —
usava o padrão da lib (baixo o suficiente pra ficar borrado ao dar zoom numa planta convertida de
PDF). Corrigido especificando 300 DPI na conversão.

**6. Ferramenta de ícones técnicos (água fria/quente, esgoto, interruptor, ponto de gás, tomada
simples/dupla).** Feature nova, planejada em duas explorações paralelas antes de implementar (ver
plano descartado no fim da sessão). Pontos principais:
   - Os 7 SVGs (antes soltos na raiz do repo) viraram `MauiAsset` em
     `BellucSketch.Mobile/Resources/Raw/Icones`, carregados sob demanda e cacheados por
     `IconeSvgCatalogo`, desenhados via `Svg.Skia` (nova dependência) como `SKPicture` — mantém
     nitidez em qualquer zoom.
   - `PlantaCanvasView.ElementoPendente` (antes só texto) virou uma hierarquia pequena
     (`ElementoTextoPendente`/`ElementoIconePendente`) — arrastar e girar continuam genéricos (só
     mexem em `Retangulo`/`RotacaoGraus`), só redimensionar e desenhar variam por tipo. Isso deixou a
     barra de posicionamento (girar/A-/A+/confirmar/cancelar) inteiramente reaproveitável sem mudar
     nada nela.
   - Ícone confirmado grava **sempre** numa camada especial "Ícones" — criada automaticamente na
     primeira vez que a planta é aberta sem ela (`PlantaViewModel.GarantirCamadaIconesAsync`), não
     importa qual camada esteja ativa no momento. O botão de olho dela fica desabilitado (`DataTrigger`
     no XAML) — nunca some da composição. Tudo client-side, seguindo o mesmo padrão de camada
     pré-definida já usado por Hidráulica/Elétrica — zero mudança em Domain/Application/Api/Contracts.
   - **Limitação conhecida, combinada antes de implementar:** ícone não entra no desfazer/refazer
     (↶/↷) — esse mecanismo só sabe reconstruir a camada *ativa*, e ícone sempre vai pra uma camada
     diferente dela. Corrigir um ícone errado hoje é girar/redimensionar antes de confirmar, ou
     "Limpar camada" na própria Ícones.
   - **Bug real encontrado em teste no aparelho: crash nativo (SIGSEGV) ao colocar qualquer ícone.**
     `IconeSvgCatalogo.ObterAsync` fazia `using var svg = new SKSvg();` — `Dispose()` do `SKSvg`
     também descarta o `SKPicture` que ele criou, então o `Picture` cacheado ficava com o handle
     nativo morto assim que o método retornava. Confirmado via `adb logcat`: SIGSEGV dentro de
     `sk_picture_get_cull_rect` (`libSkiaSharp.so`), exatamente a primeira leitura do `Picture` já
     morto. Corrigido guardando o `SKSvg` inteiro em cache (nunca descartado) em vez de só o
     `Picture` — como os 7 ícones nunca mudam durante a execução do app, mantê-lo vivo o programa
     inteiro é seguro.

**7. Destaque visual da camada em edição.** A lista de camadas não indicava visualmente qual estava
sendo editada no momento. Corrigido com um `CamadaAtivaParaCorConverter` (`IMultiValueConverter`
comparando o Id da linha com `CamadaAtiva.Id`) aplicado ao fundo de cada linha via `MultiBinding`.

**8. Configuração de servidor obsoleta removida.** O app tentava resolver automaticamente, a cada
abertura, qual de uma lista de endereços salvos (pensado pra quando rodava contra um servidor na
intranet — "Casa"/"Trabalho") respondia na rede atual, pedindo o IP manualmente se nenhum
respondesse dentro de 2s. Como o app hoje aponta só pro servidor fixo no Render, essa resolução falhava
sempre (nenhum endereço salvo respondia da rede onde o aparelho estava), mostrando o prompt de IP toda
vez que o app abria. Removido por completo (`ResolvedorEnderecoApi`, `IArmazenamentoEnderecosApi`,
`ArmazenamentoEnderecosApiPreferences`, `EnderecoApi`, `EnderecoDinamicoHandler` — nenhum tinha mais
uso depois da remoção) — `ConfiguracaoApi.BaseUrl` agora é uma constante fixa apontando pro Render,
igual o `BellucSketch.Web` já fazia.

**Verificação.** Todo o trabalho desta fase foi testado manualmente num Samsung Galaxy Tab A físico
(build Release, `adb install -r`) — não foram adicionados testes automatizados novos. `dotnet build
src/BellucSketch.Mobile/BellucSketch.Mobile.csproj` limpo em cada etapa. **Pendência conhecida, não desta fase:**
`tests/BellucSketch.Mobile.Core.Tests/ViewModels/PlantaViewModelTests.cs:102` referencia
`PlantaViewModel.CamadaSelecionadaParaEdicao`, propriedade que não existe mais (renomeada em algum
commit anterior pra `CamadaEmEdicaoId`) — isso quebra `dotnet build BellucSketch.sln`/`dotnet test
BellucSketch.sln` (a solução inteira) hoje, mesmo com `BellucSketch.Mobile.csproj` isolado compilando limpo. Ainda
não corrigido.

---

## Fase 11 — Ferramenta de seleção de cota (OCR)

Cogitada como "próximo passo combinado" ao fim da Fase 10 — já implementada, mas (assim como a Fase
8.2) sem acompanhamento fase a fase no momento; reconstruída aqui a partir do código atual
(`OcrTextoService`, `PlantaCanvasView.ModoSelecaoCota`/`GerenciarSelecaoCota`,
`PlantaPage.OnCanvasSelecaoCotaConcluida`).

**O que foi construído.** Um botão dedicado liga o "modo cota" (desliga o pan) e o usuário arrasta um
retângulo sobre um número já impresso na planta (uma cota de desenho técnico). Ao soltar:

1. `PlantaCanvasView.RecortarComposicaoNativa` recorta só aquela área da composição já renderizada.
2. `DetectarCorDeFundo`/`DetectarAreaTexto` acham a cor de fundo predominante e a caixa delimitadora
   real da tinta dentro do recorte (mais justa que o retângulo arrastado à mão) — pixels
   **vermelhos** (a linha de cota em si) ficam de fora da área que será coberta depois, a pedido
   explícito do usuário ("pixels vermelhos você não tira, pois fazem parte da linha de cota").
3. `OcrTextoService.ReconhecerAsync` manda o recorte pro Google ML Kit Text Recognition — modelo
   "Bundled" (embutido no apk, mesma escolha de design da ferramenta de ícones: funciona offline,
   sem depender de Play Services baixar nada em obra sem sinal).
4. Um prompt nativo mostra o texto reconhecido, já editável — o usuário confirma ou corrige.
5. O texto confirmado vira um elemento de texto pendente (reaproveita a mesma barra de
   girar/A-/A+/confirmar/cancelar da ferramenta de texto livre), em **vermelho** se o valor foi
   editado ou **preto** se manteve o que o OCR leu — e já carrega consigo a máscara de cobertura
   pixel-a-pixel da área de tinta original (`AreaCobertura`/`MascaraCobertura` em `AcaoTexto`), pra
   cobrir o número impresso embaixo assim que for confirmado.

**Bug real, corrigido na Fase 12 (não nesta).** A cobertura por máscara pixel-a-pixel (em vez de um
retângulo cheio) existe desde a implementação original, mas reeditar um texto de cota já colocado
perdia essa máscara — corrigido em `ApagarTextoDoBitmap`/`IniciarEdicaoTextoColocado`, ver Fase 12.

**Testes.** Nenhum automatizado — depende de Google ML Kit rodando de verdade em Android, verificado
manualmente no aparelho físico.

---

## Fase 12 — Rebranding para BellucSketch, correção de vazamento de storage e ajustes de UX (2026-07-23)

**O que motivou esta fase.** Seis pedidos pontuais de correção de UX reportados após uso real no
aparelho, mais um pedido de renomear o app inteiro de Camdas para BellucSketch (a partir de um card
de marca novo fornecido pelo usuário), mais um achado próprio ao investigar como as imagens são
salvas no Supabase (o usuário perguntou "posso estourar o limite do banco do Supabase?").

**1. Vazamento de arquivos órfãos no Supabase Storage.** `IArquivoStorage.SalvarAsync` sempre gera
uma chave nova — nada nunca chamava um "excluir" na chave antiga. Toda vez que uma camada era salva
de novo, limpa, removida, ou uma planta inteira removida, o arquivo anterior ficava perdido no bucket
pra sempre (nunca mais referenciado por nenhuma linha do banco, mas ocupando espaço). Corrigido com
`IArquivoStorage.ExcluirAsync` (novo, implementado em `ArquivoStorageS3`/`ArquivoStorageEmDisco`,
best-effort/idempotente) chamado em `AtualizarImagemCamada` (exclui o caminho anterior depois de
salvar o novo com sucesso), `LimparCamada`, `RemoverCamada` e `RemoverPlanta` (esta última também
captura e exclui o arquivo original da planta e o de todas as camadas antes de removê-la).

**2. Rebranding para BellucSketch.** Renomeados: solução (`BellucSketch.sln`), todas as pastas/csproj
`src`/`tests`, namespaces em ~275 arquivos, ícone do app (glifo "B com lápis" a ~60% de escala num
canvas maior — a primeira tentativa cortava as pontas do B, ver nota abaixo), e o card de marca
(`capa/BellucSketch.png`) como foto de fundo persistente nas telas de Login/Projetos/Plantas do
projeto (substituindo uma splash screen transitória, removida por completo a pedido do usuário: "não
quero transição"). **Deliberadamente não renomeados** (documentado em comentário no próprio código):
`ApplicationId` (`com.companyname.camdas.mobile` — trocar forçaria reinstalar em todo aparelho),
`ConnectionStrings:Camdas` (chave de um segredo `sync: false` já preenchido manualmente no painel do
Render) e os nomes dos serviços no Render (`camdas-api`/`camdas-web` — são a URL já publicada).

**Bug encontrado durante a implementação: ícone do app cortando o "B".** Causa: o glifo preenchia o
canvas quase de ponta a ponta, mas o Android só garante visível o círculo/squircle central de ~66% de
um ícone adaptativo — o resto pode ser cortado pelo launcher dependendo do fabricante. Corrigido
regenerando o ícone com o glifo a ~60% de escala num canvas de 900×900, verificado via
`adb shell screencap` direto na gaveta de apps do aparelho físico.

**3–6. Correções de UX no canvas de desenho** (`PlantaCanvasView`/`PlantaPage`):
- Cobertura de texto/cota que reaparecia ao reeditar: `ApagarTextoDoBitmap` desenhava um `Clear` de
  retângulo cheio (furando a máscara de cobertura já aplicada por baixo) e `IniciarEdicaoTextoColocado`
  não copiava `AreaCobertura`/`MascaraCobertura` pro elemento pendente reeditável — corrigidos os dois,
  o número original agora só volta via desfazer (↶), nunca reabrindo o texto pra editar.
- Arrastar texto/ícone pendente até uma lixeira flutuante nova (`LixeiraElementoPendente`) para
  excluir, com destaque visual ao passar por cima (hit-test via `View.GetLocationOnScreen` nativo,
  mesma técnica de geometria de tela já usada em outros pontos do app).
- Rastro da borracha na prévia ao vivo ficava preto em vez de transparente — causa: a prévia (ao vivo,
  antes de soltar o dedo) usa `BlendMode.Clear` direto na superfície já composta em tela, sem nenhuma
  "camada por baixo" pra revelar — um `Clear` sem fundo transparente real pinta preto. Corrigido com
  uma prévia só indicativa (traço branco translúcido), sem tentar simular visualmente o apagar de
  verdade antes de soltar o dedo (o apagar de verdade, correto/transparente, só acontece ao soltar).
- Exclusividade de ferramentas: ativar lápis ou texto não desligava a borracha (só o botão de cota já
  fazia isso certo) — corrigido replicando o mesmo reset nos dois handlers que faltavam.

**Verificação.** Build completo da solução (`dotnet build BellucSketch.sln`) e do app Android
(`-f net9.0-android -c Release`) limpos — exceto a mesma pendência já conhecida e não causada por
esta fase (`PlantaViewModelTests.cs`, ver Fase 10). Instalado via `adb install -r` e testado num
Samsung Galaxy Tab A físico. Publicado como GitHub Release `v2.1.0`.

## Fase 13 — Ferramenta de desenho na Web (2026-07-23)

**O que motivou esta fase.** Até aqui, `BellucSketch.Web` era só um visualizador: mostrava a
composição das camadas e permitia mexer em metadado (visibilidade, opacidade, bloqueio, ordem,
exclusão), mas desenhar/editar o traço em si era exclusivo do Android (o "mestre"). O usuário pediu
pra trazer essa capacidade pro navegador, com duas decisões explícitas de escopo: (1) a Web deveria
ficar **livre**, sem nenhum pedido de aprovação — o técnico continua tendo a palavra final porque pode
mexer por cima depois no Android, não porque a Web precisa pedir licença primeiro; (2) a ferramenta de
cota com OCR deveria entrar já nesta leva, mesmo sem um equivalente óbvio ao Google ML Kit (nativo
Android) rodando num navegador.

**1. Fim do fluxo de aprovação na Web.** `PlataformaEdicaoWeb` (só "Excluir camada" pedia
responsável/motivo via `prompt()`) foi apagada; o `Program.cs` da Web passou a registrar
`IPlataformaEdicao` com `PlataformaEdicaoDireta` — a mesma classe que o Android já usava, que sempre
retorna `PrecisaAprovacao() => false`. Como esse registro é a única fonte de novas
`EdicaoPendenteCamada` (o Android nunca gerava uma), o backend inteiro desse fluxo
(`EdicaoPendenteCamada`, migrations, `EdicoesPendentesController`, `RevisaoEdicoesPage`) fica dormente
— **deliberadamente não removido**, por ser uma decisão de limpeza maior e separada do que foi pedido.
O aviso "⏳ pendente" (`Planta.razor`) foi removido, já que nunca mais teria o que mostrar de novo.

**2. Ícones técnicos viraram fonte compartilhada.** Os 7 SVGs saíram de
`src/BellucSketch.Mobile/Resources/Raw/Icones` para `assets/icones-tecnicos` (raiz do repo) — decisão
explícita do usuário pra evitar duplicar bytes versionados entre Android e Web. O Android continua
carregando exatamente como antes (`MauiAsset` aponta pro novo caminho, `LogicalName` preservado, então
`IconeSvgCatalogo.cs` não mudou uma linha); a Web ganhou `IconeSvgCatalogoWeb` (mesmo cache em memória,
mesmo motivo pra nunca descartar o `SKSvg`), buscando cada ícone via `HttpClient` em `wwwroot/icones/`.

**3. `PlantaCanvasEdicaoWeb` — o motor de desenho novo.** Em vez de refatorar o `PlantaCanvasView` do
Android (2000+ linhas, testado em produção) pra extrair uma parte compartilhada, optei por um
componente Blazor **novo e próprio** (`src/BellucSketch.Web/Rendering/PlantaCanvasEdicaoWeb.razor`) —
trade-off deliberado: evita qualquer risco de regressão no app já publicado, ao custo de duplicar a
lógica de desenho (ideia de unificação futura registrada como possível follow-up, não parte deste
trabalho). Reaproveita 100% o que já era compartilhado (`PlantaViewModel`, `PlantaOverlayRenderer`,
upload/download de PNG por camada) e reimplementa, pro navegador:

- **Captura de ponteiro**: `@onpointerdown/move/up/cancel` nativos do Blazor (`PointerEventArgs`, sem
  precisar de JS interop pra leitura de coordenada) num `<div>` que envolve o `SKCanvasView`
  (`SkiaSharp.Views.Blazor`) — só foi preciso um pedacinho de JS (`capturarPonteiro` em `planta.js`)
  pra chamar `setPointerCapture`, sem o que um arrasto rápido que sai da área do elemento perderia os
  `pointermove` seguintes.
- **Traço livre**: mesma suavização por curva Bézier quadrática (`DesenharCaminhoDoTraco`) do Android
  — pontos acumulados durante o arrasto, só "queimados" no bitmap da camada ao soltar (`FinalizarTraco`),
  com uma prévia ao vivo desenhada por cima da composição já cacheada enquanto o gesto está em
  andamento (mesmo motivo do Android pra não recompor tudo a cada amostra de ponteiro).
- **Borracha**: `SKBlendMode.Clear` na gravação real; prévia ao vivo com traço branco translúcido
  (mesmo cuidado do Android — `Clear` numa superfície de tela já composta, sem nada por baixo, pinta
  preto em vez de mostrar transparência).
- **Undo/redo**: mesma estratégia do Android — histórico de ações (`AcaoTraco`/`AcaoTexto`) + replay
  sobre um snapshot-base da camada capturado ao entrar em edição, reconstruído do zero a cada
  desfazer/refazer.
- **Texto e ícones**: viram um "elemento pendente" (arrastável, girável em passos de 90°,
  redimensionável) até o usuário confirmar — texto pede o conteúdo via `prompt()` do navegador (mesmo
  padrão já usado pela antiga `PlataformaEdicaoWeb` pra responsável/motivo); ícone sempre grava na
  camada especial "Ícones", nunca na camada ativa, igual ao Android.
- **Cortes de escopo deliberados** (não bugs, decisão consciente pra não estourar o tamanho desta
  fase): sem a interação de "segurar pra pegar de volta" um ícone/texto já confirmado — só dá pra
  ajustar antes de confirmar; a tela cheia continua só de visualização (editar fica no painel normal,
  evita ter que sincronizar histórico de undo/redo entre duas instâncias do motor).

**4. Ferramenta de cota com OCR via Tesseract.js.** Sem equivalente direto ao Google ML Kit num
navegador, optei por **Tesseract.js** (motor OCR compilado pra WebAssembly, roda 100% no cliente, sem
chamada de rede nem chave de API — mesmo espírito "on-device" do ML Kit) vendorizado em
`wwwroot/lib/tesseract` (script + worker + as 4 variantes do núcleo wasm que a lib recomenda deixar
disponíveis pra ela escolher em runtime conforme suporte a SIMD do navegador, ~36MB no total, baixados
sob demanda e cacheados pelo navegador) e dados de treinamento em português (`por.traineddata.gz`,
variante "fast" do tessdata). Criei `IOcrService` (`Mobile.Core/Services`) como interface
compartilhada — `OcrTextoService` (Android) passou a implementá-la sem mudar nenhuma linha de
comportamento; `OcrServicoWeb` (novo) chama o Tesseract.js via `IJSRuntime`
(`wwwroot/js/ocr.js`, worker mantido vivo pela sessão da página, mesmo motivo do `_reconhecedor`
singleton do Android). Diferente do Android, a Web **não** cobre automaticamente o número antigo
detectado (o Android detecta cor de fundo + área de tinta pra gerar uma máscara pixel-a-pixel — ver
Fase 11) — simplificação deliberada da primeira versão; o texto reconhecido só pré-preenche o modo
texto pra o usuário confirmar/corrigir, sem apagar o que já estava impresso.

**Bug real encontrado e corrigido durante o teste manual.** A primeira tentativa de servir os ícones
compartilhados usou `<Content Include="..\..\assets\icones-tecnicos\**" LinkBase="wwwroot\icones" />`
no `.csproj` — aparecia corretamente no manifesto de static web assets (`staticwebassets.build.json`/
`.development.json`, rota `icones/agua_fria.svg` presente) e funcionava numa publicação real, mas
voltava **404** no servidor de desenvolvimento (`dotnet run`). Causa raiz: o manifesto de
desenvolvimento do Blazor mapeia cada asset ao `ContentRootIndex` 0 (a pasta `wwwroot` física do
próprio projeto) — arquivos que só existem fisicamente FORA da árvore do projeto (nosso caso,
`assets/icones-tecnicos` na raiz do repo) e chegam via `Content`/`LinkBase` não têm um content-root
próprio nesse manifesto, então o dev server procura no lugar errado. Corrigido substituindo o
`Content`/`LinkBase` por um target de build (`CopiarIconesTecnicosCompartilhados`, roda em
`BeforeBuild`) que copia de fato os SVGs pra `wwwroot/icones` — funciona idêntico em dev e em publish,
já que os arquivos passam a existir de verdade dentro do `wwwroot` do projeto; `wwwroot/icones/` foi
pro `.gitignore` (é gerado, a fonte real continua em `assets/icones-tecnicos`).

**Bug real encontrado pelo usuário testando no navegador local: a planta aparecia cortada, presa num
canto, em QUALQUER zoom (100%, 300%, "Ajustar à tela" não mudava nada).** Diagnóstico errado na
primeira tentativa (achei que era a `<div>` extra ao redor do canvas ficando dessincronizada — remover
essa div e splatar os eventos direto no `SKCanvasView` não resolveu, o usuário confirmou que continuava
cortado). Causa raiz de verdade, encontrada só depois de baixar o código-fonte oficial do
`SkiaSharp.Views.Blazor.SKCanvasView` da tag exata da versão instalada (`v3.119.4`, repositório
`mono/SkiaSharp`): esse componente **não tem** propriedades `WidthRequest`/`HeightRequest` — isso
existe só na versão MAUI/Android do mesmo nome (`SkiaSharp.Views.Maui.Controls.SKCanvasView`, que É
usada no Android, ver `PlantaCanvasView`). O componente Blazor só declara
`[Parameter(CaptureUnmatchedValues = true)] AdditionalAttributes`, que absorve QUALQUER atributo
desconhecido (inclusive `WidthRequest`/`HeightRequest`) e o repassa como um atributo HTML literal e
sem significado pro `<canvas>` via `@attributes`; o navegador simplesmente ignora um atributo HTML
chamado "WidthRequest". O tamanho real do canvas (tanto o box CSS quanto a resolução do raster
interno) vem de `SizeWatcher` — um `ResizeObserver` que lê `clientWidth`/`clientHeight` do próprio
elemento e dispara `Invalidate()` com esse tamanho. Sem nenhum `style` explícito definindo largura/
altura, o `<canvas>` cai no tamanho padrão do HTML (300×150) — e como o desenho em si ainda escala
corretamente por `canvas.Scale(zoom)` dentro de `OnPaintSurface`, o resultado é exatamente o
sintoma relatado: a composição aparece "certa" proporcionalmente, só que espremida/cortada dentro de
uma janela sempre do mesmo tamanho fixo, não importa o valor do zoom.

**Isso não é um bug introduzido nesta fase** — o `SKCanvasView` de só-visualização em `Planta.razor`
(normal e tela cheia) já usava exatamente o mesmo `WidthRequest`/`HeightRequest` sem efeito desde antes,
provavelmente copiado por analogia do padrão MAUI/Android (onde essas propriedades existem de verdade,
como `VisualElement.WidthRequest`). Corrigido nos três `SKCanvasView` da Web (visualização normal, tela
cheia e o novo `PlantaCanvasEdicaoWeb`) trocando `WidthRequest`/`HeightRequest` por um `style="width:
...px; height:...px;"` de verdade — como atributos de **componente** Razor não aceitam markup e
expressão C# misturados no mesmo valor (só elementos HTML puros aceitam), o `style` precisou virar uma
única propriedade computada (`EstiloCanvasNormal`/`EstiloCanvas`) em vez de string interpolada inline.

**Verificação.** `dotnet build` de `BellucSketch.Web` e de `BellucSketch.Mobile` (`-f net9.0-android`)
limpos, sem novos warnings além dos já existentes. Rodei a Api e o Web localmente (Postgres local já
configurado): confirmei por `curl` que os assets novos (ícones, `tesseract.min.js`, `worker.min.js`,
as 4 variantes do núcleo wasm, `por.traineddata.gz`) respondem 200 no dev server, e reproduzi via API
crua o mesmo roundtrip que o novo motor de desenho depende (`POST /api/plantas` com imagem,
`POST .../camadas`, `PUT`/`GET .../camadas/{id}/imagem`) — upload e download batem byte a byte.
**Limitação importante**: este ambiente de execução não tem `chromium-cli` nem Node+Playwright
disponíveis, então não foi possível dirigir um navegador de verdade sozinho — o teste real (incluindo
achar e confirmar o bug do canvas acima) foi feito pelo usuário, rodando o `dotnet run` localmente na
própria máquina e reportando prints de tela. Interações mais profundas (desenhar uma curva de fato,
undo/redo, posicionar/confirmar texto e ícone, o OCR rodando no navegador) ainda não foram
exercitadas.

## Como este relatório é mantido

Cada vez que uma fase é concluída (ou revisada), a seção correspondente aqui é atualizada com: o que
foi construído, o que os testes daquela fase cobrem especificamente, e — o mais importante — qualquer
bug real encontrado no caminho, com causa raiz e correção. O objetivo é que este arquivo sirva de
histórico honesto do que funcionou de primeira e do que precisou de investigação, não só uma lista de
tarefas concluídas.
