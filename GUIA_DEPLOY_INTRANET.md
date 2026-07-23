# Guia de deploy na intranet — BellucSketch.Api

Como deixar a `BellucSketch.Api` rodando de forma permanente num servidor da empresa, em vez de depender
de alguém abrir um terminal e rodar `dotnet run` manualmente toda vez. Três caminhos, do mais
simples ao mais portável — escolha um, não precisa dos três.

> **Hoje a produção real é o Render + Supabase, não a intranet** — ver
> [GUIA_DEPLOY_RENDER.md](GUIA_DEPLOY_RENDER.md). Isso muda o que este guia consegue fazer sozinho:
> `ConfiguracaoApi.BaseUrl` (Mobile) e `ApiBaseUrl` (Web) deixaram de ser configuráveis em runtime —
> são constantes fixas apontando pro Render (ver RELATORIO.md, Fase 10). Publicar a Api aqui na
> intranet deixa ela rodando e acessível por HTTP direto (Swagger, chamadas manuais, um cliente
> próprio), mas **o app Android/Web publicado hoje não vai enxergar essa instância** — pra isso, seria
> preciso editar esse endereço no código-fonte e gerar um novo build apontando pra cá.

Pré-requisito comum aos três: siga primeiro o `README.md` (seção "Rodando a Api localmente") pra
confirmar que a Api sobe e conecta no Postgres nessa máquina antes de tentar automatizar.

## Antes de publicar: configuração de produção

Independente do caminho escolhido, **não rode em produção com os valores padrão do
`appsettings.json` versionado** — ver [REVISAO_SEGURANCA.md](REVISAO_SEGURANCA.md). Configure por
variável de ambiente (o ASP.NET Core já lê automaticamente, sem código extra):

| Variável de ambiente | Substitui |
|---|---|
| `ASPNETCORE_ENVIRONMENT=Production` | Ambiente (desliga Swagger, ativa a guarda da chave JWT) |
| `Jwt__Chave` | `Jwt:Chave` — gere uma string aleatória de 32+ caracteres |
| `ConnectionStrings__Camdas` | `ConnectionStrings:Camdas` — credenciais reais do Postgres de produção |
| `ArmazenamentoArquivos__DiretorioRaiz` | Onde os PDFs/imagens importados ficam salvos — escolha um caminho com backup |

Sem `Jwt__Chave` configurada, a Api **recusa iniciar** em `Production` (guarda adicionada na
revisão de segurança) — isso é intencional, é melhor a Api não subir do que subir insegura.

---

## Opção 1 — Windows Service (mais simples, recomendado pra começar)

Roda a Api como um serviço do Windows: liga sozinha com o servidor, reinicia sozinha se cair,
não depende de ninguém logado.

### 1.1 Publicar a Api

```powershell
dotnet publish src/BellucSketch.Api -c Release -o C:\Servicos\BellucSketchApi
```

Isso gera um `BellucSketch.Api.exe` autocontido em `C:\Servicos\BellucSketchApi` (ajuste o caminho como
preferir).

### 1.2 Configurar as variáveis de ambiente do serviço

Como serviço do Windows não herda variáveis de ambiente do seu usuário, defina-as como variáveis
**de sistema** (`Painel de Controle → Sistema → Configurações avançadas → Variáveis de Ambiente`,
seção "Variáveis do sistema") ou via PowerShell como Administrador:

```powershell
[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "Machine")
[Environment]::SetEnvironmentVariable("Jwt__Chave", "<gere uma chave aleatória aqui>", "Machine")
[Environment]::SetEnvironmentVariable("ConnectionStrings__Camdas", "Host=localhost;Database=camdas;Username=camdas;Password=<senha-real>", "Machine")
```

### 1.3 Criar o serviço

```powershell
New-Service -Name "BellucSketchApi" `
  -BinaryPathName "C:\Servicos\BellucSketchApi\BellucSketch.Api.exe --urls http://0.0.0.0:5080" `
  -DisplayName "BellucSketch Api" `
  -StartupType Automatic
Start-Service BellucSketchApi
```

### 1.4 Conferir

```powershell
Get-Service BellucSketchApi
Invoke-WebRequest http://localhost:5080/health
```

Pra atualizar depois de uma mudança de código: `Stop-Service BellucSketchApi`, publique de novo por cima
da mesma pasta, `Start-Service BellucSketchApi`.

---

## Opção 2 — IIS (se a empresa já usa IIS pra outras coisas)

Mais trabalho de configurar, mas se já existe um IIS na empresa (com outros sites internos), é
onde a equipe de infra já sabe mexer.

1. Instalar o **ASP.NET Core Hosting Bundle** no servidor (não é só o .NET runtime — é um pacote à
   parte que integra o Kestrel com o IIS): [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
   (procure "Hosting Bundle" da versão .NET 8).
2. `dotnet publish src/BellucSketch.Api -c Release -o C:\inetpub\wwwroot\BellucSketchApi`.
3. No **Gerenciador do IIS**: criar um novo site (ou aplicativo dentro de um site existente)
   apontando pra essa pasta, num Application Pool com "No Managed Code" (o ASP.NET Core não usa o
   pipeline gerenciado do IIS, só o módulo `AspNetCoreModuleV2` como proxy reverso pro Kestrel).
4. Variáveis de ambiente: no Gerenciador do IIS, no site → **Configuration Editor** →
   `system.webServer/aspNetCore` → `environmentVariables`, adicione as mesmas variáveis da seção
   acima (`Jwt__Chave`, `ConnectionStrings__Camdas`, etc.) em vez de variável de sistema.
5. Liberar a porta escolhida no binding do site e no firewall do Windows (mesmo passo do
   `README.md`, `New-NetFirewallRule`).

O IIS já dá reinício automático e logs de acesso prontos — vantagem sobre o Windows Service puro se
a equipe já monitora outros sites por ali.

---

## Opção 3 — Contêiner (Docker)

Mais portável (funciona igual em qualquer máquina com Docker, inclusive fora da empresa se um dia
precisar mover o servidor), mas exige Docker instalado no servidor — o único dos três que não vem
"de graça" com o Windows.

### 3.1 Dockerfile

Já existe no repositório em `src/BellucSketch.Api/Dockerfile` (criado para o deploy no Render, ver
`GUIA_DEPLOY_RENDER.md`) — não precisa criar um novo. Uma diferença a saber: o `ENTRYPOINT` lê a
porta de uma variável `PORT` (`${PORT:-8080}`, convenção do Render), então localmente ela cai no
padrão `8080` se você não definir `PORT` explicitamente — compatível com o `docker run` abaixo.

### 3.2 Build e execução

```powershell
docker build -t bellucsketch-api -f src/BellucSketch.Api/Dockerfile .
docker run -d --name bellucsketch-api `
  -p 5080:8080 `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e Jwt__Chave="<gere uma chave aleatória aqui>" `
  -e ConnectionStrings__Camdas="Host=host.docker.internal;Database=camdas;Username=camdas;Password=<senha-real>" `
  --restart unless-stopped `
  bellucsketch-api
```

`--restart unless-stopped` já cobre o "reinicia sozinho se cair" sem precisar de mais nada. Se o
Postgres também estiver em outro contêiner (não coberto aqui — este projeto usa Postgres instalado
direto no Windows via `winget`, ver `README.md`), ajuste a connection string pro nome do serviço em
vez de `host.docker.internal`.

---

## Depois de publicado, pros clientes (Mobile/Web) enxergarem

Independente da opção escolhida:

- Liberar a porta no firewall do Windows do servidor (`New-NetFirewallRule`, ver `README.md`).
- **Só o `BellucSketch.Web` aceita reconfiguração sem rebuild** — edite `ApiBaseUrl` em
  `src/BellucSketch.Web/wwwroot/appsettings.json` (arquivo estático, lido em runtime pelo navegador)
  pro **IP fixo** (ou nome de rede interno, se a empresa tiver DNS interno) do servidor, não
  `localhost`. **O app Android precisa de um build novo**: edite a constante
  `ConfiguracaoApi.BaseUrl` (`src/BellucSketch.Mobile.Core/Services/ConfiguracaoApi.cs`) pro mesmo
  endereço, gere um novo `.apk` (ver `README.md`, "Rodando o app Android") e redistribua (ver
  `GUIA_INSTALACAO_ANDROID.md`) — não existe mais uma tela no app pra digitar esse endereço.
- Se possível, prefira dar um IP fixo (ou reserva de DHCP) pro servidor — evita ter que gerar um novo
  `.apk` toda vez que o IP mudar.
