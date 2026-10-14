# Contrato do app das TVs

Referência para o `player/` (Flutter). Base: `{api}/api/v1/dispositivo`. JSON
em camelCase.

## 1. Pareamento — `POST /autenticar` (anônimo)

O painel gera um código de 8 caracteres (válido por 24 h, uso único). A
pessoa digita na TV; o app aceita minúsculas, espaços e hífen
(`ab12-cd34` = `AB12CD34`).

```json
{ "codigoPareamento": "AB12CD34", "dispositivoId": "<uuid gerado na instalação>", "versaoApp": "1.0.0" }
```

`200`:

```json
{
  "token": "<JWT>",
  "expiraEm": "2026-11-12T18:21:47-03:00",
  "telaId": 12,
  "nomeTela": "Recepção",
  "nomeParceiro": "Academia Centro",
  "intervaloSincronizacaoSegundos": 60
}
```

- `400` — código inválido, expirado, já usado, de tela inativa ou de parceiro
  bloqueado (sempre a mesma mensagem).
- `429` — mais de 10 tentativas por IP em 5 minutos.

Guardar `token` e `dispositivoId` em armazenamento seguro
(`flutter_secure_storage`). O `dispositivoId` é gerado uma vez e não muda.

## 2. Sincronização — `GET /sincronizacao`

Cabeçalho `Authorization: Bearer <token>`. Parâmetros opcionais:

| Parâmetro | Uso |
|---|---|
| `versaoGrade` | A última `versaoGrade` recebida. Se nada mudou, vem `alterada: false` sem itens. |
| `versaoApp` | Aparece no painel. |
| `itensProntos` | Quantas mídias estão baixadas e conferidas (aparece no painel). |

`200`:

```json
{
  "alterada": true,
  "versaoGrade": "<sha-256>",
  "geradoEm": "2026-10-13T21:21:47Z",
  "hoje": "2026-10-13",
  "fusoHorario": "America/Manaus",
  "intervaloSincronizacaoSegundos": 60,
  "nomeTela": "Recepção",
  "novoToken": null,
  "itens": [
    {
      "ordem": 0,
      "midiaId": 4,
      "tipo": "Imagem",
      "tipoConteudo": "image/png",
      "url": "https://<projeto>.supabase.co/storage/v1/object/sign/midias/...?token=...",
      "hash": "<sha-256 do arquivo>",
      "tamanhoBytes": 40008,
      "duracaoSegundos": 10,
      "diasSemana": 127,
      "inicioExibicao": null,
      "fimExibicao": null,
      "exibirHoje": true
    }
  ]
}
```

| Resposta | O que o app faz |
|---|---|
| `200` com `alterada: true` | Troca a grade local pela recebida (ver regras abaixo). |
| `200` com `alterada: false` | Nada a fazer (`itens` vem nulo). |
| `novoToken` preenchido | Substitui o token guardado (vem quando falta menos de 1/4 da validade). |
| `401` | Credencial revogada (TV desvinculada ou outra pareada no lugar): apagar o token e voltar à tela de pareamento. **Manter os arquivos e continuar tocando** até parear. |
| `403` | Tela inativa ou parceiro bloqueado: manter tudo e continuar tocando o que já tem; tentar de novo no próximo ciclo. |
| `429`, `5xx`, sem rede | Manter tudo; tentar de novo no próximo ciclo. |

## 3. Regras do player

1. **Só toca arquivo local.** Nunca streaming da `url`. A `url` serve só para
   baixar e vale 24 h.
2. **Download seguro:** baixar para `<arquivo>.part`, calcular o SHA-256 em
   streaming e comparar com `hash`. Bateu: renomear para o nome final. Não
   bateu: apagar e tentar de novo (até 3 vezes, com espera crescente).
3. **Troca atômica:** a grade nova só passa a valer quando todos os arquivos
   dela estiverem prontos; até lá continua a antiga. Arquivos que saíram da
   grade são apagados depois da troca.
4. **URL vencida** (download dá 400/403 no storage): sincronizar de novo
   **sem** `versaoGrade` para receber URLs novas.
5. **Filtro do dia, sempre local:** a lista traz tudo o que ainda pode tocar
   (inclusive o que começa nos próximos dias). A cada volta do loop, e na
   virada do dia, tocar só os itens com:
   - o dia da semana de hoje em `diasSemana` (bits: domingo = 1, segunda = 2,
     terça = 4, quarta = 8, quinta = 16, sexta = 32, sábado = 64), e
   - `inicioExibicao` vazio ou ≤ hoje, e `fimExibicao` vazio ou ≥ hoje.

   "Hoje" é a data no `fusoHorario` do parceiro. `exibirHoje` já vem
   calculado pelo servidor para o dia de `hoje`, mas sem internet a TV
   precisa recalcular.
6. **Loop:** ordem crescente de `ordem`. Vídeo avança no fim do vídeo
   (`duracaoSegundos` é só um limite de segurança); imagem fica
   `duracaoSegundos` na tela. Arquivo que falhar ao abrir é pulado.
7. **Grade vazia ou sem nada para hoje:** mostrar a tela de espera com o nome
   da tela.
8. **Intervalo:** sincronizar a cada `intervaloSincronizacaoSegundos` (padrão
   60 s) e ao ligar. Sem sincronizar por 5 min, o painel mostra a TV como
   Offline.
9. **Grade nova não interrompe:** o item que está na tela vai até o fim; o
   próximo já sai da grade nova. Eventos repetidos do player (fim de vídeo
   avisado duas vezes, timer de segurança de um item que já acabou) não podem
   avançar o loop mais de uma vez.
