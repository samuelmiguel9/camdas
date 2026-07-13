# Revisão de segurança — Camdas

Revisão final da Fase 7 (ver [TASKS.md](TASKS.md)). Escopo: autenticação/JWT, transporte
(HTTP/HTTPS), segredos versionados e superfície exposta pela Api. Não cobre code review linha a
linha de todo o código — foco em riscos reais para um app usado dentro da intranet de uma empresa.

## Resumo

| # | Achado | Severidade | Status |
|---|--------|------------|--------|
| 1 | Chave de assinatura JWT commitada em texto puro no `appsettings.json` | Alta | **Corrigido** (guarda de inicialização) |
| 2 | `POST /api/auth/dev-token` emite token sem senha, para qualquer `UsuarioId` existente | Alta | Aceito como placeholder — já documentado, adiado (ver Backlog futuro em TASKS.md) |
| 3 | Api roda em HTTP puro (sem TLS); Android libera `usesCleartextTraffic` | Média | Aceito para intranet — recomendação abaixo |
| 4 | Connection string do Postgres com credenciais em texto puro no `appsettings.json` | Média | Recomendação abaixo (mesma mitigação da chave JWT) |
| 5 | CORS libera qualquer origem (`AllowAnyOrigin`) na Api | Baixa | Aceito — ver justificativa |
| 6 | Token JWT de 8h sem revogação/refresh | Baixa | Aceito para o escopo atual |
| 7 | `android:allowBackup="true"` no APK | Baixa | Aceito — ver nota |

## 1. Chave JWT commitada (Alta) — corrigido

`src/Camdas.Api/appsettings.json` tinha (e continua tendo, propositalmente, como placeholder óbvio):

```json
"Jwt": { "Chave": "TROQUE_ESTA_CHAVE_em_producao_min_32_caracteres_1234567890", ... }
```

Qualquer pessoa com acesso ao repositório consegue forjar um token válido para qualquer
`UsuarioId`, já que a chave de assinatura simétrica está no código-fonte. O `appsettings.json` já
avisava "troque em produção" no próprio valor, mas nada impedia alguém de esquecer e subir a Api
assim mesmo.

**Correção aplicada** (`src/Camdas.Api/Program.cs`): a Api agora recusa iniciar fora do ambiente
`Development` se `Jwt:Chave` ainda for exatamente esse placeholder — lança
`InvalidOperationException` na inicialização, antes de aceitar qualquer requisição.

```csharp
if (!app.Environment.IsDevelopment())
{
    var jwtOptions = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
    if (jwtOptions.Chave == ChavePlaceholder)
        throw new InvalidOperationException(/* ... */);
}
```

Isso não gera uma chave nova automaticamente (uma chave gerada em cada boot invalidaria todos os
tokens emitidos a cada reinício) — só impede que o placeholder vaze para um ambiente que não seja
Development. **Antes de publicar fora da máquina de desenvolvimento**, defina a variável de
ambiente `Jwt__Chave` (o `__` é o separador de seção que o ASP.NET Core já entende nativamente, sem
código adicional) com uma chave aleatória de pelo menos 32 caracteres, e `ASPNETCORE_ENVIRONMENT`
diferente de `Development`.

## 2. `dev-token` sem senha (Alta) — aceito como placeholder conhecido

Já documentado em `TASKS.md` (Fase 4) e no próprio `AutenticacaoController`: não existe login por
credencial ainda — `POST /api/auth/dev-token` emite um JWT válido para qualquer `UsuarioId` (Guid)
existente no banco, sem verificar senha. Isso é adequado para desenvolvimento/teste interno, mas
**não deve ir para um ambiente onde pessoas não confiáveis possam adivinhar/enumerar Ids de
usuário**. Fica no backlog futuro (login real com hash de senha, ex. `PasswordHasher<Usuario>`) —
fora do escopo desta revisão pontual porque exigiria desenhar um fluxo de cadastro/senha novo, não
uma correção de configuração.

## 3. HTTP sem TLS (Média) — aceito para intranet, com recomendação

A Api roda em `http://` puro (ver `README.md`, seção "Rodando a Api localmente"), e o
`AndroidManifest.xml` do app libera `usesCleartextTraffic="true"` porque o Android bloqueia HTTP
puro por padrão a partir da API 28. Isso expõe o token JWT (Bearer, no header `Authorization`) e o
conteúdo das plantas em texto puro para qualquer um capturando tráfego na mesma rede Wi-Fi/VPN.

Para uma intranet controlada (rede corporativa, sem Wi-Fi público misturado) o risco é baixo, mas o
ideal antes de um deploy mais amplo é publicar a Api atrás de HTTPS com uma CA interna (IIS com
certificado da CA da empresa, ou um reverse proxy como nginx/Caddy na frente do Kestrel) e então
remover `usesCleartextTraffic` do manifest — nesse ponto o app volta a exigir HTTPS por padrão sem
nenhuma mudança de código além de trocar `http://` por `https://` em `ConfiguracaoApi`. Não
implementado nesta revisão porque depende de infraestrutura (certificado) que não existe neste
ambiente de desenvolvimento — fica junto com o guia de deploy na intranet (item ainda pendente em
`TASKS.md`).

## 4. Connection string em texto puro (Média) — mesma mitigação da chave JWT

`ConnectionStrings:Camdas` no `appsettings.json` tem usuário/senha do Postgres de desenvolvimento
(`camdas`/`camdas`) versionados. Como são credenciais de desenvolvimento local (banco só acessível
em `localhost`, sem exposição externa), o risco prático hoje é baixo — mas o padrão para produção é
o mesmo da chave JWT: sobrescrever via variável de ambiente (`ConnectionStrings__Camdas`, já
suportado nativamente pelo ASP.NET Core, sem mudança de código) em vez de editar o
`appsettings.json` versionado. Não adicionei uma guarda de inicialização igual à da chave JWT aqui
porque não há um "valor placeholder óbvio" equivalente para detectar — uma connection string de
produção real também teria usuário/senha preenchidos, então não dá pra distinguir
programaticamente "esqueceram de trocar" de "essa é a senha de produção mesmo".

## 5. CORS `AllowAnyOrigin` (Baixa) — aceito

Já justificado em comentário no próprio `Program.cs`: a autenticação é por Bearer token (não
cookie), então não há credenciais implícitas do navegador em risco de CSRF — liberar qualquer
origem só permite que `Camdas.Web` (Blazor WASM, roda em porta diferente da Api) chame a Api. Um
site malicioso conseguiria, no máximo, fazer chamadas com o token de quem já estiver logado *nesse
navegador* — mesmo risco que existiria com uma lista de origens permitidas mal configurada. Aceito
como está.

## 6. Token de 8h sem refresh/revogação (Baixa) — aceito

`Jwt:ExpiracaoMinutos = 480` (8h) é razoável para uma ferramenta interna de uso diário — expira
sozinho ao fim do expediente, sem exigir refresh token (mais um mecanismo pra implementar/testar).
Não há revogação antecipada (ex.: usuário desativado continua com token válido até expirar) — se
isso virar um requisito real (ex.: desligamento de funcionário), precisa de uma verificação extra
de `Usuario.Ativo` a cada requisição autenticada, não só na emissão do token.

## 7. `android:allowBackup="true"` (Baixa) — aceito

Permite que o Android inclua dados do app no backup automático (Google/adb backup). O token JWT
fica no `SecureStorage` (backed pelo Android Keystore), que **não** é incluído em backups por
design do Android — mas os endereços de servidor salvos (`Preferences`, ver
`ArmazenamentoEnderecosApiPreferences`) são texto simples e poderiam ser restaurados em outro
aparelho. Risco baixo (só vaza um IP/porta de servidor interno, não credenciais), não alterado
nesta revisão.

## Verificado

- `dotnet build Camdas.sln` e `dotnet test Camdas.sln` — passando com a guarda nova (os testes de
  integração já sobrescrevem `Jwt:Chave` para um valor de teste em
  `tests/Camdas.Api.Tests/CustomWebApplicationFactory.cs`, então não são afetados).
