import { CampoOption, ColunaDef, FormItemConfig } from '@atlas/framework';

export const SITUACAO_TELA_BADGE: Record<string, string> = {
  'Aguardando pareamento': 'bg-secondary',
  Online: 'bg-success',
  Offline: 'bg-danger',
};

/** Grid do parceiro (somente leitura). */
export const TELA_COLUNAS: ColunaDef[] = [
  { campo: 'nome', titulo: 'Tela', principal: true, tipo: 'texto' },
  { campo: 'localizacao', titulo: 'Local', tipo: 'texto' },
  { campo: 'playlist', titulo: 'Playlist', tipo: 'texto' },
  { campo: 'situacaoRotulo', titulo: 'Conexão', tipo: 'badge', badgeMap: SITUACAO_TELA_BADGE },
  { campo: 'vistaPorUltimo', titulo: 'Última sincronização', tipo: 'texto' },
  { campo: 'ativo', titulo: 'Situação', tipo: 'status' },
];

/** Único campo que o parceiro altera numa tela (DefinirPlaylistDaTelaCommand). */
export function criarCampoPlaylist(playlists: CampoOption[]): FormItemConfig[] {
  return [
    {
      type: 'select', name: 'playlistId', label: 'Playlist', col: 'col-12',
      options: playlists,
      hint: 'A TV passa a tocar esta grade na próxima sincronização. Sem playlist, ela mostra a tela de espera.',
    },
  ];
}
