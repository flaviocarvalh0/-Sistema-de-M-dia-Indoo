# ADR-012: Armazenamento Externo das Mídias com URLs Assinadas

## Status

Aceito

## Data da decisão

2026-10-13

## Contexto

Cada parceiro envia vídeos (MP4) e imagens (JPG/PNG) de até 50 MB, e cada TV
baixa esses arquivos periodicamente. Guardar os arquivos no PostgreSQL ou no
disco do servidor da API faria a API carregar todo o tráfego de envio e de
download, crescer o banco/backup com binários e amarrar a API a uma máquina
(disco local não escala nem sobrevive a troca de servidor).

Restrições do início do produto:

- Custo zero ou quase zero (o plano gratuito do Supabase oferece 1 GB de
  storage e 50 MB por arquivo);
- Arquivos privados: só o parceiro e as TVs dele podem baixar;
- A TV precisa conferir a integridade de cada arquivo (rede ruim, download
  interrompido);
- A cota por parceiro precisa ser respeitada antes do envio.

## Decisão

As mídias ficam num **bucket privado do Supabase Storage**, acessado só pela
API com a chave `service_role`. O arquivo **não passa pela API**:

1. **Envio:** `POST /midias/envio` confere tipo, tamanho e cota do parceiro e
   devolve uma URL de envio assinada pelo storage. O navegador faz o `PUT` do
   arquivo direto nela.
2. **Cadastro:** `POST /midias` com o SHA-256 calculado no navegador. A API lê
   o arquivo enviado, confere o tipo real (assinatura do MP4/JPG/PNG), o
   tamanho e o hash, grava o hash calculado e só então cadastra a mídia.
   Arquivo recusado é apagado do bucket.
3. **Leitura:** o painel recebe uma URL de leitura assinada de 1 h para
   pré-visualizar; a TV recebe, na sincronização, URLs de 24 h
   (`Indoor:Dispositivo:ValidadeUrlDownloadHoras`) geradas em lote numa
   chamada só ao storage.
4. **Integridade:** a TV compara o SHA-256 do arquivo baixado com o `hash` da
   grade antes de usar.

O acesso ao storage fica atrás de `IArmazenamentoMidias` (Application), com a
implementação `ArmazenamentoSupabase` (Infrastructure). Sem configuração, a
API sobe com `ArmazenamentoNaoConfigurado`, que responde 503 nas rotas que
precisam do storage.

## Justificativa

- URL assinada tira a API do caminho dos bytes: ela só autoriza e confere.
- O bucket privado com URLs de validade curta impede links permanentes.
- O SHA-256 gravado pela API (não o informado pelo navegador) é a referência
  que a TV confere.
- A interface permite trocar o provedor (S3, R2, Azure Blob, disco) sem mexer
  em regras de negócio.

Alternativas rejeitadas:

- **Arquivos no PostgreSQL (bytea):** banco e backups inchados, API no caminho
  de todo download.
- **Disco do servidor da API:** sem redundância, difícil de escalar e de
  migrar; API no caminho dos bytes.
- **Bucket público:** qualquer um com o link baixaria o conteúdo dos parceiros.

## Consequências

### Ficou mais fácil

- Envio e download de arquivos grandes sem carregar a API.
- Trocar de provedor de storage implementando uma interface.
- Validar cota e tipo antes de gastar banda.

### Ficou mais difícil

- Um serviço externo a mais para configurar, monitorar e pagar quando crescer
  (o plano gratuito tem limite de 1 GB no total).
- O envio depende do CORS do storage aceitar o domínio do painel.
- URLs vencem: a TV precisa sincronizar de novo para baixar depois de 24 h.
- A chave `service_role` ignora as políticas do bucket: é segredo crítico
  (ADR-008) e nunca vai ao navegador nem às TVs.

## Observações de implementação

- Configuração: seção `Indoor:Armazenamento:Supabase` (`Url`, `Bucket`,
  `CriarBucket`) e o segredo `Indoor:Armazenamento:Supabase:ChaveServico`.
- `InicializadorBucket` cria o bucket privado na subida quando `CriarBucket`
  é verdadeiro.
- Chaves no bucket: `parceiros/{parceiroId}/midias/{guid}.{ext}`.
- Teste de contrato contra o Supabase real:
  `ArmazenamentoSupabaseContratoTests` (pulado sem as variáveis
  `INDOOR_SUPABASE_URL` e `INDOOR_SUPABASE_CHAVE_SERVICO`).

## Referências

- [ADR-008](ADR-008-Gestao-de-Configuracoes-e-Segredos.md) — Gestão de Configurações e Segredos
- [ADR-013](ADR-013-Player-Offline-First.md) — Player offline-first
- Supabase Storage — URLs assinadas de envio e leitura
