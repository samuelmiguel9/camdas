# Guia de instalação — BellucSketch no Android

Guia para quem vai **usar** o app (montador), não para quem desenvolve. Para gerar o `.apk` a
partir do código-fonte, veja [README.md](README.md), seção "Rodando o app Android".

## Pré-requisitos

- Celular Android (testado num Samsung Galaxy A15 e num Galaxy Tab A, Android recente — funciona a
  partir da API 23).
- O arquivo `BellucSketch.apk` mais recente (build Release já assinado) — disponível nos
  [Releases do GitHub](https://github.com/samuelmiguel9/camdas/releases).
- **Internet** (Wi-Fi ou dados móveis) — o app se conecta à Api publicada na nuvem (Render), não
  precisa mais estar na mesma rede de nenhum servidor específico.

## 1. Permitir instalação de fontes desconhecidas

Como o app não vem da Google Play, o Android bloqueia a instalação por padrão. Antes de instalar:

1. Envie o `.apk` para o celular (cabo USB, link de download interno, WhatsApp/Drive — qualquer
   meio que deixe o arquivo acessível no celular).
2. Ao tocar no arquivo pra instalar, o Android vai pedir permissão pra "instalar apps desconhecidos"
   vindo do app usado pra abrir o `.apk` (Arquivos, Chrome, etc.) — toque em **Permitir** e depois em
   **Instalar**.
   - Em alguns aparelhos Samsung isso fica em **Configurações → Apps → Acesso especial → Instalar
     apps desconhecidos**, selecionando o app usado pra abrir o arquivo.

## 2. Primeira abertura

Não há nenhuma configuração de servidor para fazer — o app já vem com o endereço da Api (publicada
no Render) embutido no próprio `.apk`. A tela de login pede direto o **Id do usuário** (um Guid) —
peça ao administrador, já que ainda não existe cadastro de usuário pelo próprio app (ver
`REVISAO_SEGURANCA.md`, item 2).

> Se você usou uma versão bem antiga do app, talvez lembre de uma tela pedindo IP/porta do servidor
> na primeira abertura — isso existia quando a Api rodava só na intranet da empresa e foi removido
> depois que a Api passou a rodar na nuvem (não há mais "servidor da rede" pra configurar).

## 3. Atualizando para uma versão nova

Basta repetir o passo 1 com o `.apk` novo — o Android substitui a instalação anterior sem apagar os
dados salvos (sessão). Se o app não abrir mais depois de atualizar, veja a seção de problemas
conhecidos abaixo antes de reinstalar do zero. O app também avisa sozinho, na tela de Projetos,
quando existe uma versão mais nova disponível (checagem automática contra os Releases do GitHub).

## Problemas conhecidos e como resolver

- **App instala mas fecha sozinho ao abrir.** Só acontece se alguém tentar instalar um `.apk` de
  build **Debug** em vez de Release — o `.apk` distribuído já é sempre Release, então isso não
  deveria acontecer com o arquivo oficial. Se acontecer mesmo assim, avise quem gerou o `.apk`.
- **Erro ao entrar: `Java.Security.GeneralSecurityException`.** Bug conhecido do Android em alguns
  aparelhos (mais comum em Samsung) — a chave de armazenamento seguro do token fica inválida,
  geralmente depois de reinstalar o app várias vezes. Já corrigido nas versões atuais (o app limpa e
  recria a chave sozinho); se aparecer numa versão antiga, desinstale e instale de novo.
- **App abre e funciona, mas as imagens/plantas não carregam.** Confirme que o celular tem internet
  de verdade (não só conectado ao Wi-Fi sem sinal de rede). A Api roda no plano gratuito do Render,
  que "dorme" depois de um tempo sem uso — a primeira chamada depois disso demora uns 30-60s pra
  responder (ver `GUIA_DEPLOY_RENDER.md`), o que pode parecer trava na primeira tela depois de um
  tempo sem abrir o app.

## Segurança

O app se conecta à Api publicada no Render por **HTTPS** (certificado automático do Render) — funciona
de qualquer rede com internet, não só dentro de uma rede confiável específica. Detalhes e
recomendações em [REVISAO_SEGURANCA.md](REVISAO_SEGURANCA.md).
