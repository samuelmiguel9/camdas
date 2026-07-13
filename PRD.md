# PRD — Camdas | Ratificação de Projetos de Planta Baixa

> **Pivot**: este PRD descreve a visão *original* do produto — ratificação formal com fluxo de
> revisão/aprovação por perfil e versionamento. Essa direção foi abandonada: o app hoje é uma
> ferramenta de desenho livre (importar planta, rabiscar/escrever por cima em camadas que
> ligam/desligam, tipo Paint), sem revisão/aprovação/perfil de usuário. Mantido como registro
> histórico da decisão original — para o estado atual, ver [TASKS.md](TASKS.md) e
> [RELATORIO.md](RELATORIO.md), e o código em `src/Camdas.Domain`.

## 1. Visão Geral

Aplicativo corporativo para **ratificação (revisão e aprovação) de projetos de planta baixa**, permitindo que
projetistas insiram, editem e organizem **cotas** em **camadas independentes** (hidráulica, elétrica, mobiliário,
estrutura e cotas de arquitetura), com um fluxo formal de **revisão e aprovação por um técnico responsável**.

O sistema é composto por:

- Um **backend (.NET / ASP.NET Core Web API)** hospedado na **intranet da empresa**, responsável pelas regras de
  negócio, persistência, versionamento e controle de acesso.
- Um **aplicativo Android (.NET MAUI)** usado pelos projetistas e técnicos em campo/obra ou escritório, que se
  conecta à API via Wi-Fi/VPN corporativa.

## 2. Problema a resolver

Hoje a conferência de cotas em plantas baixas (hidráulica, elétrica, mobiliário, estrutura, arquitetura) é feita de
forma manual e fragmentada, sem controle de versões, sem rastreabilidade de quem alterou o quê, e sem um fluxo formal
de aprovação técnica antes de uma planta ser considerada "ratificada" (pronta para execução).

## 3. Objetivos

1. Permitir importar uma planta baixa (PDF ou imagem) como base de trabalho.
2. Organizar cotas em **camadas independentes**, cada uma podendo ser **ligada/ocultada/bloqueada/editada** sem afetar
   as demais.
3. Registrar **histórico de alterações** (quem, quando, o quê) e manter **versões** da planta.
4. Formalizar um **fluxo de revisão e aprovação** conduzido por um técnico habilitado, com possibilidade de
   aprovação/rejeição com comentários.
5. Disponibilizar tudo isso em um **app Android** operando dentro da **rede intranet** da empresa.

## 4. Personas

| Persona | Perfil | Necessidades |
|---|---|---|
| **Projetista** | Cria/edita cotas nas camadas, importa plantas | Editar rápido, sem derrubar o trabalho de outra disciplina |
| **Técnico responsável** | Revisa e aprova/rejeita versões | Ver o que mudou, aprovar com segurança, registrar parecer |
| **Administrador** | Gerencia usuários, projetos e permissões | Controlar quem acessa o quê |

## 5. Escopo funcional (MVP)

### 5.1 Projetos e Plantas
- CRUD de **Projeto** (nome, descrição, responsável, status geral).
- Importação de **Planta** a partir de **PDF ou imagem** (jpg/png), vinculada a um Projeto.
- Cada importação bem-sucedida gera a **versão 1** da planta.

### 5.2 Camadas (Layers)
- Camadas fixas por tipo: `Hidraulica`, `Eletrica`, `Mobiliario`, `Estrutura`, `CotasArquitetura` (extensível a novos
  tipos no domínio sem alterar as demais — princípio Aberto/Fechado).
- Cada camada tem estado independente: **Visível** (on/off), **Bloqueada** (impede edição) e cor de identificação.
- Alternar visibilidade não exige permissão especial; **bloquear/desbloquear** e **editar conteúdo de camada
  bloqueada** exigem regra de negócio explícita (ver seção de regras).

### 5.3 Cotas (Dimensions)
- Uma Cota pertence a exatamente uma Camada.
- Atributos: ponto inicial, ponto final, valor da medida, unidade, rótulo/observação.
- Cota só pode ser criada/editada/removida se a camada estiver **desbloqueada** e a planta **não estiver em
  revisão aprovada final** (regra detalhada abaixo).

### 5.4 Versionamento e Histórico
- Toda alteração relevante (criar/editar/remover cota, bloquear/desbloquear camada, importar nova planta) gera um
  registro de **HistoricoAlteracao** (quem, quando, ação, dados antes/depois).
- Uma nova **Versão** é criada sempre que uma revisão é **aprovada** (congela o estado atual) ou quando o usuário
  decide explicitamente "publicar nova versão".
- É possível consultar versões anteriores (somente leitura).

### 5.5 Fluxo de Revisão e Aprovação
1. Projetista conclui os ajustes e **solicita revisão** da planta (status: `Pendente`).
2. Técnico responsável analisa as cotas/camadas e:
   - **Aprova** → gera nova versão "ratificada", planta pode voltar a ser editada normalmente (nova rodada) ou ser
     arquivada como definitiva, conforme o Projeto.
   - **Rejeita** → registra comentário/motivo, planta volta ao status `EmEdicao` para ajustes.
3. Um técnico **não pode aprovar uma revisão que ele mesmo solicitou/criou como projetista da mesma alteração**
   (regra de segregação de função, configurável).

### 5.6 Autenticação e Perfis
- Login via API (JWT). Perfis: `Projetista`, `Tecnico`, `Administrador`.
- Apenas usuários com perfil `Tecnico` ou `Administrador` podem aprovar/rejeitar revisões.
- Apenas `Administrador` gerencia usuários e projetos no nível de acesso.

## 6. Requisitos não funcionais

- **Arquitetura limpa e modular** (Clean Architecture), independente de framework de UI ou banco de dados.
- **SOLID** aplicado nas entidades e casos de uso (ex.: novos tipos de camada ou regras de aprovação não devem exigir
  alterar código existente, apenas estender).
- **Rede intranet**: API não exposta à internet pública; app Android consome via IP/hostname interno (Wi-Fi
  corporativo ou VPN).
- **Testabilidade**: regras de domínio e casos de uso cobertos por testes automatizados (xUnit), sem dependência de
  banco de dados real ou UI.
- **Auditabilidade**: nada é apagado fisicamente do histórico; remoções são lógicas (soft delete) quando aplicável.
- **Extensibilidade**: novos tipos de camada, novos formatos de importação (ex.: DWG/DXF no futuro) devem ser
  plugáveis sem reescrever o núcleo do domínio.

## 7. Arquitetura (visão geral)

Clean Architecture em círculos concêntricos — dependências sempre apontam para dentro (Domain no centro):

```
                ┌───────────────────────────────────────────┐
                │        Camdas.Api (ASP.NET Core)           │
                │        Camdas.Mobile (.NET MAUI/Android)   │   <- Apresentação / Entrada
                └───────────────────┬─────────────────────────┘
                                    │ usa
                ┌───────────────────▼─────────────────────────┐
                │         Camdas.Application                  │   <- Casos de uso, DTOs,
                │  (Use Cases, Interfaces/Ports, Validators)   │      orquestração, regras de aplicação
                └───────────────────┬─────────────────────────┘
                                    │ usa
                ┌───────────────────▼─────────────────────────┐
                │            Camdas.Domain                     │   <- Entidades, VOs, regras de
                │  (Entities, Value Objects, Domain Rules)     │      negócio puras, zero dependências
                └───────────────────────────────────────────────┘
                                    ▲
                                    │ implementa interfaces definidas na Application
                ┌───────────────────┴─────────────────────────┐
                │        Camdas.Infrastructure                 │   <- EF Core, Repositórios,
                │  (EF Core, Storage de arquivos, PDF/Imagem)  │      armazenamento de arquivos
                └───────────────────────────────────────────────┘
```

`Camdas.Contracts` é uma biblioteca fina de DTOs de request/response (e reaproveita enums do Domain, que não têm
dependências) compartilhada entre `Camdas.Api` e `Camdas.Mobile`, permitindo tipagem forte na comunicação HTTP sem
acoplar o app móvel às camadas de Application/Infrastructure/EF Core.

## 8. Estrutura de pastas

```
projeto_camdas/
├── PRD.md
├── TASKS.md
├── Camdas.sln
├── src/
│   ├── Camdas.Domain/                # Entidades, VOs, enums, regras de negócio puras (zero dependências)
│   │   ├── Common/                   # Entity base, exceções de domínio
│   │   ├── Enums/
│   │   ├── ValueObjects/
│   │   └── Entities/
│   │
│   ├── Camdas.Application/           # Casos de uso, portas (interfaces), DTOs, validações
│   │   ├── Abstractions/             # IProjetoRepository, IUnitOfWork, IArquivoStorage, IUsuarioContext...
│   │   ├── Projetos/
│   │   ├── Plantas/
│   │   ├── Camadas/
│   │   ├── Cotas/
│   │   └── Revisoes/
│   │
│   ├── Camdas.Infrastructure/        # Implementações concretas
│   │   ├── Persistence/              # DbContext, Configurations, Migrations
│   │   ├── Repositories/
│   │   ├── Storage/                  # Armazenamento de PDF/imagem no servidor da intranet
│   │   └── Import/                   # Conversão de PDF -> imagem de base
│   │
│   ├── Camdas.Contracts/             # DTOs de request/response compartilhados API <-> Mobile
│   │
│   ├── Camdas.Api/                   # ASP.NET Core Web API (hospedada na intranet)
│   │   ├── Controllers/
│   │   ├── Auth/
│   │   └── Program.cs
│   │
│   └── Camdas.Mobile/                # App .NET MAUI (Android)
│       ├── Views/
│       ├── ViewModels/
│       ├── Services/                 # HttpClient/API client
│       └── Rendering/                # Canvas de desenho (camadas + cotas sobre a planta)
│
└── tests/
    ├── Camdas.Domain.Tests/
    ├── Camdas.Application.Tests/
    └── Camdas.Api.Tests/             # Testes de integração dos endpoints
```

## 9. Modelo de dados (domínio)

### Entidades principais

- **Usuario**: `Id, Nome, Email, Perfil (Projetista|Tecnico|Administrador), Ativo`
- **Projeto**: `Id, Nome, Descricao, CriadoPorId, DataCriacao, Status`
- **Planta**: `Id, ProjetoId, TipoArquivoOrigem (Pdf|Imagem), CaminhoArquivoOriginal, Escala, Status
  (EmEdicao|EmRevisao|Ratificada), VersaoAtual, DataImportacao`
- **Camada**: `Id, PlantaId, Tipo (Hidraulica|Eletrica|Mobiliario|Estrutura|CotasArquitetura), Nome, Visivel,
  Bloqueada, Cor, Ordem`
- **Cota**: `Id, CamadaId, PontoInicio (Ponto2D), PontoFim (Ponto2D), Valor (Medida), Rotulo, CriadoPorId,
  DataCriacao, DataAtualizacao, Removida (soft delete)`
- **Versao**: `Id, PlantaId, Numero, DataCriacao, CriadaPorId, Descricao, SnapshotJson (estado congelado de
  camadas+cotas)`
- **Revisao**: `Id, PlantaId, VersaoBaseId, SolicitanteId, TecnicoResponsavelId, Status
  (Pendente|Aprovada|Rejeitada), Comentario, DataSolicitacao, DataResposta`
- **HistoricoAlteracao**: `Id, EntidadeTipo, EntidadeId, Acao, UsuarioId, DataHora, DadosAnterioresJson,
  DadosNovosJson`

### Value Objects

- **Ponto2D**: `X, Y` (imutável, com validação de coordenadas não negativas conforme sistema de referência da planta)
- **Medida**: `Valor decimal, Unidade (m|cm|mm)`
- **CorHex**: representação validada de cor (`#RRGGBB`) para identificação visual da camada

### Regras de negócio centrais (aplicadas no Domain)

1. Uma Cota não pode ser criada/editada/removida se sua Camada estiver `Bloqueada`.
2. Uma Camada não pode ser removida se possuir Cotas ativas (deve ser esvaziada antes).
3. Uma Planta com status `Ratificada` não aceita novas Cotas diretamente — é necessário reabrir uma nova rodada de
   edição (nova versão de trabalho) primeiro.
4. Uma Revisao só pode ser `Aprovada`/`Rejeitada` por um usuário de perfil `Tecnico` ou `Administrador`.
5. O técnico que aprova uma Revisao não pode ser o mesmo usuário que a solicitou (segregação de função).
6. Aprovar uma Revisao gera automaticamente uma nova Versao (snapshot) e muda o status da Planta para `Ratificada`.
7. Rejeitar uma Revisao retorna a Planta para `EmEdicao` e exige um comentário obrigatório.

## 10. Fluxo completo (ponta a ponta)

1. **Login** no app Android (JWT contra a API na intranet).
2. **Criar/abrir Projeto** → **Importar Planta** (PDF ou imagem) → planta vira `EmEdicao`, versão 1 criada, 5 camadas
   padrão criadas (visíveis, desbloqueadas).
3. Projetista alterna **visibilidade** das camadas para focar em uma disciplina, adiciona/edita **Cotas** na camada
   ativa.
4. Projetista pode **bloquear** uma camada já finalizada para evitar edições acidentais enquanto trabalha em outra.
5. Cada ação relevante grava uma entrada no **Histórico**.
6. Quando pronto, projetista **solicita revisão** → Planta passa a `EmRevisao`.
7. Técnico abre a revisão pendente, compara com a versão anterior (opcional: diff visual), e **aprova** ou
   **rejeita** (com comentário).
8. Se aprovado → nova **Versão** é gravada, Planta vira `Ratificada`.
   Se rejeitado → Planta volta a `EmEdicao`, comentário fica disponível para o projetista.
9. Histórico e versões anteriores continuam disponíveis para consulta a qualquer momento.

## 11. Tecnologias escolhidas

| Camada | Tecnologia | Justificativa |
|---|---|---|
| Backend/API | ASP.NET Core 8 Web API | Robusto, hospedável na intranet, integra nativamente com .NET MAUI |
| Domínio/Aplicação | C# puro + MediatR + FluentValidation | Clean Architecture idiomática em .NET |
| Persistência | Entity Framework Core + SQL Server (ou PostgreSQL) | Maduro, suporta migrations, transacional |
| Import PDF | Renderização de PDF em imagem de fundo no servidor (biblioteca .NET, ex. PDFtoImage/PdfPig) | Evita depender de motor CAD completo no MVP |
| Autenticação | JWT Bearer + ASP.NET Identity (ou tabela própria de Usuario) | Simples de integrar com app mobile |
| App móvel | .NET MAUI (Android) | Compartilha C#/Domain com o backend, um único time/linguagem |
| Desenho de cotas | .NET MAUI Graphics / SkiaSharp | Canvas performático para overlay de camadas sobre a planta |
| Testes | xUnit + FluentAssertions | Padrão de mercado em .NET, legível |

## 12. Fora de escopo (MVP)

- Importação/edição nativa de DWG/DXF (fica para fase futura, mencionada como extensão possível).
- Assinatura digital certificada da aprovação (pode ser adicionada depois via integração com certificado A1/A3).
- Modo 100% offline com sincronização multi-usuário complexa (MVP assume conectividade à intranet; fila local simples
  pode ser avaliada depois).

## 13. Fases de entrega

1. **Domínio + regras de negócio** (entidades, VOs, regras, testes unitários) — *fase atual*.
2. **Application** (casos de uso, DTOs, validações, portas de infraestrutura).
3. **Infrastructure** (EF Core, repositórios, storage de arquivos, importação PDF/imagem).
4. **API** (endpoints REST, autenticação JWT, composição de DI).
5. **Mobile (.NET MAUI/Android)** (telas, consumo da API, canvas de camadas/cotas).
6. **Hardening**: testes de integração, logging, deploy na intranet, documentação final.
