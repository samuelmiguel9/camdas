# Guia de instalação — BellucSketch no Android

Guia para quem vai **usar** o app (montador), não para quem desenvolve. Para gerar o `.apk` a
partir do código-fonte, veja [README.md](README.md), seção "Rodando o app Android".

## Pré-requisitos

- Celular Android (testado num Samsung Galaxy A15, Android recente — funciona a partir da API 21).
- O arquivo `M12.apk` (build Release já assinado, gerado por quem administra o projeto).
- Celular na **mesma rede Wi-Fi** do servidor da Api (ou acesso à rede da empresa por VPN) — o app
  não funciona fora dessa rede, porque a Api roda dentro da intranet, sem exposição à internet.

## 1. Permitir instalação de fontes desconhecidas

Como o app não vem da Google Play, o Android bloqueia a instalação por padrão. Antes de instalar:

1. Envie o `M12.apk` para o celular (cabo USB, link de download interno, WhatsApp/Drive — qualquer
   meio que deixe o arquivo acessível no celular).
2. Ao tocar no arquivo pra instalar, o Android vai pedir permissão pra "instalar apps desconhecidos"
   vindo do app usado pra abrir o `.apk` (Arquivos, Chrome, etc.) — toque em **Permitir** e depois em
   **Instalar**.
   - Em alguns aparelhos Samsung isso fica em **Configurações → Apps → Acesso especial → Instalar
     apps desconhecidos**, selecionando o app usado pra abrir o arquivo.

## 2. Primeira abertura — configurar o servidor

Na primeira vez que o app abre (ou sempre que troca de rede — casa/trabalho/cliente), ele tenta
sozinho os endereços de servidor já salvos. Se nenhum responder, aparece uma tela pedindo:

1. **IP e porta do servidor**, no formato `192.168.0.50:5080` (pergunte ao administrador da rede
   qual é o IP da máquina rodando a `BellucSketch.Api` nesse momento — pode mudar se a máquina reiniciar o
   Wi-Fi, por exemplo).
2. **Um nome pra esse endereço** (ex.: "Escritório", "Cliente X") — fica salvo pra da próxima vez o
   app reconhecer sozinho essa rede.

Depois disso, a tela de login pede o **Id do usuário** (um Guid) — peça ao administrador, já que
ainda não existe cadastro de usuário pelo próprio app (ver `REVISAO_SEGURANCA.md`, item 2).

## 3. Atualizando para uma versão nova

Basta repetir o passo 1 com o `M12.apk` novo — o Android substitui a instalação anterior sem apagar
os dados salvos (endereços de servidor, sessão). Se o app não abrir mais depois de atualizar, veja
a seção de problemas conhecidos abaixo antes de reinstalar do zero.

## Problemas conhecidos e como resolver

- **App instala mas fecha sozinho ao abrir.** Só acontece se alguém tentar instalar um `.apk` de
  build **Debug** em vez de Release — o `M12.apk` distribuído já é sempre Release, então isso não
  deveria acontecer com o arquivo oficial. Se acontecer mesmo assim, avise quem gerou o `.apk`.
- **Erro ao entrar: `Java.Security.GeneralSecurityException`.** Bug conhecido do Android em alguns
  aparelhos (mais comum em Samsung) — a chave de armazenamento seguro do token fica inválida,
  geralmente depois de reinstalar o app várias vezes. Já corrigido nas versões atuais (o app limpa e
  recria a chave sozinho); se aparecer numa versão antiga, desinstale e instale de novo.
- **Tela de login não sai do "Servidor não encontrado".** Confirme que o celular está na mesma rede
  Wi-Fi do servidor, que a máquina do servidor está com a Api rodando (`dotnet run --project
  src/BellucSketch.Api --urls http://0.0.0.0:5080`, não só `localhost`) e que a porta está liberada no
  firewall do Windows daquela máquina.
- **App abre e funciona, mas as imagens/plantas não carregam.** Geralmente é a Api ter reiniciado e
  trocado de IP (ex.: outra rede Wi-Fi) — force o app a esquecer o endereço salvo removendo e
  reinstalando, ou espere a próxima tentativa automática de resolução de endereço (acontece toda vez
  que a tela de login aparece).

## Segurança

O app se conecta à Api por HTTP simples (sem HTTPS) dentro da rede interna — não use fora de uma
rede confiável (Wi-Fi público, por exemplo). Detalhes e recomendações em
[REVISAO_SEGURANCA.md](REVISAO_SEGURANCA.md).
