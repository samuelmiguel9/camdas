# Guia de deploy: Render (Api) + Supabase (banco + arquivos)

**Este é o deploy em produção hoje** — a Api está publicada no Render, com banco de dados e
armazenamento de arquivos no Supabase, e o app Android/`BellucSketch.Web` já apontam pra lá por padrão
(`ConfiguracaoApi.BaseUrl`/`ApiBaseUrl`). Este guia serve para: entender como esse deploy foi feito,
reproduzi-lo (ex.: um ambiente próprio de testes) ou migrar pra um projeto novo de Render/Supabase se
for preciso um dia. Veja `GUIA_DEPLOY_INTRANET.md` se preferir manter tudo dentro da empresa em vez da
nuvem — nesse caso, o endereço fixo no app precisa ser trocado no código-fonte (ver nota no fim deste
guia).

O repositório já tem os arquivos que o Render precisa — `render.yaml` (raiz) e
`src/BellucSketch.Api/Dockerfile`. As duas partes de cadastro (Supabase e Render) só você consegue fazer,
são login por navegador.

## Passo 1 — Criar o projeto no Supabase

1. Acesse **https://supabase.com/dashboard** e cadastre-se (dá pra usar a conta do GitHub também).
2. **New project** → escolha um nome (ex.: `camdas`) e uma senha forte pro banco — **guarde essa
   senha**, você vai precisar dela no passo 3.
3. Espera o projeto provisionar (~2 minutos).

## Passo 2 — Criar o bucket de armazenamento

1. No menu lateral do projeto, vá em **Storage**.
2. **New bucket** → nome `plantas` → pode deixar **privado** (não marque "Public bucket" — a Api
   sempre acessa via credencial, não precisa de acesso público direto ao arquivo).
3. Ainda em Storage, clique em **Settings** (ou "S3 Connection", dependendo da versão da UI) e
   anote três coisas:
   - **Endpoint URL** (algo como `https://<id-do-projeto>.supabase.co/storage/v1/s3`)
   - **Access Key ID**
   - **Secret Access Key** (só aparece uma vez na criação — se perder, gera uma nova credencial ali
     mesmo)

## Passo 3 — Pegar a connection string do banco

1. **Project Settings** (ícone de engrenagem) → **Database**.
2. Em **Connection string**, escolha a aba **URI** e copie o valor — algo como
   `postgresql://postgres.xxxx:[YOUR-PASSWORD]@aws-0-xxxx.pooler.supabase.com:5432/postgres`.
3. Substitua `[YOUR-PASSWORD]` pela senha que você criou no passo 1.

## Passo 4 — Criar o serviço no Render

1. Acesse **https://dashboard.render.com/register** e cadastre-se **com sua conta do GitHub**
   (`samuelmiguel9`) — assim o Render já enxerga o repositório `camdas`.
2. **New +** → **Blueprint** → selecione o repositório `samuelmiguel9/camdas`, branch `master`.
3. O Render lê o `render.yaml` e mostra o preview do serviço `camdas-api`. Confirme em **Apply**.
4. O primeiro deploy vai **falhar ou ficar incompleto** — normal, faltam as variáveis que só você
   tem (do Supabase). Depois do Apply, vá em **camdas-api → Environment** e preencha:

   | Variável | Valor |
   |---|---|
   | `ConnectionStrings__Camdas` | a connection string do Passo 3 (já com a senha) |
   | `ArmazenamentoArquivos__S3__EndpointUrl` | Endpoint URL do Passo 2 |
   | `ArmazenamentoArquivos__S3__Bucket` | `plantas` |
   | `ArmazenamentoArquivos__S3__AccessKey` | Access Key ID do Passo 2 |
   | `ArmazenamentoArquivos__S3__SecretKey` | Secret Access Key do Passo 2 |

5. Salvar as variáveis já dispara um novo deploy automaticamente. Acompanha o log — na primeira
   subida, a Api aplica as migrations sozinha no banco do Supabase (não precisa rodar `dotnet ef`
   manualmente).
6. Quando concluir, o Render mostra a URL pública, tipo `https://camdas-api.onrender.com` — é esse
   endereço que precisa ir em `ConfiguracaoApi.BaseUrl`
   (`src/BellucSketch.Mobile.Core/Services/ConfiguracaoApi.cs`) e `ApiBaseUrl`
   (`src/BellucSketch.Web/wwwroot/appsettings.json`), com um novo build/deploy depois (isso já está
   feito para o deploy atual, apontando pra `https://camdas-api-gb9z.onrender.com/` — só repita este
   passo se migrar pra um projeto novo de Render).

## O que verificar se algo falhar

- **Erro de conexão com o banco**: confirme que substituiu `[YOUR-PASSWORD]` de verdade na
  connection string, e que copiou a URI completa (não só o host).
- **Erro de acesso ao Storage** ("Access Denied", "InvalidAccessKeyId"): confira se a Secret Key foi
  colada certinha (ela só aparece uma vez — se não salvou, gere uma nova em Storage → Settings).
- **App sobe mas todo request devolve 500**: veja os logs do serviço no painel do Render.

## Limitações a saber

- **O serviço do Render "dorme" depois de ~15 minutos sem uso** (plano free) e demora uns 30-60s
  pra acordar na próxima chamada — normal, planos pagos não têm esse comportamento.
- **Banco e Storage do Supabase (plano free)**: o projeto pausa automaticamente depois de ~1 semana
  sem nenhuma atividade (basta acessar o painel ou a Api pra reativar) — diferente do Render, aqui
  os dados **não são apagados**, só fica pausado até alguém acessar de novo.
- Diferente do disco efêmero do Render, os arquivos no Supabase Storage **persistem normalmente**
  entre deploys/reinícios da Api — esse era o problema do plano anterior (só Render), já resolvido.

## Depois que a Api estiver no ar

Com a Api pública e em HTTPS (o Render já dá certificado automático — resolve, de quebra, o item
"HTTP sem TLS" do `REVISAO_SEGURANCA.md`), o app Android/Web **não precisa mais** de estar na mesma
rede do servidor — funciona de qualquer lugar com internet. O antigo fluxo de "IP da rede" (perguntar
o endereço do servidor na primeira abertura, com vários endereços salvos ao mesmo tempo) foi removido
por completo depois que esse deploy ficou estável — `ConfiguracaoApi.BaseUrl` hoje é uma constante
fixa apontando pro Render (ver RELATORIO.md, Fase 10). Pra apontar pra outro servidor (um projeto novo
de Render, ou uma Api na intranet via `GUIA_DEPLOY_INTRANET.md`), é preciso editar esse arquivo e
gerar um novo build — não tem mais como trocar em runtime pelo app.
