# TASKS — BellucSketch

Backlog organizado por fases. Marque `[x]` ao concluir.

Legenda de status: `[ ]` pendente · `[~]` em andamento · `[x]` concluído

---

## Fase 0 — Fundamentos do repositório
- [x] TASKS.md (este arquivo)
- [x] `BellucSketch.sln` criada
- [x] `.gitignore` para projetos .NET/MAUI
- [x] `README.md` com instruções de build/execução

## Fase 1 — BellucSketch.Domain (regras de negócio puras) — **CONCLUÍDA**
- [x] `Entity` base (Id, igualdade por identidade) em `Common/Entity.cs`
- [x] `DomainException` (exceção específica para violação de regra de negócio)
- [x] Enums: `TipoArquivoOrigem`, `StatusProjeto`, `TipoAcaoHistorico`
- [x] Entidade `Usuario` (nome, e-mail, ativar/desativar)
- [x] Entidade `Projeto` (renomear, arquivar/reativar)
- [x] Entidade `Planta` — agregado raiz (o usuário cria as camadas manualmente, sem padrão fixo;
      orquestra camadas)
- [x] Entidade `Camada` (ligar/ocultar/bloquear/desbloquear, traço livre raster via
      `ImagemRasterCaminho`, ordem de prioridade) — construtor e mutadores `internal`, só
      acessíveis via `Planta`
- [x] Entidade `HistoricoAlteracao` (construtor público — criada pela Aplicação, fora do agregado
      Planta) — log de auditoria genérico de ações sobre planta/camada
- [x] Testes unitários (xUnit + FluentAssertions) em `tests/BellucSketch.Domain.Tests`: criação/remoção
      de camada, bloqueio de camada impede editar imagem raster, reordenação com renumeração
      crescente
- [x] Verificado: `dotnet build BellucSketch.sln` e `dotnet test tests/BellucSketch.Domain.Tests` — passando

## Fase 2 — BellucSketch.Application — **CONCLUÍDA**
- [x] Abstrações (ports): `IProjetoRepository`, `IPlantaRepository`, `IUsuarioRepository`,
      `IHistoricoRepository`, `IUnitOfWork`, `IArquivoStorage` (`SalvarAsync`/`AbrirAsync`),
      `IUsuarioContext`, `IClock`
  - Ajuste em relação ao planejado: **não** há `ICamadaRepository` — Camada é entidade filha do
    agregado `Planta` (só ela tem repositório próprio, conforme regra de um
    repositório por agregado raiz)
- [x] Casos de uso — Projetos: `CriarProjeto`, `ListarProjetos`, `ObterProjeto`
- [x] Casos de uso — Plantas: `ImportarPlanta`, `ObterPlanta`, `ListarPlantasPorProjeto`,
      `ObterArquivoPlanta` (serve os bytes da imagem importada)
- [x] Casos de uso — Camadas: `CriarCamada`, `ReordenarCamadas`, `AlternarVisibilidadeCamada`,
      `BloquearCamada`, `DesbloquearCamada`, `AtualizarImagemCamada` (salva o traço raster),
      `ObterImagemCamada`
  - Adicionados depois, sem entrada própria de fase na época (retroativamente listados aqui —
    ver RELATORIO.md, Fase 11): `DefinirOpacidadeCamada`, `BloquearAlphaCamada`/
    `DesbloquearAlphaCamada` (trava a transparência do traço já pintado, independente do bloqueio
    normal), `DuplicarCamada` (copia nome/opacidade/visibilidade/traço para uma camada nova logo
    abaixo), `LimparCamada` (esvazia o traço sem excluir a camada) e `RemoverCamada` (ver Fase 8.1)
- [x] Caso de uso — Histórico: `ObterHistoricoDaPlanta`
- [x] Validações (FluentValidation) nos comandos que recebem input externo
- [x] `RecursoNaoEncontradoException` em `Common/` — a Api mapeia para HTTP 404
- [x] Padrão MediatR (`IRequest`/`IRequestHandler`) — cada caso de uso é um arquivo único com
      Command/Query + Validator + Handler (vertical slice)
- [x] Testes de aplicação (`tests/BellucSketch.Application.Tests`, NSubstitute) cobrindo caminho feliz e
      propagação de `DomainException` (ex.: camada bloqueada) em atualização de imagem
- [x] Verificado: `dotnet build BellucSketch.sln` e `dotnet test BellucSketch.sln` — passando

## Fase 3 — BellucSketch.Infrastructure — **CONCLUÍDA**
- [x] `BellucSketchDbContext` (EF Core) + mapeamentos (Fluent API) para todas as entidades/VOs
  - `DbSet` só para os agregados raiz (`Projeto`, `Planta`) e para `HistoricoAlteracao` (entidade
    independente) — Camada é alcançada só via navegação de `Planta`
  - Coleção do agregado (`Camadas` em `Planta`) mapeada via backing field
    (`PropertyAccessMode.Field`), já que a propriedade pública expõe apenas um wrapper
    somente-leitura
  - Enums sempre persistidos como `string` (`HasConversion<string>()`) para legibilidade no banco
  - Verificado com construtores: o EF Core prioriza o construtor com **menos** parâmetros (o
    `private` sem parâmetros de cada entidade) ao materializar — os construtores de negócio com
    validação não são usados na reidratação, sem risco de revalidar/resetar estado ao carregar
- [x] Provider: **PostgreSQL** (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- [x] Migration `InitialCreate` — 6 tabelas (`Projetos`, `Plantas`, `Camadas`, `Usuarios`,
      `HistoricoAlteracoes`, `__EFMigrationsHistory`)
- [x] `BellucSketchDbContextFactory` (`IDesignTimeDbContextFactory`) — permite `dotnet ef` funcionar sem
      depender do projeto Api
- [x] Repositórios concretos (`Repositories/`): `ProjetoRepositoryEfCore`, `PlantaRepositoryEfCore`
      (sempre carrega o agregado completo via `Include`), `UsuarioRepositoryEfCore`,
      `HistoricoRepositoryEfCore`, `UnitOfWorkEfCore`
- [x] `ArquivoStorageEmDisco` (`IArquivoStorage`) — salva e reabre arquivos num diretório raiz
      configurável (disco local ou caminho de rede/UNC), validando que o caminho lido fica dentro
      da raiz de armazenamento
- [x] `IConversorPdfParaImagem` + `ConversorPdfParaImagemPdfium` (via `PDFtoImage`/PDFium+SkiaSharp)
      — `ImportarPlantaCommandHandler` converte a 1ª página do PDF em PNG antes de salvar, quando
      `TipoArquivoOrigem == Pdf` (o arquivo salvo é sempre raster exibível)
- [x] Testes de integração com **Sqlite** (motor relacional real, sem Docker) exercitando os
      repositórios concretos + `UnitOfWorkEfCore` fim a fim
- [x] Verificado: `dotnet build BellucSketch.sln` e `dotnet test BellucSketch.sln` — passando

## Fase 4 — BellucSketch.Api — **CONCLUÍDA**
- [x] Configuração do host ASP.NET Core (`Program.cs`), DI de todas as camadas (Application +
      Infrastructure), `appsettings.json` (connection string, Jwt, diretório de armazenamento)
- [x] Autenticação JWT via `[Authorize]` simples — qualquer usuário autenticado pode usar qualquer
      endpoint (sem perfis/roles)
  - **Pendência conhecida e sinalizada no código**: não há login por credencial ainda — `Usuario`
    não tem campo de senha. `POST /api/auth/dev-token` emite um token para qualquer `UsuarioId`
    existente **sem verificar senha** — documentado como placeholder de desenvolvimento/teste em
    `AutenticacaoController`, não usar em produção. Implementar login real (hash de senha, ex.
    `PasswordHasher<Usuario>`) fica para um próximo passo.
- [x] Controllers: `ProjetosController` (criar/listar/renomear/excluir, com exclusão em cascata das
      plantas), `PlantasController` (importação via multipart/form-data, com nome/descrição/cliente
      opcionais; `GET .../arquivo` serve a imagem da planta importada), `CamadasController` (criar,
      reordenar, visibilidade, bloqueio, `PUT`/`GET .../imagem` salva/baixa o traço raster de cada
      camada), `HistoricoController`, `AutenticacaoController`
- [x] Middleware de tratamento de erros (`TratadorDeExcecoesGlobal : IExceptionHandler`) — mapeia
      `DomainException` → 400, `RecursoNaoEncontradoException` → 404, `FluentValidation.ValidationException`
      → 400 com detalhe por campo; qualquer outra exceção → 500 genérico
- [x] `ValidationBehavior<TRequest,TResponse>` (pipeline do MediatR, em `BellucSketch.Application.Common`)
      roda os validadores FluentValidation antes de cada handler
- [x] Testes de integração de endpoints (`tests/BellucSketch.Api.Tests`, `WebApplicationFactory` + Sqlite)
      cobrindo o fluxo completo (login → projeto → planta → `GET arquivo` → criar camada →
      `PUT`/`GET imagem` de camada → histórico), 401 sem token, 404 recurso inexistente, 404 camada
      sem imagem
- [x] Documentação OpenAPI/Swagger (`Swashbuckle`, com suporte a Bearer token na UI)
- [x] Verificado: build e testes passando

### Bugs reais encontrados e corrigidos durante a Fase 4 (vale registrar — não são óbvios)
- **EF Core: entidade filha nova virando UPDATE em vez de INSERT.** Ao adicionar uma `Cota` a uma
  `Camada` já persistida (carregada como `Unchanged` numa unidade de trabalho anterior), o EF Core
  marcava a nova `Cota` como `Modified`, não `Added` — por convenção, uma chave `Guid` "não vazia" é
  tratada como possivelmente já existente no banco (`ValueGeneratedOnAdd`). Corrigido configurando
  `ValueGenerated.Never` para a propriedade `Id` de toda entidade, globalmente, em
  `BellucSketchDbContext.OnModelCreating` — já que nossos Ids são sempre gerados pela própria entidade
  (`Entity.Id`), nunca pelo banco.
- **JWT: claim "sub" sumindo.** Por padrão, `JwtBearerHandler` remapeia claims curtas do JWT (como
  `sub`) para as URIs longas de `ClaimTypes` (`ClaimTypes.NameIdentifier`). `UsuarioContextHttp`
  procurava literalmente por `"sub"` e nunca encontrava. Corrigido com
  `bearerOptions.MapInboundClaims = false;`.
- **JWT: options lidas cedo demais.** `TokenValidationParameters` era montado lendo
  `builder.Configuration` diretamente em `Program.cs`, antes do host terminar de montar — podendo
  divergir do `IOptions<JwtOptions>` resolvido tardiamente (via DI) em outros pontos. Corrigido com o
  padrão oficial "options que dependem de outro serviço":
  `services.AddOptions<JwtBearerOptions>(...).Configure<IOptions<JwtOptions>>(...)`.
- **Enum como string no corpo JSON.** `[FromForm]` aceita enum por nome nativamente, mas
  `System.Text.Json` (usado no `[FromBody]`) não converte string→enum sem um conversor explícito.
  Corrigido registrando `JsonStringEnumConverter` em `AddControllers().AddJsonOptions(...)`.

## Fase 5 — BellucSketch.Contracts — **CONCLUÍDA**
- [x] DTOs de request/response usados por `Api` e `Mobile` — projeto novo, só depende de
      `BellucSketch.Domain` (entidades/enums, sem dependências)
  - `ProjetoDto`, `PlantaDto`, `CamadaDto` (inclui `TemImagemRaster`, um flag em vez de expor o
    caminho de disco ao cliente), `HistoricoDto`, e o mapeador `Mapeamentos` (extension methods
    `ParaDto()`, `public` por ser usado entre assemblies)
  - Records de request (`CriarProjetoRequest`, `RenomearProjetoRequest`, `CriarCamadaRequest`,
    `ReordenarCamadasRequest`, `EmitirTokenRequest`/`EmitirTokenResponse`) também vivem aqui,
    desacoplando o contrato de rede do shape interno dos casos de uso
  - `ImportarPlantaCampos` — constantes com os nomes dos campos do formulário multipart de
    `POST /api/plantas`, para não duplicar strings mágicas entre a Api (`[FromForm(Name = ...)]`) e
    o cliente HTTP do app Mobile
- [ ] Versionamento simples de contrato (namespace/pasta por versão) — não necessário ainda (só uma
      versão de contrato existe); adiado até haver motivo real para versionar
- [x] Verificado: build e testes passando

## Fase 6 — BellucSketch.Mobile (.NET MAUI / Android) — **CONCLUÍDA**
- [x] **Split em dois projetos** (feito ao perceber que um app MAUI "Exe" (`net8.0-android`) não
      pode ser referenciado por um projeto de teste comum):
  - `BellucSketch.Mobile.Core` (`net8.0`, biblioteca "plain"): ViewModels, `IApiClient`/`ApiClient`,
    `ITokenStore` (interface), `PlantaOverlayRenderer` (SkiaSharp puro) — tudo testável em xUnit
    comum, sem Android/emulador
  - `BellucSketch.Mobile` (`net8.0-android`, escafoldado com `dotnet new maui`): Views (XAML),
    `MauiProgram.cs`, `AppShell`, conversores XAML, `PlantaCanvasView` (toque/desenho, conecta o
    renderer do Core a um `SKCanvasView`), `TokenStoreSecureStorage` (implementação concreta usando
    `SecureStorage` do MAUI Essentials — só podia viver aqui, não no Core)
- [x] Estrutura MVVM (CommunityToolkit.Mvvm — `[ObservableProperty]`/`[RelayCommand]`)
- [x] Serviço HTTP (`ApiClient`) consumindo `BellucSketch.Api` via `BellucSketch.Contracts`, com
      `TokenAuthHandler` (anexa o Bearer token automaticamente) e `ConfiguracaoApi.BaseUrl`
      configurável (endereço da intranet/rede local)
- [x] Tela de login (`LoginPage`/`LoginViewModel`) — usa o `POST /api/auth/dev-token`
- [x] Tela de lista de Projetos (`ProjetosPage`) com criar/editar/excluir, botão "Relatório" no
      canto superior (gera o changelog em PDF) e assinatura "developed by Samuel Miguel" no rodapé
- [x] Dentro de cada projeto, lista de Plantas (`PlantasDoProjetoPage`) com importação de PDF/imagem
      via `FilePicker` (nome, descrição opcional, nome do cliente opcional) e miniatura da planta já
      composta com todas as camadas, ao lado oposto do nome
- [x] Tela principal da planta (`PlantaPage`): canvas ocupando a maior parte da tela (proporção 7:2
      contra a lista de camadas, pra não cortar a visualização), desenhando a imagem da planta + o
      traço raster de cada camada visível, nessa ordem; lista de camadas com botões ▲/▼ pra mudar a
      prioridade (renumerada em ordem crescente pelo servidor), alternar visível/bloqueada
- [x] Tela de edição isolada de camada (`CamadaEdicaoPage`): mostra só a camada sendo editada, toque
      para desenhar/apagar (cor por paleta, espessura numa escala própria — até 120 no modo
      borracha, contra 24 no traço normal —, alternância apagar via `SKBlendMode.Clear`, "Limpar
      camada", "Salvar camada")
- [x] Tela de histórico (`HistoricoPage`)
- [x] Relatório de atualizações em PDF (`BellucSketch.Mobile.Core/Relatorios/`): changelog versionado
      (`HistoricoVersoes`, ordem crescente a partir de 1.0) renderizado com QuestPDF
      (`RelatorioPdfService`), aberto no visualizador padrão do Android via `Launcher`
- [x] Testes de ViewModel, do renderer e do relatório PDF (`tests/BellucSketch.Mobile.Core.Tests`)
- [x] **Build real do APK Android verificado** (Android SDK + JDK instalados neste ambiente) — build
      **Release** assinado (`com.companyname.camdas.mobile-Signed.apk`), instalado e testado num
      Samsung Galaxy A15 físico via `adb install`
- [x] Verificado: `dotnet test BellucSketch.sln` — passando

### Simplificações conscientes desta fase (não são bugs, são escopo deliberadamente reduzido)
- **Sem undo por traço nem zoom/pan no canvas de desenho** — resolvido depois, ver "Backlog futuro"
  em TASKS.md (undo por traço e zoom/pan com resolução nativa).
- **Login continua sendo o `dev-token`** da Fase 4 — a tela de login já está pronta para trocar de
  mecanismo assim que existir um endpoint de login por credencial de verdade (só a chamada dentro de
  `LoginViewModel.EntrarAsync` muda).

### Bugs/gotchas reais encontrados e corrigidos durante a Fase 6
- **Toolchain Android ausente neste ambiente.** Só o workload `maui-android` (`dotnet workload
  install`) não é suficiente — falta o Android SDK (adb/build-tools/platform) e um JDK, que a
  Microsoft normalmente instala via Visual Studio. Resolvido instalando manualmente: OpenJDK 17
  (`winget install Microsoft.OpenJDK.17`), Android command-line tools (baixadas do repositório
  oficial do Google), aceite de licenças via `sdkmanager --licenses`, e instalação de
  `platform-tools`, `platforms;android-34`, `build-tools;34.0.0` — depois apontado via
  `-p:AndroidSdkDirectory=...` no build.
- **Build falhando com `XARDF7024: Access to the path ... is denied` num diretório dentro de
  `obj/`.** O repositório está dentro de uma pasta sincronizada pelo OneDrive — builds Android geram
  muitos arquivos pequenos (assets/recursos) rapidamente, e o OneDrive prendia um lock passageiro em
  algum deles no meio do build. Corrigido limpando `obj/`/`bin/` e rodando de novo (lock era
  transitório); se persistir em outra máquina, mover `BaseIntermediateOutputPath`/`BaseOutputPath`
  para fora de uma pasta sincronizada é o próximo passo.
- **Tráfego HTTP bloqueado por padrão no Android (API 28+).** A Api roda em HTTP simples na
  intranet (sem HTTPS ainda), e o Android bloqueia "cleartext traffic" por padrão a partir da API
  28 — todas as chamadas da Api falhariam silenciosamente num dispositivo/emulador real. Corrigido
  com `android:usesCleartextTraffic="true"` no `AndroidManifest.xml`, com nota de que o ideal em
  produção é publicar a Api com HTTPS (CA interna) e remover essa permissão.
- **`IsVisible` bindado direto a um objeto ou a uma string sem conversor compatível.**
  `InvertedBoolConverter` só sabe inverter `bool`; usá-lo (ou o conversor de string) contra uma
  referência de objeto simplesmente não funciona (o binding não lança erro de compilação, mas o
  `IsVisible` fica incorreto em runtime). Corrigido criando um conversor dedicado
  (`ObjetoNuloParaBoolConverter`, com `ConverterParameter="inverso"` para o caso contrário) antes de
  isso virar um bug silencioso.
- **`StringFormat` com seções tipo `'{0:Visível;Oculta}'` não funciona para `bool`.** Esse
  truque de formatação condicional com `;` é só para tipos numéricos (positivo;negativo;zero) — para
  `bool`, o MAUI simplesmente ignora o format string. Corrigido com um conversor próprio
  (`BoolParaTextoConverter`, com `ConverterParameter="TextoSeVerdadeiro;TextoSeFalso"`).
- **APK instalado manualmente (fora do Visual Studio) abre e fecha na hora, com `Abort message: 'No
  assemblies found in .../.__override__'`.** Builds **Debug** do .NET MAUI usam "Fast Deployment":
  o APK não embute as DLLs geradas, espera que a ferramenta de deploy (VS/`dotnet build -t:Install`)
  as envie separado ao instalar. Sideload manual (copiar o `.apk` e instalar direto) não faz isso, e
  o app trava na inicialização. Corrigido gerando um build **Release** (`-c Release`), que embute
  todos os assemblies no próprio APK — instalável em qualquer aparelho sem mais nada.
- **`Shell.Current.GoToAsync("//ProjetosPage")` derrubava o app com `FATAL EXCEPTION` logo após o
  login.** `"//"` é navegação **absoluta**, só válida para telas declaradas como raiz
  (`ShellContent`) no `AppShell.xaml` — só `LoginPage` é raiz; `ProjetosPage` e as demais são rotas
  registradas via `Routing.RegisterRoute` para navegação **relativa** (empilhada). Corrigido trocando
  para `Shell.Current.GoToAsync(nameof(ProjetosPage))` (sem `"//"`), mesmo padrão já usado nas
  outras telas do app. Diagnosticado lendo o log de crash real do Android (`adb logcat`) com o
  aparelho conectado via USB — a mensagem de exceção apontou a causa direto.
- **`ConfiguracaoApi.BaseUrl` fixo em `http://10.0.2.2:5000/` não funciona em celular físico.**
  `10.0.2.2` é um alias especial que só existe dentro do emulador Android (aponta pro `localhost` da
  máquina host); um celular de verdade precisa do IP da máquina na rede Wi-Fi (`ipconfig`/
  `Get-NetIPAddress`), com celular e PC na mesma rede, a Api ligada em todas as interfaces
  (`dotnet run --urls http://0.0.0.0:5080`, não só `localhost`) e uma regra de firewall liberando a
  porta de entrada.

## Fase 7 — Hardening e entrega
- [x] Logging estruturado (Serilog) na API — console + arquivo (`logs/camdas-.log`, rolling diário,
      14 dias de retenção) configurados via `appsettings.json` (seção `Serilog`).
      `app.UseSerilogRequestLogging()` loga cada requisição HTTP (método, rota, status, duração);
      `TratadorDeExcecoesGlobal` loga exceções — `Warning` para regra de negócio/validação (esperado,
      já tratado), `Error` com stack trace completo só para os 500 (erro realmente inesperado)
- [x] Testes end-to-end do fluxo completo (`tests/BellucSketch.Api.Tests/PlantaFluxoCompletoTests.cs`):
      importar planta → `GET arquivo` devolve os bytes salvos → adicionar cota → `PUT`/`GET imagem`
      de camada devolve exatamente o PNG enviado → histórico reflete a timeline na ordem certa
- [x] Guia de deploy na intranet (IIS/serviço Windows/contêiner interno) —
      [GUIA_DEPLOY_INTRANET.md](GUIA_DEPLOY_INTRANET.md)
- [x] Guia de instalação do app Android (APK interno, sem Google Play) —
      [GUIA_INSTALACAO_ANDROID.md](GUIA_INSTALACAO_ANDROID.md)
- [x] Revisão final de segurança (JWT, HTTPS interno) —
      [REVISAO_SEGURANCA.md](REVISAO_SEGURANCA.md). Achado real corrigido: chave JWT placeholder
      commitada no `appsettings.json` agora bloqueia a Api de subir fora de `Development` (ver
      `Program.cs`); HTTPS interno documentado como recomendação (depende de infraestrutura/CA que
      não existe neste ambiente — fica junto do guia de deploy, ainda pendente)

## Fase 8 — Limpeza e correções reportadas em teste no aparelho — **CONCLUÍDA**
- [x] Removida por completo a entidade `Cota` (e `Ponto2D`/`Medida`/`UnidadeMedida`) — sem nenhum
      consumidor na UI atual. Removida em todas as camadas
      (Domain/Application/Infrastructure/Api/Contracts/Mobile), migration recriada do zero (6
      tabelas em vez de 7)
- [x] Apagados os arquivos de teste que só cobriam Cota/`ValueObjects`; os demais ajustados para as
      novas assinaturas (`CamadaDto` sem lista de Cotas, `PlantaOverlayRenderer` só com raster)
- [x] Botão de arrastar (drag-and-drop) trocado por botões ▲/▼ de subir/descer — o
      `DragGestureRecognizer` nativo do MAUI não respondia de forma confiável a toque no Android
- [x] Layout da tela principal da planta rebalanceado (canvas com peso 7x contra 2x da lista de
      camadas) — a planta estava sendo cortada na visualização geral
- [x] Espessura da borracha numa escala própria e maior (até 120, contra 24 do traço normal)
- [x] Miniatura da planta (base + todas as camadas compostas) na lista de plantas do projeto, do
      lado oposto ao nome
- [x] Relatório de atualizações em PDF, acessível por um botão no canto superior da aba Projetos,
      com changelog versionado a partir de 1.0 (dia/hora de cada atualização e bugs corrigidos em
      teste)
- [x] Verificado: `dotnet build BellucSketch.sln` e `dotnet test BellucSketch.sln` — 62/62 testes passando

### Fase 8.1 — Correções reportadas após publicar a Api na nuvem (Render + Supabase)
- [x] Excluir camada (Domain já tinha `Planta.RemoverCamada`; faltava o caso de uso
      `RemoverCamadaCommand`, endpoint `DELETE /api/plantas/{id}/camadas/{id}` e o botão 🗑 na UI —
      Mobile e Web)
- [x] Excluir planta (`RemoverPlantaCommand`, `DELETE /api/plantas/{id}`, botão "Excluir" na lista de
      plantas do projeto — Mobile e Web)
- [x] **Bug real**: depois de tocar num projeto/planta já selecionado (ex.: voltar da tela e tocar de
      novo no mesmo item), o `CollectionView` não abria mais — só entrando em outro item antes.
      Causa: `SelectionChangedCommand` só dispara quando a seleção *muda*; tocar no item já
      selecionado não conta como mudança. Corrigido trocando por `TapGestureRecognizer` por item
      (mesmo padrão já usado na lista de camadas), que dispara sempre, em `ProjetosPage` e
      `PlantasDoProjetoPage`
- [x] Verificado: `dotnet build BellucSketch.sln`, build do projeto Android e `dotnet test BellucSketch.sln` —
      63/63 testes passando

### Fase 8.2 — Fluxo de edição colaborativa (Web solicita, Android aprova)
- [x] Novos casos de uso (`Application/EdicoesPendentes/`): `SolicitarEdicaoCamada`,
      `ListarEdicoesPendentes`, `AprovarEdicaoCamada`, `RejeitarEdicaoCamada` — entidade
      `EdicaoPendenteCamada` (status `Pendente|Aprovada|Rejeitada`) e enum
      `TipoOperacaoEdicaoPendente` (visibilidade/opacidade/bloqueio/reordenar/excluir); endpoint
      `EdicoesPendentesController` (`POST`/`GET .../edicoes-pendentes`, `POST .../aprovar`,
      `POST .../rejeitar`)
- [x] `IPlataformaEdicao` (porta client-side, sem equivalente na Api): decide se um tipo de operação
      precisa de aprovação antes de aplicar. `PlataformaEdicaoDireta` (Mobile/Android, "mestre") —
      sempre aplica direto, nunca pede aprovação. `PlataformaEdicaoWeb` (Blazor) — hoje só exige
      aprovação para **excluir camada** (visibilidade/opacidade/bloqueio/ordem continuam livres na
      Web, por não apagarem o traço); pede responsável/motivo via `prompt()` do navegador
- [x] Tela `RevisaoEdicoesPage` no Mobile — lista as edições pendentes de uma planta, aprovar aplica
      a mudança de verdade no mestre, rejeitar exige motivo
- [x] Indicador visual (⏳) na Web (`Planta.razor`) na camada/planta com edição pendente
- [x] Camada ganhou também: `Opacidade` (slider, `DefinirOpacidadeCamada`), `BloqueioAlpha`
      (`BloquearAlphaCamada`/`DesbloquearAlphaCamada` — trava a transparência do traço já pintado,
      independente do bloqueio normal) e `DuplicarCamada` (copia nome/opacidade/visibilidade/traço
      para uma camada nova logo abaixo)
- [ ] Sem teste automatizado novo para este fluxo (verificado manualmente Web ↔ Android) — considerar
      cobertura de integração se o fluxo crescer

## Fase 9 — Upgrade para SkiaSharp 3.x — **CONCLUÍDA**
- [x] `SkiaSharp`/`SkiaSharp.Views.Maui.Controls` para 3.119.4 e `BellucSketch.Mobile` de
      `net8.0-android` para `net9.0-android`
- [x] Corrigidos 4 crashes nativos (SIGSEGV) só reproduzíveis em aparelho físico — detalhe completo
      de cada um em [RELATORIO.md](RELATORIO.md), Fase 9 (superfície 0×0, bitmap liberado/inválido,
      zoom alto estourando limite de memória do Android, hover da S Pen)
- [ ] Sem teste automatizado novo (crashes nativos dependentes de timing/hardware, verificados
      manualmente num Samsung Galaxy Tab A físico)

## Fase 10 — Pan/zoom por gesto, ferramenta de ícones técnicos e limpeza de config obsoleta — **CONCLUÍDA**
- [x] Traço com curva suave (Bézier quadrática), trava do traço/texto dentro dos limites da planta,
      padrão pan/desenho invertido (toque só ajusta a visualização por padrão; um botão liga o
      desenho) e zoom por pinça de dois dedos reimplementado sobre o toque bruto do SkiaSharp (ver
      RELATORIO.md, Fase 10, para a "saga" de três iterações até funcionar de forma confiável)
- [x] Resolução do PDF importado especificada em 300 DPI (antes usava o padrão da lib, baixo demais)
- [x] Ferramenta de ícones técnicos (água fria/quente, esgoto, interruptor, ponto de gás, tomada
      simples/dupla) — SVGs como `MauiAsset`, renderizados via `Svg.Skia`; ícone confirmado grava
      sempre numa camada especial "Ícones" (criada automaticamente, olho sempre travado ligado) —
      **limitação conhecida**: ícone não entra no desfazer/refazer (esse mecanismo só reconstrói a
      camada ativa; corrigir um ícone errado hoje é girar/redimensionar antes de confirmar ou
      "Limpar camada" na própria Ícones)
- [x] Destaque visual da camada em edição na lista de camadas
- [x] Configuração de servidor obsoleta removida (`ResolvedorEnderecoApi` e afins) —
      `ConfiguracaoApi.BaseUrl` virou uma constante fixa apontando pro Render (ver Fase 8.1); o app
      não pergunta mais IP/porta na primeira abertura
- [ ] Sem teste automatizado novo — verificado manualmente num Samsung Galaxy Tab A físico
- [~] **Pendência conhecida, ainda não corrigida**: `tests/BellucSketch.Mobile.Core.Tests/ViewModels/
      PlantaViewModelTests.cs:102` referencia `PlantaViewModel.CamadaSelecionadaParaEdicao`,
      propriedade renomeada em commit anterior para `CamadaEmEdicaoId` — quebra `dotnet build
      BellucSketch.sln`/`dotnet test BellucSketch.sln` (a solução inteira), mesmo com
      `BellucSketch.Mobile.csproj` isolado compilando limpo

## Fase 11 — Ferramenta de seleção de cota (OCR) — **CONCLUÍDA**
- [x] "Próximo passo combinado" ao fim da Fase 10 (ver RELATORIO.md) — implementada em sessão(ões)
      não documentada(s) fase a fase no momento; primeira aparição no histórico de commits é junto do
      rename para BellucSketch (Fase 12), então fica registrada retroativamente aqui
- [x] Fluxo: usuário liga o modo cota (botão dedicado, desliga pan) e arrasta um retângulo sobre o
      número impresso na planta (`PlantaCanvasView.ModoSelecaoCota`/`GerenciarSelecaoCota`) → recorta
      a composição nativa daquela área → detecta cor de fundo e a área de tinta do texto
      (`DetectarCorDeFundo`/`DetectarAreaTexto`, mantendo de fora pixels vermelhos — linha de cota —
      da máscara de cobertura, pedido explícito do usuário) → `OcrTextoService.ReconhecerAsync`
      (Google ML Kit Text Recognition, modelo "Bundled" embutido no apk, funciona offline) reconhece
      o texto → prompt nativo mostra o texto reconhecido, editável → confirmando, cria um texto
      pendente (mesma barra de posicionar/girar/A-/A+/confirmar/cancelar da ferramenta de texto) já
      com a máscara de cobertura do número original anexada (`AreaCobertura`/`MascaraCobertura` em
      `AcaoTexto`) e cor vermelha se o usuário editou o valor reconhecido (preta se manteve)
- [x] Cobertura via máscara pixel-a-pixel (não um retângulo cheio) — ver bug corrigido na Fase 12
      (reedição vazava o número original de volta)
- [ ] Sem teste automatizado (depende de ML Kit/Android real) — verificado manualmente no aparelho

## Fase 12 — Rebranding para BellucSketch, correção de vazamento de storage e ajustes de UX (2026-07-23) — **CONCLUÍDA**
- [x] Projeto inteiro renomeado de Camdas para **BellucSketch** — solução, namespaces, pastas
      `src`/`tests`, ícone do app, telas de fundo (login/projetos/plantas). `ApplicationId`
      (`com.companyname.camdas.mobile`) e a chave `ConnectionStrings:Camdas`/nomes dos serviços no
      Render (`camdas-api`/`camdas-web`) deliberadamente **não** renomeados (ver comentários em
      `render.yaml`/`Program.cs` — reinstalação forçada e segredo `sync: false` já preenchido
      manualmente no painel, respectivamente)
- [x] Corrigido vazamento de arquivos órfãos no Supabase Storage: `IArquivoStorage.ExcluirAsync`
      (novo) chamado ao salvar nova imagem de camada, limpar camada, remover camada e remover planta
      — antes, cada "Salvar camada" deixava o arquivo anterior perdido no bucket para sempre
- [x] Removida a transição de splash (`MauiSplashScreen`) — o card de marca já é fundo persistente
      das telas principais, uma splash separada era redundante
- [x] Cobertura de texto/cota na reedição: `ApagarTextoDoBitmap`/`IniciarEdicaoTextoColocado`
      corrigidos para não perder a máscara de cobertura ao reeditar um texto já colocado — o número
      original impresso na planta só volta a aparecer via desfazer (↶), nunca reabrindo o texto pra
      editar
- [x] Arrastar texto/ícone pendente até uma lixeira flutuante para excluir (antes só dava pra
      confirmar/cancelar a posição)
- [x] Rastro da borracha na prévia ao vivo deixou de ficar preto (era `BlendMode.Clear` sem nada por
      baixo na superfície já composta) — agora mostra um traço branco translúcido só como indicador
- [x] Exclusividade real entre ferramentas (lápis/texto/cota desligam a borracha ao serem
      selecionadas, e vice-versa)
- [x] Verificado: build completo da solução (`dotnet build BellucSketch.sln`) e do app Android
      (`-f net9.0-android -c Release`) limpos, exceto a pendência já conhecida da Fase 10
      (`PlantaViewModelTests.cs`); instalado e testado num Samsung Galaxy Tab A físico

## Fase 13 — Ferramenta de desenho na Web (BellucSketch.Web deixa de ser só visualizador) — **CONCLUÍDA**
- [x] Removido o fluxo de aprovação da Web (`PlataformaEdicaoWeb` apagado, registrado
      `PlataformaEdicaoDireta` — a mesma classe já usada pelo Android): toda edição na Web (inclusive
      excluir camada) aplica direto, sem pedir responsável/motivo. O backend (`EdicaoPendenteCamada`,
      migrations, `EdicoesPendentesController`, `RevisaoEdicoesPage` no Android) não foi removido —
      fica dormente, decisão de limpeza maior deixada de fora deste escopo
- [x] Ícones técnicos movidos de `src/BellucSketch.Mobile/Resources/Raw/Icones` para
      `assets/icones-tecnicos` (raiz do repo) — fonte única compartilhada por Android e Web, sem
      duplicar bytes versionados
- [x] `PlantaCanvasEdicaoWeb` (`src/BellucSketch.Web/Rendering/`) — motor de desenho novo e próprio da
      Web (não uma refatoração do `PlantaCanvasView` do Android, pra não arriscar regressão no app já
      publicado), capturando ponteiro do navegador (`@onpointerdown/move/up`) em vez de touch nativo
      do MAUI: traço livre com suavização Bézier, borracha, estilos de linha (reta contínua/
      pontilhada/tracejada), texto, ícones técnicos e undo/redo (histórico de ações + replay sobre
      snapshot-base da camada, mesma estratégia do Android)
- [x] Ferramenta de cota com OCR na Web via **Tesseract.js** vendorizado (sem CDN,
      `wwwroot/lib/tesseract`) — equivalente ao Google ML Kit do Android, atrás de uma interface nova
      compartilhada `IOcrService` (`Mobile.Core/Services`), implementada por `OcrTextoService`
      (Android, sem mudar comportamento) e por `OcrServicoWeb` (Web, novo)
- [x] Simplificações deliberadas frente ao Android (cortes de escopo, não bugs): a Web não cobre
      automaticamente o número antigo detectado pelo OCR (o Android usa detecção de cor de
      fundo/máscara pixel-a-pixel — ver Fase 11); não há "segurar pra pegar de volta" um ícone/texto
      já confirmado pra reeditar; a tela cheia continua só de visualização (editar fica só no painel
      normal)
- [x] Bug encontrado e corrigido durante o teste manual: ícones registrados via
      `<Content Include="..." LinkBase="wwwroot\icones">` apareciam no manifesto de static web assets
      mas voltavam 404 no servidor de desenvolvimento (`dotnet run`) — o manifesto de dev assume que
      todo asset raiz mora fisicamente dentro do `wwwroot` do próprio projeto. Corrigido copiando de
      fato os SVGs pra `wwwroot/icones` num target de build (`CopiarIconesTecnicosCompartilhados`,
      `BellucSketch.Web.csproj`) — pasta gerada, no `.gitignore`, fonte real continua em
      `assets/icones-tecnicos`
- [x] **Bug real encontrado ao testar no navegador (reportado pelo usuário) e corrigido: planta
      aparecia cortada/pequena em qualquer zoom.** Causa raiz: `SkiaSharp.Views.Blazor.SKCanvasView`
      **não tem** propriedades `WidthRequest`/`HeightRequest` (isso só existe na versão MAUI/Android
      do mesmo nome) — confirmado lendo o código-fonte oficial da versão instalada (3.119.4) direto
      do repositório do SkiaSharp. Usá-las (como o código já fazia, inclusive no SKCanvasView de
      só-visualização — bug pré-existente, não introduzido nesta fase) vira um atributo HTML sem
      efeito nenhum (só é "aceito" por existir um `[Parameter(CaptureUnmatchedValues=true)]
      AdditionalAttributes` que absorve qualquer atributo desconhecido); o tamanho real do canvas
      vem do **CSS** (`style="width;height"`), observado via `ResizeObserver`. Sem isso, o canvas
      sempre caía no tamanho padrão do HTML (300×150), cortando a composição e ignorando o zoom por
      completo. Corrigido nos três `SKCanvasView` da Web (visualização normal, tela cheia e o novo
      `PlantaCanvasEdicaoWeb`), trocando `WidthRequest`/`HeightRequest` por `style="width:...px;
      height:...px;"` de verdade
- [x] Verificado: `dotnet build` de `BellucSketch.Web` e de `BellucSketch.Mobile` (`-f net9.0-android`)
      limpos; roundtrip completo de upload/download de imagem de camada via API local (mesmo endpoint
      `PUT`/`GET .../camadas/{id}/imagem` que o novo motor usa) validado por `curl`; assets estáticos
      novos (ícones, Tesseract.js, dados de treinamento) servidos com 200 pelo dev server
- [ ] **Sem teste de navegador de verdade automatizado** — este ambiente não tem `chromium-cli`/Node+Playwright
      disponível, então a interação real (arrastar o mouse pra desenhar, undo/redo clicado, texto/
      ícone posicionado e confirmado, OCR rodando de fato no navegador) não foi exercitada
      automaticamente; precisa de verificação manual num navegador antes de considerar esta fase
      validada de ponta a ponta

---

## Backlog futuro (fora do MVP, não priorizado)
- [ ] Login por credencial de verdade (hash de senha) — o `dev-token` é só placeholder de dev/teste
- [ ] Importação/edição nativa de DWG/DXF
- [ ] Modo offline com fila de sincronização
- [x] Undo por traço (não só "limpar camada" inteira) — teve idas e vindas (implementado, revertido a
      pedido, reimplementado) em commits não documentados fase a fase; **estado atual**: `Desfazer`/
      `Refazer` (↶/↷) funcionam para traço e texto/cota (`PlantaCanvasView._historico`/`_desfeitas`),
      mas **não** para ícone (ver Fase 10 — grava direto e permanente, fora desse histórico)
- [x] Zoom/pan no canvas de desenho mantendo a resolução nativa do traço — implementado nesta forma
      (bitmap por camada no tamanho nativo + `ScrollView`) e depois **substituído** na Fase 10 por
      canvas de tamanho fixo com `PanX`/`PanY` e zoom por pinça, sem `ScrollView` (o mecanismo antigo
      não convivia bem com o gesto de 2 dedos — ver Fase 10 acima)
