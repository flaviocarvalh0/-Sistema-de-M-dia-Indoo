import { CampoOption, ColunaDef, FormItemConfig } from '@atlas/framework';

export const SITUACAO_TELA_BADGE: Record<string, string> = {
  'Aguardando pareamento': 'bg-secondary',
  Online: 'bg-success',
  Offline: 'bg-danger',
};

export const TELA_COLUNAS: ColunaDef[] = [
  { campo: 'nome', titulo: 'Tela', principal: true, tipo: 'texto' },
  { campo: 'localizacao', titulo: 'Local', tipo: 'texto' },
  { campo: 'playlist', titulo: 'Playlist', tipo: 'texto' },
  { campo: 'situacaoRotulo', titulo: 'Conexão', tipo: 'badge', badgeMap: SITUACAO_TELA_BADGE },
  { campo: 'vistaPorUltimo', titulo: 'Última sincronização', tipo: 'texto' },
  { campo: 'ativo', titulo: 'Situação', tipo: 'status' },
];

/** Espelha CriarTelaCommandValidator / AtualizarTelaCommandValidator. */
export function criarCamposTela(playlists: CampoOption[]): FormItemConfig[] {
  return [
    { type: 'texto', name: 'nome', label: 'Nome', required: true, col: 'col-md-6', placeholder: 'Ex.: TV da recepção' },
    { type: 'texto', name: 'localizacao', label: 'Local', col: 'col-md-6', placeholder: 'Ex.: Térreo, ao lado do caixa' },
    {
      type: 'select', name: 'playlistId', label: 'Playlist', col: 'col-12',
      options: playlists,
      hint: 'A mesma playlist pode tocar em várias telas. Sem playlist, a TV fica sem grade.',
    },
  ];
}
