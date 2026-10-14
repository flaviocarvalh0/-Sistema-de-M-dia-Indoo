import { CampoOption, ColunaDef, FormItemConfig } from '@atlas/framework';
import { SITUACAO_TELA_BADGE } from '../../telas/configs/tela.config';

export const TELA_REDE_COLUNAS: ColunaDef[] = [
  { campo: 'parceiroNome', titulo: 'Estabelecimento', tipo: 'texto' },
  { campo: 'nome', titulo: 'Tela', principal: true, tipo: 'texto' },
  { campo: 'localizacao', titulo: 'Local', tipo: 'texto' },
  { campo: 'identificacaoAparelho', titulo: 'Aparelho', tipo: 'texto' },
  { campo: 'playlist', titulo: 'Playlist', tipo: 'texto' },
  { campo: 'situacaoRotulo', titulo: 'Conexão', tipo: 'badge', badgeMap: SITUACAO_TELA_BADGE },
  { campo: 'vistaPorUltimo', titulo: 'Última sincronização', tipo: 'texto' },
  { campo: 'ativo', titulo: 'Situação', tipo: 'status' },
];

/** Espelha CriarTelaCommandValidator / AtualizarTelaCommandValidator (Rede). */
export function criarCamposTelaRede(parceiros: CampoOption[], playlists: CampoOption[]): FormItemConfig[] {
  return [
    {
      type: 'select', name: 'parceiroId', label: 'Estabelecimento (host)', required: true, col: 'col-md-6', options: parceiros,
      hint: 'O parceiro que cede o espaço. O fuso horário da grade vem dele.',
    },
    {
      type: 'select', name: 'playlistId', label: 'Playlist', col: 'col-md-6', options: playlists,
      hint: 'O que esta TV toca. Sem playlist, ela mostra a tela de espera. Vale na próxima sincronização.',
    },
    { type: 'texto', name: 'nome', label: 'Nome', required: true, col: 'col-md-6', placeholder: 'Ex.: TV da recepção' },
    { type: 'texto', name: 'localizacao', label: 'Local', col: 'col-md-6', placeholder: 'Ex.: Térreo, ao lado do caixa' },
    {
      type: 'texto', name: 'identificacaoAparelho', label: 'Identificação do aparelho', col: 'col-12',
      placeholder: 'MAC, número de série ou etiqueta',
      hint: 'Só para controle da instalação. A TV é reconhecida pelo pareamento.',
    },
  ];
}
