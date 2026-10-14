# ADR-017: QR Code nos Anúncios

## Status

Aceito

## Data da decisão

2026-10-14

## Contexto

O anunciante quer que quem está na frente da TV aja na hora: abrir o site,
chamar no WhatsApp, seguir no Instagram, ligar ou pagar com Pix. O QR code
precisa:

- ser lido de longe (2 m ou mais) com a câmera do celular;
- funcionar com a TV offline (ADR-013);
- ser cadastrado por quem não sabe o que é um "BR Code": o formulário pede
  só os dados do tipo escolhido.

## Decisão

- **Um QR code opcional por mídia**, guardado na própria `Midia` (colunas
  `qr_*`). Vale em todas as grades em que a mídia passa.
  - Tipos: Link, WhatsApp, Instagram, Telefone e Pix. Cada um usa só os
    seus campos; os outros são apagados ao salvar.
  - Check constraints: o QR está todo preenchido ou todo vazio
    (`ck_midia_qr_code_completo`); valor do Pix, se houver, maior que zero.
- **O conteúdo é montado e validado na API** (`MontadorQrCode`), não no
  painel nem na TV:
  - Link: URL absoluta http(s).
  - WhatsApp: `https://wa.me/55DDDNUMERO?text=...`.
  - Instagram: `https://instagram.com/usuario` (aceita `@usuario` ou o link).
  - Telefone: `tel:+55DDDNUMERO`.
  - Pix: **copia e cola estático** do Banco Central (EMV, com CRC16), com
    chave normalizada (CPF/CNPJ conferidos pelo dígito, e-mail, +55 ou chave
    aleatória), nome e cidade sem acentos e valor opcional.
  - Os erros voltam por campo, e nada é salvo com o QR inválido.
- **A TV gera a imagem** do QR a partir do texto (`qr_flutter`), sem
  internet, por cima da mídia:
  - o texto vai no item da sincronização (`qrCode`) e entra na
    `versaoGrade`;
  - cartão branco com margem e correção de erro M;
  - lado de 22%, 30% ou 40% da menor dimensão da tela, no canto escolhido;
  - o relógio sai do canto se o QR estiver nele.

## Justificativa

- Montar no servidor dá uma regra só para o painel (prévia) e para todas as
  TVs, e a validação do Pix fica testada em um lugar.
- Gerar a imagem na TV dispensa arquivo no storage, download e hash, e
  funciona offline. Mandar só o texto é leve.
- Pix estático é aceito por todos os bancos e não precisa de conta de
  integração (o dinâmico exige PSP e certificado).

Alternativas rejeitadas:

- **Imagem do QR enviada pelo anunciante:** podia vir ilegível ou para outro
  destino, sem como validar; mais um arquivo no storage.
- **QR com link curto nosso (redirecionador) para contar leituras:** útil,
  mas exige um serviço público sempre no ar e põe a nossa disponibilidade no
  caminho do cliente. Fica para depois.
- **Pix dinâmico (cobrança com txid):** exige integração com o banco do
  anunciante.

## Consequências

### Ficou mais fácil

- O anunciante cadastra o QR sozinho, com a prévia para testar no celular.
- Trocar o destino é editar a mídia; a TV atualiza na próxima sincronização.

### Ficou mais difícil

- Sem contagem de leituras: o QR leva direto ao destino.
- O banco local da TV passou para a versão 3 (colunas do QR), com migração
  automática.

## Referências

- [ADR-013](ADR-013-Player-Offline-First.md) — Player offline-first
- [ADR-016](ADR-016-Composicao-da-Fila-na-TV-Noticias-e-Clima.md) — Composição da fila na TV
- Manual de Padrões para Iniciação do Pix (Banco Central do Brasil), BR Code
