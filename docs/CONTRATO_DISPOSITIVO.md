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

- `400` — código inválido, expirado, já usado ou de tela inativa (sempre a
  mesma mensagem). O bloqueio do estabelecimento (host) não impede parear.
- `429` — mais de 10 tentativas por IP em 5 minutos.

Guardar `token` e `dispositivoId` em armazenamento seguro
(`flutter_secure_storage`). O `dispositivoId` é gerado uma vez e não muda.

## 2. Sincronização — `GET /sincronizacao`

Cabeçalho `Authorization: Bearer <token>`. Parâmetros opcionais:

| Parâmetro | Uso |
|---|---|
| `versaoGrade` | A última `versaoGrade` recebida. Se nada mudou, vem `alterada: false` sem itens. |
| `versaoNoticias` | A última `versaoNoticias` recebida. Se as notícias não mudaram, `noticias` vem nulo. |
| `versaoApp` | Aparece no painel. |
| `versaoCodigoApp` | O `versionCode` do app instalado (o `+N` do pubspec). Liga a atualização automática (ADR-021); sem ele, `atualizacao` vem sempre nulo. |
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
      "posicaoFixa": false,
      "qrCode": {
        "conteudo": "https://wa.me/5592999990000?text=Quero%20o%20caf%C3%A9",
        "legenda": "Fale no WhatsApp",
        "posicao": "InferiorDireito",
        "tamanho": "Medio"
      },
      "inicioExibicao": null,
      "fimExibicao": null,
      "exibirHoje": true
    }
  ],
  "composicao": {
    "noticiaACadaAnuncios": 3,
    "duracaoNoticiaSegundos": 10,
    "embaralharAnuncios": true,
    "duracaoMaximaVoltaSegundos": 180,
    "exibirRelogio": true,
    "exibirClima": true,
    "cartaoTempo": true,
    "cartaoCotacoes": true
  },
  "versaoNoticias": "<sha-256>",
  "noticias": [
    {
      "id": 81,
      "titulo": "Campanha de vacinação começa segunda",
      "credito": "Fonte: Agência Brasil",
      "publicadaEm": "2026-10-14T11:30:00+00:00",
      "url": "https://<projeto>.supabase.co/storage/v1/object/sign/midias/noticias/1/...jpg?token=...",
      "hash": "<sha-256 da imagem>",
      "tamanhoBytes": 182340,
      "tipoConteudo": "image/jpeg"
    }
  ],
  "clima": {
    "temperaturaC": 31.4,
    "minimaC": 24.0,
    "maximaC": 33.2,
    "codigo": 2,
    "descricao": "Parcialmente nublado",
    "icone": "sol-nuvem",
    "local": "Manaus, Amazonas",
    "atualizadaEm": "2026-10-14T11:12:00+00:00",
    "credito": "Clima: Open-Meteo",
    "dias": [
      { "data": "2026-10-14", "minimaC": 24.1, "maximaC": 33.9, "codigo": 80, "descricao": "Pancadas de chuva fracas", "icone": "chuva" },
      { "data": "2026-10-15", "minimaC": 24.0, "maximaC": 34.2, "codigo": 0, "descricao": "Céu limpo", "icone": "sol" }
    ]
  },
  "cotacoes": [
    {
      "moeda": "USD",
      "nome": "Dólar",
      "compra": 5.5150,
      "venda": 5.5156,
      "variacaoPercentual": 1.02,
      "cotadaEm": "2026-10-14T14:04:59+00:00",
      "boletim": "Intermediário",
      "credito": "Fonte: Banco Central (PTAX)"
    }
  ],
  "diretorio": {
    "url": "https://tv.suaempresa.com.br/d/K7M2Q9XHAB",
    "legenda": "Contatos dos anunciantes"
  },
  "atualizacao": {
    "codigo": 7,
    "nome": "1.2.0",
    "url": "https://<storage>/app/3f2a….apk?token=…",
    "hash": "<sha-256 do APK>",
    "tamanhoBytes": 21450112
  }
}
```

- `composicao` e `clima` vêm em toda resposta `200`. `clima` é nulo quando a
  playlist não usa o clima do canto nem o cartão do tempo, ou quando não há
  leitura recente (6 h). `dias` traz hoje e os próximos dias.
- `cotacoes` vem em toda resposta quando a playlist usa o cartão de cotações
  (nulo quando não usa): dólar e euro, só com cotação dos últimos 4 dias.
- `diretorio` vem em toda resposta `200` quando a operadora ligou o
  diretório na tela (nulo se desligado ou se a API não tem endereço público).
  Não entra na `versaoGrade`.
- `atualizacao` vem em toda resposta `200` quando há uma versão do app
  liberada para esta TV (Todas, ou Teste se a tela é TV de teste) com
  `codigo` maior que o `versaoCodigoApp` enviado; nulo caso contrário. Não
  entra na `versaoGrade`. A `url` vale 24 h, como a das mídias.
- `qrCode` do item é nulo quando a mídia não tem QR code. `conteudo` é o
  texto a codificar, já pronto (link, `tel:` ou Pix copia e cola);
  `posicao` é `InferiorDireito`, `InferiorEsquerdo`, `SuperiorDireito` ou
  `SuperiorEsquerdo`; `tamanho` é `Pequeno`, `Medio` ou `Grande`. Mudar o
  QR code muda a `versaoGrade`.
- `noticias` vem só quando a `versaoNoticias` mudou: vazia quando a playlist
  não intercala notícias. Notícia sem foto vem com `url`, `hash`,
  `tamanhoBytes` e `tipoConteudo` nulos.

| Resposta | O que o app faz |
|---|---|
| `200` com `alterada: true` | Troca a grade local pela recebida (ver regras abaixo). |
| `200` com `alterada: false` | Nada a fazer com a grade (`itens` vem nulo). Notícias, composição e clima valem do mesmo jeito. |
| `novoToken` preenchido | Substitui o token guardado (vem quando falta menos de 1/4 da validade). |
| `401` | Credencial revogada (TV desvinculada ou outra pareada no lugar): apagar o token e voltar à tela de pareamento. **Manter os arquivos e continuar tocando** até parear. |
| `403` | Tela inativa: manter tudo e continuar tocando o que já tem; tentar de novo no próximo ciclo. Host ou anunciante bloqueado não dá 403: as mídias de anunciante bloqueado só deixam de vir nos `itens`. |
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

   "Hoje" é a data no `fusoHorario` do estabelecimento (host) da tela. `exibirHoje` já vem
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
   próximo já sai da grade nova. No carrossel (regra 10), a volta em andamento
   vai até o fim (pulando quem saiu da grade) e a grade nova entra na volta
   seguinte — remontar no meio gastaria o cursor sem exibir os anúncios. Eventos repetidos do player (fim de vídeo
   avisado duas vezes, timer de segurança de um item que já acabou) não podem
   avançar o loop mais de uma vez.
10. **Fila montada na TV, a cada volta** (ADR-016), com os anúncios do dia:
    - `embaralharAnuncios`: sortear a ordem a cada volta; itens com
      `posicaoFixa` ficam na posição deles;
    - `duracaoMaximaVoltaSegundos` > 0: **carrossel com cursor** (ADR-019).
      Os itens com `posicaoFixa` entram em toda volta, na posição deles; os
      outros formam uma lista circular (embaralhada se `embaralharAnuncios`,
      senão na ordem da grade). Cada volta puxa a partir do cursor enquanto a
      soma das durações (fixos inclusos, informativos não) couber no limite —
      pelo menos um por volta — e o cursor avança. No fim da lista, embaralha
      de novo e recomeça, sem repetir um anúncio na mesma volta nem emendar o
      mesmo duas vezes seguidas. Grade nova: quem saiu sai da lista, quem
      entrou entra no trecho que ainda não passou. 0 = todos em toda volta;
    - `noticiaACadaAnuncios` > 0: um informativo depois de cada N anúncios,
      por `duracaoNoticiaSegundos`, no rodízio notícia, tempo, notícia,
      cotações (só os cartões ligados e com dados; sem notícias, só os
      cartões). As notícias seguem rodízio próprio, mais recentes primeiro. A
      contagem continua de uma volta para a outra;
    - sem anúncios para hoje e com informativos, tocar só os informativos.
11. **Notícias:** imagem baixada como as mídias (regra 2), numa pasta
    própria; a lista nova só entra com as imagens prontas. Imagem que falhar
    fica de fora e a versão não é guardada (o próximo ciclo pede de novo).
    Notícia com mais de 48 h (`publicadaEm`) sai da tela, mesmo offline.
    Mostrar sempre o `credito`.
12. **Relógio e clima:** por cima de tudo, se `exibirRelogio`/`exibirClima`.
    A hora é a do `fusoHorario` (não a do aparelho). O clima guardado vale até
    6 h depois de `atualizadaEm`, com o `credito` ao lado.
13. **Cartões:** "Previsão do tempo" (agora, mínima e máxima de hoje e os
    próximos 3 dias de `clima.dias`) e "Cotações" (venda de cada moeda e a
    variação do dia). Guardar o último recebido e mostrar enquanto valer
    (clima 6 h; cotação 4 dias), sempre com o `credito`. Sem dado válido, o
    cartão sai do rodízio.
14. **QR code do anúncio:** com `qrCode`, a TV gera o QR (offline) e o
    desenha por cima da mídia, no canto pedido, num cartão branco com a
    legenda embaixo. Lado do QR: 22%, 30% ou 40% da menor dimensão da tela
    (pequeno, médio, grande), com margem branca e correção de erro M. O
    relógio vai para o outro canto de cima se o QR ocupar o dele. Notícias e
    cartões não têm QR.
15. **Diretório da TV** (ADR-018): com `diretorio`, a TV gera um QR code
    pequeno (22% da menor dimensão) com a `url`, no canto inferior direito,
    com a `legenda`. Aparece nos anúncios **sem** QR code próprio e na tela
    de espera; some nos anúncios com QR (o do anúncio vale), nas notícias e
    nos cartões. Guardar o último recebido (funciona offline); nulo na
    resposta apaga.
16. **Atualização do app** (ADR-021): com `atualizacao`, baixar o APK numa
    pasta própria com as regras 2 e 4 e entregar ao instalador do Android
    (`PackageInstaller`). Sem `atualizacao`, apagar APKs guardados. Falhou
    (download, instalação, confirmação recusada)? Tentar a mesma versão de
    novo só depois de 1 hora. A atualização não interrompe nada até o Android
    instalar; depois o app reabre sozinho (`MY_PACKAGE_REPLACED`).
