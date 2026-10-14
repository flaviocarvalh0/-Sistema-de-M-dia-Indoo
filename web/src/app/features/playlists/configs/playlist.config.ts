import { ColunaDef, FormItemConfig } from '@atlas/framework';

export const PLAYLIST_COLUNAS: ColunaDef[] = [
  { campo: 'nome', titulo: 'Playlist', principal: true, tipo: 'texto' },
  { campo: 'quantidadeItens', titulo: 'Itens', tipo: 'numero' },
  { campo: 'duracao', titulo: 'Ciclo', tipo: 'texto' },
  { campo: 'quantidadeTelas', titulo: 'Telas', tipo: 'numero' },
  { campo: 'noticias', titulo: 'Notícias', tipo: 'texto' },
  { campo: 'ativo', titulo: 'Situação', tipo: 'status' },
];

/** Espelha CriarPlaylistCommandValidator / AtualizarPlaylistCommandValidator. */
export function criarCamposPlaylist(): FormItemConfig[] {
  return [
    { type: 'texto', name: 'nome', label: 'Nome', required: true, col: 'col-12', placeholder: 'Ex.: Recepção — dia a dia' },
    { type: 'textarea', name: 'descricao', label: 'Descrição', col: 'col-12', hint: 'Opcional. Só aparece no painel.' },
    {
      type: 'numero', name: 'noticiaACadaAnuncios', label: 'Uma notícia a cada', col: 'col-md-6', value: 0, min: 0, max: 20,
      hint: 'Quantos anúncios entre uma notícia e outra. 0 = sem notícias.',
    },
    {
      type: 'numero', name: 'duracaoNoticiaSegundos', label: 'Tempo da notícia (s)', col: 'col-md-6', value: 10, min: 5, max: 60,
      hint: 'De 5 a 60 segundos na tela.',
    },
    {
      type: 'checkbox', name: 'embaralharAnuncios', label: 'Embaralhar', col: 'col-12', value: false,
      placeholder: 'Sortear a ordem dos anúncios a cada volta (os itens fixados na grade ficam no lugar)',
    },
    { type: 'checkbox', name: 'exibirRelogio', label: 'Relógio', col: 'col-md-6', value: true, placeholder: 'Mostrar o relógio na TV' },
    { type: 'checkbox', name: 'exibirClima', label: 'Clima', col: 'col-md-6', value: true, placeholder: 'Mostrar a temperatura da cidade do estabelecimento' },
  ];
}
