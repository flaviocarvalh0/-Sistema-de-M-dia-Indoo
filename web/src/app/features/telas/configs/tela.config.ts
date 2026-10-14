import { ColunaDef } from '@atlas/framework';

export const SITUACAO_TELA_BADGE: Record<string, string> = {
  'Aguardando pareamento': 'bg-secondary',
  Online: 'bg-success',
  Offline: 'bg-danger',
};

/** "Onde meus anúncios passam" (parceiro, somente leitura). */
export const TELA_COLUNAS: ColunaDef[] = [
  { campo: 'nome', titulo: 'Tela', principal: true, tipo: 'texto' },
  { campo: 'localizacao', titulo: 'Local', tipo: 'texto' },
  { campo: 'parceiroNome', titulo: 'Estabelecimento', tipo: 'texto' },
  { campo: 'situacaoRotulo', titulo: 'Conexão', tipo: 'badge', badgeMap: SITUACAO_TELA_BADGE },
  { campo: 'vistaPorUltimo', titulo: 'Última sincronização', tipo: 'texto' },
];
