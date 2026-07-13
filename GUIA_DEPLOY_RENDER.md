# Guia de deploy no Render — Camdas.Api na nuvem

Coloca a `Camdas.Api` (e o banco Postgres) online, acessível de qualquer rede — resolve o problema
de precisar estar na mesma Wi-Fi do servidor pra usar o app/celular/tablet. Usa só o Render (banco +
Api no mesmo lugar); veja `GUIA_DEPLOY_INTRANET.md` se preferir manter tudo dentro da empresa.

O repositório já tem os dois arquivos que o Render precisa:
- `render.yaml` (raiz) — descreve o banco + a Api pro Render criar os dois de uma vez ("Blueprint").
- `src/Camdas.Api/Dockerfile` — como buildar a Api dentro de um contêiner.

## Passo a passo (a parte de cadastro só você consegue fazer — é login por navegador)

1. Acesse **https://dashboard.render.com/register** e cadastre-se **com sua conta do GitHub**
   (`samuelmiguel9`) — assim o Render já enxerga o repositório `camdas` direto, sem precisar
   configurar nada de acesso depois.
2. No painel, clique em **New +** → **Blueprint**.
3. Selecione o repositório **`samuelmiguel9/camdas`** e a branch `master`.
4. O Render lê o `render.yaml` sozinho e mostra um preview com dois recursos: o banco `camdas-db` e
   o serviço web `camdas-api`. Confirme em **Apply**.
5. Espera o primeiro deploy (a primeira vez demora mais — ele builda a imagem Docker do zero,
   geralmente uns 5-10 minutos). Acompanha o log ali mesmo no painel.
6. Quando terminar, o Render mostra a URL pública da Api, algo como
   `https://camdas-api.onrender.com`. **Me manda essa URL** que eu atualizo:
   - `ConfiguracaoApi`/endereço padrão do app Android, pra ele já vir configurado com essa URL
     (sem precisar digitar IP na primeira tela toda vez).
   - `src/Camdas.Web/wwwroot/appsettings.json` (`ApiBaseUrl`), pro visualizador Web apontar pra lá.

## O que verificar se o deploy falhar

- **Erro de conexão com o banco** ("could not connect", "SSL required"): abra a env var
  `ConnectionStrings__Camdas` do serviço `camdas-api` no painel do Render e confirme que ela foi
  preenchida automaticamente (vem do banco via `fromDatabase` no `render.yaml`). Se precisar forçar
  SSL, adicione `;SSL Mode=Require;Trust Server Certificate=true` no final do valor.
- **Build falha por falta de memória**: o plano free do Render tem RAM limitada pro build — se
  acontecer, tenta de novo (às vezes é só uma instabilidade momentânea do runner).
- **App sobe mas todo request devolve 500**: veja os logs do serviço no painel — a causa mais comum
  seria `Jwt:Chave` vazia, mas o `render.yaml` já gera uma automaticamente
  (`generateValue: true`), então não deveria acontecer.

## Limitações importantes do plano free (leia antes de confiar 100% nele)

- **O serviço "dorme" depois de ~15 minutos sem uso** e demora uns 30-60s pra acordar na próxima
  chamada — o app pode parecer "travado" na primeira tentativa depois de um tempo parado. Normal do
  plano gratuito; planos pagos não têm esse comportamento.
- **Arquivos enviados (plantas/camadas) não persistem entre deploys/reinícios.** `ArquivoStorageEmDisco`
  salva em `App_Data/plantas` dentro do contêiner — no Render (free), esse disco é *efêmero*: some
  a cada novo deploy ou quando o serviço reinicia depois de dormir. Pra resolver de verdade, as
  opções são: (a) um **Render Disk** (armazenamento persistente, precisa de plano pago), ou (b)
  trocar `ArquivoStorageEmDisco` por um storage externo tipo **Cloudflare R2** ou **AWS S3**
  (compatível com S3, tem camada free generosa) — isso exigiria uma implementação nova de
  `IArquivoStorage`, não é automático. Enquanto isso não for feito, trate o Render free como
  ambiente de teste/demonstração, não como armazenamento definitivo das plantas dos clientes.
- **Banco Postgres free do Render expira depois de 90 dias** (política deles pra plano gratuito) —
  se for usar por muito tempo, prepare-se pra migrar pro plano pago do banco antes disso ou fazer
  backup e recriar.

## Depois que a Api estiver no ar

Com a Api pública e em HTTPS (o Render já dá certificado automático — resolve, de quebra, o item
"HTTPS interno" do `REVISAO_SEGURANCA.md`), o app Android **não precisa mais** de
`usesCleartextTraffic="true"` nem do fluxo de "IP da rede" — passa a funcionar de qualquer lugar com
internet. Ainda não removi essas partes do código porque, até você confirmar que o deploy no Render
ficou estável, o app continua podendo apontar pro servidor local também (endereços múltiplos, ver
`ResolvedorEnderecoApi`) — dá pra ter os dois configurados ao mesmo tempo (ex.: "Render" e "PC
local") e trocar conforme a necessidade.
