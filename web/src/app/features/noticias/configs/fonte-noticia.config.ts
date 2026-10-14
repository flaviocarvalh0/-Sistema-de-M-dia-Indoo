import { ColunaDef, FormItemConfig } from '@atlas/framework';

export const SITUACAO_COLETA_BADGE: Record<string, string> = {
  Ok: 'bg-success',
  'Com erro': 'bg-danger',
  Aguardando: 'bg-secondary',
};

export const FONTE_NOTICIA_COLUNAS: ColunaDef[] = [
  { campo: 'nome', titulo: 'Fonte', principal: true, tipo: 'texto' },
  { campo: 'credito', titulo: 'Crédito na TV', tipo: 'texto' },
  { campo: 'quantidadeNoticias', titulo: 'Notícias', tipo: 'numero' },
  { campo: 'situacaoColeta', titulo: 'Última leitura', tipo: 'badge', badgeMap: SITUACAO_COLETA_BADGE },
  { campo: 'ultimaColeta', titulo: 'Quando', tipo: 'texto' },
  { campo: 'ativo', titulo: 'Situação', tipo: 'status' },
];

/** Espelha CriarFonteNoticiaCommandValidator / AtualizarFonteNoticiaCommandValidator. */
export function criarCamposFonteNoticia(): FormItemConfig[] {
  return [
    { type: 'texto', name: 'nome', label: 'Nome', required: true, col: 'col-md-6', placeholder: 'Ex.: Agência Brasil' },
    {
      type: 'texto', name: 'credito', label: 'Crédito exibido na TV', required: true, col: 'col-md-6',
      placeholder: 'Ex.: Fonte: Agência Brasil', hint: 'A licença do conteúdo costuma exigir o crédito.',
    },
    {
      type: 'texto', name: 'urlFeed', label: 'Endereço do feed (RSS ou Atom)', required: true, col: 'col-12',
      placeholder: 'https://…/feed.xml',
      hint: 'Use só fontes com licença para exibição pública (ex.: Creative Commons). A responsabilidade é de quem cadastra.',
    },
    { type: 'checkbox', name: 'ativo', label: 'Ativa', col: 'col-12', value: true, placeholder: 'Ativa (lida a cada hora e enviada às TVs)' },
  ];
}
