# Briefing do conselho — Mídia Indoor

Leia isto como um estranho: tudo o que você precisa está aqui. Responda em **português**.

## O negócio

Uma rede de TVs em estabelecimentos de terceiros que exibe anúncios de pequenos negócios locais.

- **Operadora**: o fundador. Compra e instala as TVs/TV boxes (Android), é dona do hardware, cadastra parceiros e clientes, monta as playlists (grades) e decide o que cada TV toca. Hoje é uma pessoa só.
- **Parceiro**: o estabelecimento onde a TV fica (academia, lanchonete, clínica). Cede o espaço e a energia/internet do local. **Não acessa o sistema.** Ganha anúncio grátis do próprio negócio na TV dele e talvez algum repasse em dinheiro, negociado caso a caso conforme a disposição dele em ceder o espaço.
- **Cliente**: o anunciante (pequeno negócio local). Tem login num painel web, envia vídeos/imagens, vê em quais TVs passa. Paga pela veiculação — **o preço e o modelo de cobrança ainda não foram definidos**.

## Escala e restrições

- Piloto: até 10 TVs, uma cidade. Operado pelo próprio fundador.
- A API roda num servidor caseiro (um celular). As mídias ficam no Supabase gratuito (1 GB no total, 50 MB por arquivo).
- Sem budget em dinheiro informado. Sem dados ainda de quantos clientes pagantes existem, custo por TV instalada, ou taxa de cancelamento.

## O que o software já faz (estado atual)

- **Player offline-first** (app Flutter no Android TV): só toca arquivos já baixados e conferidos por SHA-256; sem internet continua tocando o que tem; troca a grade só quando tudo baixou; abre sozinho quando a TV liga e depois de atualizar.
- **Sincronização a cada 60 s**; a TV aparece Online/Offline no painel (Offline após 5 min sem sincronizar).
- **Alerta por e-mail** quando uma TV fica 15 min sem sincronizar (só no horário 7h–22h do local) e quando ela volta.
- **Atualização automática do app** das TVs (envio do APK no painel, liberação primeiro para TVs de teste, depois para todas; sem confirmação se o app for "device owner").
- **Anúncios**: vídeo MP4 ou imagem, período de exibição, dias da semana, QR code (link, WhatsApp, Instagram, telefone, Pix); plano por cliente (quantas mídias ativas) e cota de espaço.
- **Troca de arquivo** de anúncio que já está no ar passa por aprovação da operadora.
- **Fila na TV**: carrossel com cursor que garante a mesma frequência para todos os anunciantes; intercala notícias (RSS), clima e cotações.
- **Diretório de anunciantes**: QR code fixo na TV que abre uma página pública com os contatos dos anunciantes daquela TV.
- **Bloqueio de cliente** (ex.: inadimplência) tira os anúncios dele das grades.

## O que **não** existe hoje

- Prova de veiculação / relatório de exibições para o anunciante (quantas vezes passou, onde, quando).
- Contagem de leituras do QR code ou de acessos ao diretório.
- Cobrança, faturas, contratos ou controle de inadimplência no sistema.
- Monitoramento do hardware além de "sincronizou ou não" (sem saber se a tela física está ligada, na entrada certa, ou com o volume/brilho certos).
- Backup automatizado documentado do servidor caseiro; redundância do servidor.
- Autocadastro de clientes (só a operadora cadastra).

## O que o fundador pediu

Uma análise crítica rigorosa, como um conselho de investidores. Foco em **riscos operacionais** — por exemplo: queda de internet nos pontos, manutenção de hardware, churn (cancelamento) de anunciantes — e **o que ele precisa prever no software** para mitigar cada risco. Também avalie o negócio em si (vale continuar? sob que condições?).
