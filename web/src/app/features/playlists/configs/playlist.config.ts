import { ColunaDef, FormItemConfig } from '@atlas/framework';

export const PLAYLIST_COLUNAS: ColunaDef[] = [
  { campo: 'nome', titulo: 'Playlist', principal: true, tipo: 'texto' },
  { campo: 'quantidadeItens', titulo: 'Itens', tipo: 'numero' },
  { campo: 'duracao', titulo: 'Ciclo', tipo: 'texto' },
  { campo: 'quantidadeTelas', titulo: 'Telas', tipo: 'numero' },
  { campo: 'ativo', titulo: 'Situação', tipo: 'status' },
];

/** Espelha CriarPlaylistCommandValidator / AtualizarPlaylistCommandValidator. */
export function criarCamposPlaylist(): FormItemConfig[] {
  return [
    { type: 'texto', name: 'nome', label: 'Nome', required: true, col: 'col-12', placeholder: 'Ex.: Recepção — dia a dia' },
    { type: 'textarea', name: 'descricao', label: 'Descrição', col: 'col-12', hint: 'Opcional. Só aparece no painel.' },
  ];
}
