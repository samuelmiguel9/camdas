# TASKS — Camdas

Backlog organizado por fases, conforme roteiro definido no [PRD.md](PRD.md). Marque `[x]` ao concluir.

Legenda de status: `[ ]` pendente · `[~]` em andamento · `[x]` concluído

---

## Fase 0 — Fundamentos do repositório
- [x] PRD.md com arquitetura, modelo de dados, fluxo e stack
- [x] TASKS.md (este arquivo)
- [x] `Camdas.sln` criada
- [x] `.gitignore` para projetos .NET/MAUI
- [x] `README.md` com instruções de build/execução

## Fase 1 — Camdas.Domain (regras de negócio puras) — **CONCLUÍDA**
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
- [x] Testes unitários (xUnit + FluentAssertions) em `tests/Camdas.Domain.Tests`: criação/remoção
      de camada, bloqueio de camada impede editar imagem raster, reordenação com renumeração
      crescente
- [x] Verificado: `dotnet build Camdas.sln` e `dotnet test tests/Camdas.Domain.Tests` — passando

## Fase 2 — Camdas.Application — **CONCLUÍDA**
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
- [x] Caso de uso — Histórico: `ObterHistoricoDaPlanta`
- [x] Validações (FluentValidation) nos comandos que recebem input externo
- [x] `RecursoNaoEncontradoException` em `Common/` — a Api mapeia para HTTP 404
- [x] Padrão MediatR (`IRequest`/`IRequestHandler`) — cada caso de uso é um arquivo único com
      Command/Query + Validator + Handler (vertical slice)
- [x] Testes de aplicação (`tests/Camdas.Application.Tests`, NSubstitute) cobrindo caminho feliz e
      propagação de `DomainException` (ex.: camada bloqueada) em atualização de imagem
- [x] Verificado: `dotnet build Camdas.sln` e `dotnet test Camdas.sln` — passando

## Fase 3 — Camdas.Infrastructure — **CONCLUÍDA**
- [x] `CamdasDbContext` (EF Core) + mapeamentos (Fluent API) para todas as entidades/VOs
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
- [x] `CamdasDbContextFactory` (`IDesignTimeDbContextFactory`) — permite `dotnet ef` funcionar sem
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
- [x] Verificado: `dotnet build Camdas.sln` e `dotnet test Camdas.sln` — passando

## Fase 4 — Camdas.Api — **CONCLUÍDA**
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
- [x] `ValidationBehavior<TRequest,TResponse>` (pipeline do MediatR, em `Camdas.Application.Common`)
      roda os validadores FluentValidation antes de cada handler
- [x] Testes de integração de endpoints (`tests/Camdas.Api.Tests`, `WebApplicationFactory` + Sqlite)
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
  `CamdasDbContext.OnModelCreating` — já que nossos Ids são sempre gerados pela própria entidade
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

## Fase 5 — Camdas.Contracts — **CONCLUÍDA**
- [x] DTOs de request/response usados por `Api` e `Mobile` — projeto novo, só depende de
      `Camdas.Domain` (entidades/enums, sem dependências)
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

## Fase 6 — Camdas.Mobile (.NET MAUI / Android) — **CONCLUÍDA**
- [x] **Split em dois projetos** (feito ao perceber que um app MAUI "Exe" (`net8.0-android`) não
      pode ser referenciado por um projeto de teste comum):
  - `Camdas.Mobile.Core` (`net8.0`, biblioteca "plain"): ViewModels, `IApiClient`/`ApiClient`,
    `ITokenStore` (interface), `PlantaOverlayRenderer` (SkiaSharp puro) — tudo testável em xUnit
    comum, sem Android/emulador
  - `Camdas.Mobile` (`net8.0-android`, escafoldado com `dotnet new maui`): Views (XAML),
    `MauiProgram.cs`, `AppShell`, conversores XAML, `PlantaCanvasView` (toque/desenho, conecta o
    renderer do Core a um `SKCanvasView`), `TokenStoreSecureStorage` (implementação concreta usando
    `SecureStorage` do MAUI Essentials — só podia viver aqui, não no Core)
- [x] Estrutura MVVM (CommunityToolkit.Mvvm — `[ObservableProperty]`/`[RelayCommand]`)
- [x] Serviço HTTP (`ApiClient`) consumindo `Camdas.Api` via `Camdas.Contracts`, com
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
- [x] Relatório de atualizações em PDF (`Camdas.Mobile.Core/Relatorios/`): changelog versionado
      (`HistoricoVersoes`, ordem crescente a partir de 1.0) renderizado com QuestPDF
      (`RelatorioPdfService`), aberto no visualizador padrão do Android via `Launcher`
- [x] Testes de ViewModel, do renderer e do relatório PDF (`tests/Camdas.Mobile.Core.Tests`)
- [x] **Build real do APK Android verificado** (Android SDK + JDK instalados neste ambiente) — build
      **Release** assinado (`com.companyname.camdas.mobile-Signed.apk`), instalado e testado num
      Samsung Galaxy A15 físico via `adb install`
- [x] Verificado: `dotnet test Camdas.sln` — passando

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
- [x] Testes end-to-end do fluxo completo (`tests/Camdas.Api.Tests/PlantaFluxoCompletoTests.cs`):
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
- [x] Removida por completo a entidade `Cota` (e `Ponto2D`/`Medida`/`UnidadeMedida`) — recurso da
      versão antiga de ratificação, sem nenhum consumidor na UI atual. Removida em todas as camadas
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
- [x] Verificado: `dotnet build Camdas.sln` e `dotnet test Camdas.sln` — 62/62 testes passando

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
- [x] Verificado: `dotnet build Camdas.sln`, build do projeto Android e `dotnet test Camdas.sln` —
      63/63 testes passando

---

## Backlog futuro (fora do MVP, não priorizado)
- [ ] Login por credencial de verdade (hash de senha) — o `dev-token` é só placeholder de dev/teste
- [ ] Importação/edição nativa de DWG/DXF
- [ ] Modo offline com fila de sincronização
- [ ] Undo por traço (não só "limpar camada" inteira) — implementado e depois **revertido a pedido**
      (só "Limpar camada" inteira, como era antes)
- [x] Zoom/pan no canvas de desenho e suporte a manter a proporção do traço entre resoluções de
      dispositivo diferentes — `PlantaCanvasView.UsarResolucaoNativa` (novo): quando ligado (Planta
      Page e CamadaEdicaoPage), o bitmap de cada camada é criado no tamanho nativo da imagem base,
      não mais no tamanho da tela; `Zoom` só controla a apresentação (`canvas.Scale`) dentro de um
      `ScrollView`, sem afetar a resolução onde o traço é armazenado
