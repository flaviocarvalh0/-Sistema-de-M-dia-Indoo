# ADR-025: Cloudflare R2 e Teto de 10 GB

## Status

Aceito

## Data da decisão

2026-10-20

## Contexto

O Supabase gratuito tem 1 GB de storage e pausa o projeto depois de 7 dias sem
uso. As TVs baixam os vídeos de novo a cada troca, e no Supabase a saída é cobrada
acima da franquia. O Cloudflare R2 tem **10 GB grátis**, **sem taxa de saída** e
operações gratuitas suficientes para a rede (1 milhão classe A e 10 milhões
classe B por mês).

A exigência da operadora: **não passar dos 10 GB de jeito nenhum**. Acima disso o
R2 cobra, e a cobrança vai no cartão sem aviso.

## Decisão

### Provedor

- `IArmazenamentoMidias` ganha a implementação `ArmazenamentoR2` pela API S3,
  com **assinatura SigV4 feita à mão** (`AssinaturaS3`), conferida com os
  exemplos oficiais da AWS. Sem o SDK da AWS: as versões novas mandam cabeçalhos
  de checksum que o R2 nem sempre aceita.
- **R2 tem prioridade**: com `Indoor:Armazenamento:R2` configurado, ele é o
  storage; senão o Supabase; senão nenhum (rotas de arquivo respondem 503).
- URLs de leitura são assinadas na API, **sem chamar o R2** (nenhuma operação
  cobrada). Validade máxima de 7 dias (limite do S3).
- Na subida, a API tenta liberar o **CORS** do bucket para `Indoor:UrlPainel`.
  Com token só de objetos, registra o aviso e o CORS é feito no painel da
  Cloudflare.
- **Migração**: com R2 e Supabase configurados e
  `Indoor:Armazenamento:CopiarDoSupabase=true`, a subida copia para o R2 tudo que
  o banco conhece e ainda não está lá (recomeça do que falta se parar). Se o
  total passar do teto, não copia nada.

### Teto (as travas, da primeira à última)

1. **Limite configurado**: `Indoor:Armazenamento:LimiteTotalMb`, padrão **9216**
   (9 GB), **nunca acima de 10240** (o código corta). O 1 GB de folga cobre o que
   o banco não sabe.
2. **Reserva antes da URL** (`TetoArmazenamento.ReservarAsync`): o envio de mídia
   e de APK só recebe URL se `usado + reservado + arquivo ≤ teto`. A reserva
   (`indoor.reserva_envio`) vale 2 h, como a URL. "Usado" é o **maior** entre a
   soma do banco e a última medição do bucket (até 3 h).
3. **Tamanho e tipo assinados na URL**: o R2 recusa um PUT com
   `Content-Length` ou `Content-Type` diferente do declarado.
4. **Conferência no recebimento**: sem reserva ou maior que o reservado, o
   arquivo é apagado e o envio recusado.
5. **Inventário de hora em hora** (`MonitorArmazenamento`, trava 7_310_005):
   lista o bucket (ListObjectsV2), grava o uso real em
   `indoor.inventario_armazenamento` e apaga os **órfãos** (nenhuma tabela
   referencia e têm mais de 6 h). **Nunca apaga mais da metade** dos arquivos numa
   passada: se parecer tudo órfão, só registra o erro.
6. **Alertas** por e-mail à operadora a **70%, 85% e 95%**, uma vez por nível
   (volta a zero quando o uso cai).
7. **Conteúdo automático** (fotos das notícias) só entra até **90%** do teto.

O painel da operadora mostra a barra de uso em **Clientes**
(`GET /api/v1/business/rede/armazenamento`).

## Alternativas consideradas

- **SDK da AWS (AWSSDK.S3):** mais código de terceiros e incompatibilidades de
  checksum com o R2.
- **Alerta de cobrança da Cloudflare:** avisa depois de cobrar. Fica como
  recomendação extra, não como trava.
- **Upload pela API (proxy):** daria controle total do tamanho, mas passa todos
  os vídeos pelo servidor. A URL com tamanho assinado dá o mesmo controle sem o
  tráfego.
- **Bucket público com domínio próprio:** URLs sem expiração, qualquer um
  baixaria. O bucket fica privado.

## Consequências

- A soma das cotas dos clientes pode passar do teto (as cotas são promessa; o
  teto é o limite físico). Quando o teto bate, o envio é recusado com mensagem
  clara, mesmo com cota sobrando.
- O inventário custa uma operação classe A por 1.000 arquivos por hora — dentro
  do gratuito.
- A cópia do Supabase lê cada arquivo inteiro em memória (um por vez). Com APKs de
  até 50 MB e vídeos curtos, aceitável.
- O token do R2 deve ter só "Object Read & Write" no bucket das mídias. Sem
  permissão de administrar o bucket, o CORS é feito à mão (ver `docs/IMPLANTACAO.md`).
