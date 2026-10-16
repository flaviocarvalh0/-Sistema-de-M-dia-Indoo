# ADR-020: Aprovação da Troca de Arquivo e Alerta de TV Fora do Ar

## Status

Aceito

## Data da decisão

2026-10-16

## Contexto

Dois riscos apareceram antes do piloto com clientes reais:

1. **Troca de arquivo sem revisão.** O anunciante pode trocar o arquivo de uma
   mídia que já está numa playlist (ADR-015). A troca ia direto para as TVs:
   um arquivo errado ou impróprio aparecia no estabelecimento de outra pessoa
   sem a operadora ver.
2. **TV parada sem ninguém saber.** A TV continua tocando o que já baixou sem
   internet (ADR-013), mas para de receber grade nova. A situação "Offline"
   só aparece no painel; ninguém olha o painel o dia todo.

## Decisão

### Aprovação da troca

- Mídia **que está em alguma playlist**: o arquivo novo fica **pendente**
  (`Pendente*` na `Midia`). As TVs seguem com o atual. A operadora recebe um
  e-mail (`Indoor:Alertas:Emails`) e vê o cartão "Trocas de arquivo para
  aprovar" em Rede → Playlists, com o atual e o novo lado a lado.
  - **Aprovar**: o pendente vira o arquivo da mídia (hash novo, as TVs baixam
    na próxima sincronização) e o antigo é apagado do storage.
  - **Recusar** com motivo (até 500 caracteres): o pendente é apagado e o
    anunciante recebe o motivo por e-mail.
  - Nova troca antes da decisão substitui o pendente (o anterior é apagado).
- Mídia **fora de playlist**: troca na hora, como antes (ninguém a vê ainda).
- A cota de armazenamento conta o arquivo pendente.

### Alerta de TV fora do ar

- `MonitorTelas` (serviço em segundo plano, a cada 5 min, com
  `pg_try_advisory_lock` para rodar em uma instância só) procura TVs ativas e
  pareadas sem sincronizar há mais de `Indoor:Alertas:TvOfflineMinutos`
  (padrão 15).
- Só alerta **dentro do horário** `Indoor:Alertas:HoraInicio`–`HoraFim`
  (padrão 7–22) **no fuso do estabelecimento**: TV desligada de madrugada não
  é problema.
- Um **e-mail-resumo** por varredura com as que caíram e as que voltaram.
  `EstadoTela.AlertaOfflineEm` marca o alerta em aberto: a mesma queda não
  gera outro e-mail; ao voltar, sai no resumo seguinte e a marca é limpa.
- Sem e-mail configurado, só registra no log.

## Justificativa

- O conteúdo exibido no espaço de um parceiro é responsabilidade da operadora;
  a revisão só onde ele já está no ar mantém o envio inicial simples.
- E-mail usa a fila que a plataforma já tem (ADR-010), sem serviço novo.
- O resumo por varredura evita uma rajada de e-mails quando a internet de um
  bairro cai.

Alternativas rejeitadas:

- **Aprovar todo envio, inclusive o primeiro:** a mídia nova só entra na grade
  pela própria operadora, que já a vê ao montar a playlist.
- **Alerta por WhatsApp/SMS:** custo e integração extra; o e-mail resolve no
  piloto e o alerta é um ponto só (`VerificadorTelasOffline`).

## Consequências

### Ficou mais fácil

- Trocar um anúncio no ar com segurança.
- Saber em até ~20 minutos que uma TV parou.

### Ficou mais difícil

- A troca de uma mídia em grade depende de alguém da operadora aprovar.
- É preciso configurar os e-mails da operadora e o SMTP para os alertas.

## Referências

- [ADR-010](ADR-010-Estrategia-de-Mensagens-e-Entrega-de-Emails.md) — Mensagens e e-mails
- [ADR-013](ADR-013-Player-Offline-First.md) — Player offline-first
- [ADR-015](ADR-015-Playlists-da-Operadora-e-Plano-do-Anunciante.md) — Playlists da operadora
