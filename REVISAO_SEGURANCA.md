# Revisão de segurança — BellucSketch

Revisão final da Fase 7 (ver [TASKS.md](TASKS.md)). Escopo: autenticação/JWT, transporte
(HTTP/HTTPS), segredos versionados e superfície exposta pela Api. Não cobre code review linha a
linha de todo o código — foco em riscos reais para um app usado dentro da intranet de uma empresa.

## Resumo

| # | Achado | Severidade | Status |
|---|--------|------------|--------|
| 1 | Chave de assinatura JWT commitada em texto puro no `appsettings.json` | Alta | **Corrigido** (guarda de inicialização) |
| 2 | `POST /api/auth/dev-token` emite token sem senha, para qualquer `UsuarioId` existente | Alta | Aceito como placeholder — já documentado, adiado (ver Backlog futuro em TASKS.md) |
| 3 | Api roda em HTTP puro (sem TLS); Android libera `usesCleartextTraffic` | Média | **Resolvido em produção** — Api publicada no Render com HTTPS automático (ver nota abaixo) |
| 4 | Connection string do Postgres com credenciais em texto puro no `appsettings.json` | Média | Recomendação abaixo (mesma mitigação da chave JWT) — em produção já vem de segredo `sync: false` no Render |
| 5 | CORS libera qualquer origem (`AllowAnyOrigin`) na Api | Baixa | Aceito — ver justificativa |
| 6 | Token JWT de 8h sem revogação/refresh | Baixa | Aceito para o escopo atual |
| 7 | `android:allowBackup="true"` no APK | Baixa | Aceito — ver nota (achado original ficou parcialmente obsoleto, ver texto) |
| 8 | Credenciais do Supabase Storage (Access Key/Secret Key S3) | Média | Mesma mitigação da connection string — já são segredo `sync: false` no Render, nunca commitadas |

## 1. Chave JWT commitada (Alta) — corrigido

`src/BellucSketch.Api/appsettings.json` tinha (e continua tendo, propositalmente, como placeholder óbvio):

```json
"Jwt": { "Chave": "TROQUE_ESTA_CHAVE_em_producao_min_32_caracteres_1234567890", ... }
```

Qualquer pessoa com acesso ao repositório consegue forjar um token válido para qualquer
`UsuarioId`, já que a chave de assinatura simétrica está no código-fonte. O `appsettings.json` já
avisava "troque em produção" no próprio valor, mas nada impedia alguém de esquecer e subir a Api
assim mesmo.

**Correção aplicada** (`src/BellucSketch.Api/Program.cs`): a Api agora recusa iniciar fora do ambiente
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

## 3. HTTP sem TLS (Média) — resolvido em produção, ainda vale para quem usar o guia de intranet

**Atualização**: a Api de produção não roda mais na intranet — está publicada no Render
([GUIA_DEPLOY_RENDER.md](GUIA_DEPLOY_RENDER.md)), que dá HTTPS automático, e
`ConfiguracaoApi.BaseUrl`/`ApiBaseUrl` (Mobile/Web) já apontam para a URL `https://`. O token JWT e o
conteúdo das plantas trafegam criptografados por padrão, sem nenhuma ação extra. `AndroidManifest.xml`
ainda libera `android:usesCleartextTraffic="true"` — hoje é inofensivo (nada no app chama HTTP puro
de propósito), mas ficou vestigial e poderia ser removido numa limpeza futura; não removi nesta
revisão para não misturar com o escopo desta análise.

Texto original desta seção (segue válido para quem optar por
[GUIA_DEPLOY_INTRANET.md](GUIA_DEPLOY_INTRANET.md) em vez do Render — nesse caminho a Api volta a
rodar em `http://` puro, e a recomendação abaixo se aplica de novo): para uma intranet controlada
(rede corporativa, sem Wi-Fi público misturado) o risco de HTTP puro é baixo, mas o ideal antes de um
deploy mais amplo é publicar a Api atrás de HTTPS com uma CA interna (IIS com certificado da CA da
empresa, ou um reverse proxy como nginx/Caddy na frente do Kestrel) — nesse ponto o app volta a
exigir HTTPS por padrão sem nenhuma mudança de código além de trocar `http://` por `https://` em
`ConfiguracaoApi` (que hoje exigiria editar o código-fonte e gerar um novo build, já que o endereço
deixou de ser configurável em runtime — ver RELATORIO.md, Fase 10).

## 4. Connection string em texto puro (Média) — mesma mitigação da chave JWT

`ConnectionStrings:Camdas` no `appsettings.json` tem usuário/senha do Postgres de desenvolvimento
(`camdas`/`camdas`) versionados. Como são credenciais de desenvolvimento local (banco só acessível
em `localhost`, sem exposição externa), o risco prático hoje é baixo — mas o padrão para produção é
o mesmo da chave JWT: sobrescrever via variável de ambiente (`ConnectionStrings__Camdas`, já
suportado nativamente pelo ASP.NET Core, sem mudança de código) em vez de editar o
`appsettings.json` versionado. Não adicionei uma guarda de inicialização igual à da chave JWT aqui
porque não há um "valor placeholder óbvio" equivalente para detectar — uma connection string de
produção real também teria usuário/senha preenchidos, então não dá pra distinguir
programaticamente "esqueceram de trocar" de "essa é a senha de produção mesmo". **Em produção** (Render)
essa recomendação já é seguida: `ConnectionStrings__Camdas` é um segredo `sync: false`, preenchido
manualmente no painel do Render com a string real do Supabase — nunca commitada (ver `render.yaml`).

## 5. CORS `AllowAnyOrigin` (Baixa) — aceito

Já justificado em comentário no próprio `Program.cs`: a autenticação é por Bearer token (não
cookie), então não há credenciais implícitas do navegador em risco de CSRF — liberar qualquer
origem só permite que `BellucSketch.Web` (Blazor WASM, roda em porta diferente da Api) chame a Api. Um
site malicioso conseguiria, no máximo, fazer chamadas com o token de quem já estiver logado *nesse
navegador* — mesmo risco que existiria com uma lista de origens permitidas mal configurada. Aceito
como está.

## 6. Token de 8h sem refresh/revogação (Baixa) — aceito

`Jwt:ExpiracaoMinutos = 480` (8h) é razoável para uma ferramenta interna de uso diário — expira
sozinho ao fim do expediente, sem exigir refresh token (mais um mecanismo pra implementar/testar).
Não há revogação antecipada (ex.: usuário desativado continua com token válido até expirar) — se
isso virar um requisito real (ex.: desligamento de funcionário), precisa de uma verificação extra
de `Usuario.Ativo` a cada requisição autenticada, não só na emissão do token.

## 7. `android:allowBackup="true"` (Baixa) — aceito, achado original parcialmente obsoleto

Permite que o Android inclua dados do app no backup automático (Google/adb backup). O token JWT
fica no `SecureStorage` (backed pelo Android Keystore), que **não** é incluído em backups por
design do Android. **Atualização**: o achado original citava os endereços de servidor salvos em
`Preferences` (`ArmazenamentoEnderecosApiPreferences`) como o dado sensível exposto por esse backup —
esse mecanismo inteiro foi removido na Fase 10 (ver RELATORIO.md), já que o app hoje aponta pra um
endereço fixo (`ConfiguracaoApi.BaseUrl`), não mais configurável nem salvo em `Preferences`. Não sobrou
nenhum dado de rede sensível pra esse achado cobrir — risco reavaliado como ainda mais baixo do que
antes, não alterado nesta revisão por não haver mais ação corretiva óbvia a tomar.

## 8. Credenciais do Supabase Storage (S3) (Média) — mesma mitigação da connection string

Desde a migração pra Supabase Storage (`ArquivoStorageS3`, ver `GUIA_DEPLOY_RENDER.md`), a Api
também guarda `ArmazenamentoArquivos:S3:AccessKey`/`SecretKey` — credenciais que dão acesso de
leitura/escrita a todos os arquivos de todas as plantas do bucket. Assim como a connection string do
Postgres, essas chaves são segredos `sync: false` no Render (nunca aparecem em `appsettings.json`
nem em `render.yaml`, ver comentário no próprio arquivo) — preenchidas manualmente no painel a partir
do que o Supabase gera em Storage → Settings. Mesma ressalva do item 4: sem guarda de inicialização
(não há um "placeholder óbvio" pra detectar), mitigação é só não commitar o valor real.

## Verificado

- `dotnet build BellucSketch.sln` e `dotnet test BellucSketch.sln` — passando com a guarda nova (os testes de
  integração já sobrescrevem `Jwt:Chave` para um valor de teste em
  `tests/BellucSketch.Api.Tests/CustomWebApplicationFactory.cs`, então não são afetados).
