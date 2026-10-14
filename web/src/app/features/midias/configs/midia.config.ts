import { ColunaDef, FormItemConfig } from '@atlas/framework';

export const TIPO_MIDIA_BADGE: Record<string, string> = {
  'Vídeo': 'bg-primary',
  Imagem: 'bg-info text-dark',
};

export const MIDIA_COLUNAS: ColunaDef[] = [
  { campo: 'titulo', titulo: 'Título', principal: true, tipo: 'texto' },
  { campo: 'tipoRotulo', titulo: 'Tipo', tipo: 'badge', badgeMap: TIPO_MIDIA_BADGE },
  { campo: 'duracao', titulo: 'Duração', tipo: 'texto' },
  { campo: 'tamanho', titulo: 'Tamanho', tipo: 'texto' },
  { campo: 'periodo', titulo: 'Período', tipo: 'texto' },
  { campo: 'quantidadePlaylists', titulo: 'Em grades', tipo: 'numero' },
  { campo: 'ativo', titulo: 'Situação', tipo: 'status' },
];

/** Duração máxima aceita pela API (CriarMidiaCommandValidator). */
export const DURACAO_MAXIMA_SEGUNDOS = 3600;

/**
 * Título, duração e período. Espelha AtualizarMidiaCommandValidator: o
 * arquivo não muda depois do envio — para trocar, envie outra mídia.
 */
export function criarCamposMidia(video: boolean): FormItemConfig[] {
  return [
    { type: 'texto', name: 'titulo', label: 'Título', required: true, col: 'col-md-8', placeholder: 'Ex.: Promoção de inverno' },
    {
      type: 'numero', name: 'duracaoSegundos', label: 'Duração (s)', required: true, col: 'col-md-4', value: 15,
      min: 1, max: DURACAO_MAXIMA_SEGUNDOS,
      hint: video ? 'Tempo que o vídeo fica na tela.' : 'Tempo que a imagem fica na tela.',
    },
    { type: 'data', name: 'inicioExibicao', label: 'Exibir a partir de', col: 'col-md-6', hint: 'Vazio: desde já.' },
    { type: 'data', name: 'fimExibicao', label: 'Exibir até', col: 'col-md-6', hint: 'Vazio: sem data para sair.' },
  ];
}
