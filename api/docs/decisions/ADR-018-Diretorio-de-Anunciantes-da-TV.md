# ADR-018: Diretório de Anunciantes da TV

## Status

Aceito

## Data da decisão

2026-10-14

## Contexto

O QR code de cada anúncio (ADR-017) só fica na tela enquanto o anúncio passa,
em geral 10 a 15 segundos: muitas vezes a pessoa não tem tempo de pegar o
celular. A operadora quer um QR code **fixo** na TV que abra uma página com
os contatos de todos os anunciantes daquela tela.

Restrições:

- **LGPD e relação comercial:** o telefone de um MEI é dado pessoal, e
  publicar numa página permanente vai além do anúncio. Quem aparece tem de
  ter aceitado.
- **Celular no 4G:** a página tem de abrir rápido em rede fraca, fora do
  Wi-Fi do estabelecimento.
- **Endereço público:** os ids das telas são sequenciais; um endereço com o
  id deixaria qualquer um listar os anunciantes da rede inteira.

## Decisão

- **Dupla autorização:**
  - a operadora liga o diretório em cada tela (`Tela.DiretorioAtivo`);
  - cada parceiro aceita aparecer no seu perfil público
    (`Parceiro.ExibirNoDiretorioPublico`, padrão não).

  Só o dono do parceiro muda o aceite (não a equipe nem a operadora, que só
  vê). O momento do aceite fica gravado (`DiretorioAceitoEm`) e no histórico.
- **Quem aparece:** os parceiros com mídia na grade de **hoje** da tela, pelas
  mesmas regras da sincronização:
  - mídia ativa, dentro do período e no dia da semana, no fuso do
    estabelecimento;
  - anunciante não bloqueado.

  O estabelecimento que hospeda a TV vem primeiro (permuta pelo espaço), mesmo
  sem anúncio na grade, desde que também tenha aceitado.
- **Contatos:** WhatsApp, Instagram e site do perfil público, mais os QR codes
  das mídias que estão na grade, sem repetir e com no máximo 6 por anunciante.
  No diretório, o Pix vira o botão **"Copiar Pix"** (copia e cola), porque
  ninguém lê um QR na própria tela.
- **Endereço:** cada tela ganha um código aleatório de 10 caracteres (o
  alfabeto do pareamento, 32¹⁰ combinações), criado quando o diretório é
  ligado pela primeira vez.
  - O endereço é `{Indoor:UrlPublica}/d/{codigo}`: curto, para o QR ser menos
    denso e lido de mais longe.
  - Desligar mantém o código (o QR impresso continua o mesmo).
  - "Trocar endereço" invalida o anterior.
- **A página é HTML pronto, servido pela API**, e não uma rota do painel
  Angular:
  - o pacote inicial do painel tem cerca de 440 KB comprimidos (o framework
    vai inteiro), enquanto a página tem cerca de 10 KB, sem framework e numa
    requisição só;
  - só a API precisa estar na internet.

  Proteções da página:
  - texto codificado e só endereços `https:`, `http:` e `tel:`;
  - CSP com `nonce`, sem nada externo;
  - `Referrer-Policy: no-referrer`;
  - `noindex`;
  - o mesmo conteúdo também sai em JSON (`/api/v1/publico/diretorio/{codigo}`).
- **Desempenho e abuso:**
  - o diretório de cada código fica 60 s em memória, inclusive "não existe"
    (quem tenta adivinhar códigos não chega ao banco);
  - limite de 60 acessos por minuto por IP;
  - código inexistente, tela inativa e diretório desligado dão o mesmo 404.
- **Na TV:** o endereço vem na sincronização (`diretorio: {url, legenda}`),
  fora da `versaoGrade`. A TV gera o QR (pequeno, canto inferior direito) e o
  mostra:
  - durante os anúncios **sem** QR próprio (dois QR ao mesmo tempo
    confundem);
  - na tela de espera.

  Fica fora das notícias e dos cartões, que têm título e crédito nos cantos.

## Justificativa

- O aceite do parceiro é a base legal (consentimento) e evita surpresa
  comercial. A chave da tela deixa a operadora decidir onde vale a pena.
- Montar a lista na hora (com cache curto) mantém a página fiel à grade: quem
  sai da grade some sozinho, sem tarefa de limpeza.

Alternativas rejeitadas:

- **Rota pública no painel Angular:** pesada para o 4G e obrigaria o painel a
  ficar na internet.
- **Id da tela no endereço:** dava para varrer a rede inteira.
- **Aceite pela operadora:** o dado e a decisão são do anunciante.
- **Logo do anunciante:** exige um caminho público no storage (o bucket é
  privado); fica para a fase 2, com as iniciais no lugar.

## Consequências

### Ficou mais fácil

- O anunciante ganha um contato permanente por TV, além dos 15 s do anúncio.
- O estabelecimento aparece em primeiro, como benefício da permuta.

### Ficou mais difícil

- A API precisa de um endereço HTTPS público (`Indoor:UrlPublica`).
- Mudanças (aceite, grade, desligar) levam até 60 s para aparecer na página.
- Fase 2: contagem de acessos e de cliques por anunciante e o logo.

## Referências

- [ADR-013](ADR-013-Player-Offline-First.md) — Player offline-first
- [ADR-017](ADR-017-QR-Code-nos-Anuncios.md) — QR code nos anúncios
