import { CampoOption, ColunaDef, FormItemConfig } from '@atlas/framework';
import { SITUACAO_TELA_BADGE } from '../../telas/configs/tela.config';

export const TELA_REDE_COLUNAS: ColunaDef[] = [
  { campo: 'parceiroNome', titulo: 'Parceiro (local)', tipo: 'texto' },
  { campo: 'nome', titulo: 'Tela', principal: true, tipo: 'texto' },
  { campo: 'localizacao', titulo: 'Local', tipo: 'texto' },
  { campo: 'identificacaoAparelho', titulo: 'Aparelho', tipo: 'texto' },
  { campo: 'playlist', titulo: 'Playlist', tipo: 'texto' },
  { campo: 'situacaoRotulo', titulo: 'Conexão', tipo: 'badge', badgeMap: SITUACAO_TELA_BADGE },
  { campo: 'vistaPorUltimo', titulo: 'Última sincronização', tipo: 'texto' },
  { campo: 'diretorio', titulo: 'Diretório', tipo: 'texto' },
  { campo: 'versaoAppRotulo', titulo: 'App', tipo: 'texto' },
  { campo: 'ativo', titulo: 'Situação', tipo: 'status' },
];

/** Espelha CriarTelaCommandValidator / AtualizarTelaCommandValidator (Rede). */
export function criarCamposTelaRede(parceiros: CampoOption[], playlists: CampoOption[]): FormItemConfig[] {
  return [
    {
      type: 'select', name: 'parceiroId', label: 'Parceiro (local da TV)', required: true, col: 'col-md-6', options: parceiros,
      hint: 'O estabelecimento onde a TV fica (cadastro em Rede → Parceiros). O fuso da grade e a cidade do clima vêm dele.',
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
    {
      type: 'checkbox', name: 'diretorioAtivo', label: 'Diretório de anunciantes', col: 'col-12', value: false,
      placeholder: 'Mostrar na TV o QR code com os contatos dos anunciantes',
      hint: 'A página lista só os anunciantes desta grade que aceitaram aparecer (no perfil deles). Vale na próxima sincronização.',
    },
      {
      type: 'checkbox', name: 'tvDeTeste', label: 'TV de teste', col: 'col-12', value: false,
      placeholder: 'Recebe primeiro as versões novas do app',
      hint: 'Versões liberadas "Só TVs de teste" (Rede → Telas → Versões do app) instalam apenas nas telas marcadas.',
    },
  ];
}
