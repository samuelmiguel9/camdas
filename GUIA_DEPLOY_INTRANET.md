# Guia de deploy na intranet — Camdas.Api

Como deixar a `Camdas.Api` rodando de forma permanente num servidor da empresa, em vez de depender
de alguém abrir um terminal e rodar `dotnet run` manualmente toda vez. Três caminhos, do mais
simples ao mais portável — escolha um, não precisa dos três.

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
dotnet publish src/Camdas.Api -c Release -o C:\Servicos\CamdasApi
```

Isso gera um `Camdas.Api.exe` autocontido em `C:\Servicos\CamdasApi` (ajuste o caminho como
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
New-Service -Name "CamdasApi" `
  -BinaryPathName "C:\Servicos\CamdasApi\Camdas.Api.exe --urls http://0.0.0.0:5080" `
  -DisplayName "Camdas Api" `
  -StartupType Automatic
Start-Service CamdasApi
```

### 1.4 Conferir

```powershell
Get-Service CamdasApi
Invoke-WebRequest http://localhost:5080/health
```

Pra atualizar depois de uma mudança de código: `Stop-Service CamdasApi`, publique de novo por cima
da mesma pasta, `Start-Service CamdasApi`.

---

## Opção 2 — IIS (se a empresa já usa IIS pra outras coisas)

Mais trabalho de configurar, mas se já existe um IIS na empresa (com outros sites internos), é
onde a equipe de infra já sabe mexer.

1. Instalar o **ASP.NET Core Hosting Bundle** no servidor (não é só o .NET runtime — é um pacote à
   parte que integra o Kestrel com o IIS): [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
   (procure "Hosting Bundle" da versão .NET 8).
2. `dotnet publish src/Camdas.Api -c Release -o C:\inetpub\wwwroot\CamdasApi`.
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

### 3.1 Dockerfile (criar na raiz de `src/Camdas.Api/`, ainda não existe no repositório)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Camdas.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Camdas.Api.dll", "--urls", "http://0.0.0.0:8080"]
```

### 3.2 Build e execução

```powershell
docker build -t camdas-api -f src/Camdas.Api/Dockerfile .
docker run -d --name camdas-api `
  -p 5080:8080 `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e Jwt__Chave="<gere uma chave aleatória aqui>" `
  -e ConnectionStrings__Camdas="Host=host.docker.internal;Database=camdas;Username=camdas;Password=<senha-real>" `
  --restart unless-stopped `
  camdas-api
```

`--restart unless-stopped` já cobre o "reinicia sozinho se cair" sem precisar de mais nada. Se o
Postgres também estiver em outro contêiner (não coberto aqui — este projeto usa Postgres instalado
direto no Windows via `winget`, ver `README.md`), ajuste a connection string pro nome do serviço em
vez de `host.docker.internal`.

---

## Depois de publicado, pros clientes (Mobile/Web) enxergarem

Independente da opção escolhida, os passos finais são os mesmos já documentados:

- Liberar a porta no firewall do Windows do servidor (`New-NetFirewallRule`, ver `README.md`).
- Apontar `ConfiguracaoApi.BaseUrl` (app Android) e `ApiBaseUrl` (`Camdas.Web/wwwroot/appsettings.json`)
  pro **IP fixo** (ou nome de rede interno, se a empresa tiver DNS interno) do servidor — não
  `localhost`, que só funciona na própria máquina.
- Se possível, prefira dar um IP fixo (ou reserva de DHCP) pro servidor — evita o problema descrito
  em `RELATORIO.md`/`GUIA_INSTALACAO_ANDROID.md` de o app "perder" o servidor toda vez que o IP
  muda.
