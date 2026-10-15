import { ColunaDef, FormItemConfig } from '@atlas/framework';

export const PLAYLIST_COLUNAS: ColunaDef[] = [
  { campo: 'nome', titulo: 'Playlist', principal: true, tipo: 'texto' },
  { campo: 'quantidadeItens', titulo: 'Itens', tipo: 'numero' },
  { campo: 'duracao', titulo: 'Ciclo', tipo: 'texto' },
  { campo: 'quantidadeTelas', titulo: 'Telas', tipo: 'numero' },
  { campo: 'noticias', titulo: 'Informativos', tipo: 'texto' },
  { campo: 'ativo', titulo: 'Situação', tipo: 'status' },
];

/** Espelha CriarPlaylistCommandValidator / AtualizarPlaylistCommandValidator. */
export function criarCamposPlaylist(): FormItemConfig[] {
  return [
    { type: 'texto', name: 'nome', label: 'Nome', required: true, col: 'col-12', placeholder: 'Ex.: Recepção — dia a dia' },
    { type: 'textarea', name: 'descricao', label: 'Descrição', col: 'col-12', hint: 'Opcional. Só aparece no painel.' },
    {
      type: 'numero', name: 'noticiaACadaAnuncios', label: 'Um informativo a cada', col: 'col-md-6', value: 0, min: 0, max: 20,
      hint: 'Anúncios entre um informativo e outro (notícia, tempo ou cotações). 0 = sem informativos.',
    },
    {
      type: 'numero', name: 'duracaoNoticiaSegundos', label: 'Tempo do informativo (s)', col: 'col-md-6', value: 10, min: 5, max: 60,
      hint: 'De 5 a 60 segundos na tela.',
    },
    {
      type: 'checkbox', name: 'cartaoTempo', label: 'Cartão do tempo', col: 'col-md-6', value: false,
      placeholder: 'Previsão do tempo (hoje e próximos dias) no rodízio',
    },
    {
      type: 'checkbox', name: 'cartaoCotacoes', label: 'Cartão de cotações', col: 'col-md-6', value: false,
      placeholder: 'Dólar e euro (Banco Central) no rodízio',
    },
    {
      type: 'checkbox', name: 'embaralharAnuncios', label: 'Embaralhar', col: 'col-12', value: false,
      placeholder: 'Sortear a ordem dos anúncios a cada volta (os itens fixados na grade ficam no lugar)',
    },
    {
      type: 'numero', name: 'duracaoMaximaVoltaSegundos', label: 'Tamanho da volta (s)', col: 'col-12', value: 0, min: 0, max: 3600,
      hint: 'Com muitos anúncios: segundos de anúncios por volta (ex.: 180). A TV passa os próximos da fila a cada volta e só repete '
        + 'quando todos passaram — mesma frequência para todos. Os fixados (📌) entram em toda volta. 0 = todos em toda volta.',
    },
    { type: 'checkbox', name: 'exibirRelogio', label: 'Relógio', col: 'col-md-6', value: true, placeholder: 'Mostrar o relógio na TV' },
    { type: 'checkbox', name: 'exibirClima', label: 'Clima', col: 'col-md-6', value: true, placeholder: 'Mostrar a temperatura da cidade do estabelecimento' },
  ];
}
