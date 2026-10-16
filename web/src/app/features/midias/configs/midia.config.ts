import { ColunaDef, FormItemConfig } from '@atlas/framework';
import { FormGroup } from '@angular/forms';
import { MidiaDto, TipoQrCode } from '../models/midia.model';

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
  { campo: 'qrCode', titulo: 'QR code', tipo: 'texto' },
  { campo: 'troca', titulo: 'Troca de arquivo', tipo: 'texto' },
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

export const TIPO_QR_ROTULO: Record<TipoQrCode, string> = {
  Link: 'Link (site)',
  WhatsApp: 'WhatsApp',
  Instagram: 'Instagram',
  Telefone: 'Telefone',
  Pix: 'Pix',
};

const tipoQr = (form: FormGroup): TipoQrCode | null => form.get('qrTipo')?.value ?? null;
const exceto = (...tipos: TipoQrCode[]) => (form: FormGroup) => !tipos.includes(tipoQr(form) as TipoQrCode);

/**
 * QR code exibido por cima da mídia na TV. O tipo escolhido decide os campos
 * (espelha MontadorQrCode na API, que valida e monta o conteúdo).
 */
export function criarCamposQrCode(): FormItemConfig[] {
  return [
    {
      type: 'select', name: 'qrTipo', label: 'QR code', col: 'col-md-6',
      options: (Object.keys(TIPO_QR_ROTULO) as TipoQrCode[]).map(t => ({ value: t, label: TIPO_QR_ROTULO[t] })),
      hint: 'Em branco: sem QR code. Quem estiver na frente da TV lê com a câmera do celular.',
    },
    { type: 'texto', name: 'qrLink', label: 'Link', col: 'col-12', placeholder: 'https://…', hidden: exceto('Link') },
    {
      type: 'texto', name: 'qrTelefone', label: 'Telefone com DDD', col: 'col-md-6', placeholder: '(92) 99999-0000',
      hidden: exceto('WhatsApp', 'Telefone'),
    },
    {
      type: 'textarea', name: 'qrMensagem', label: 'Mensagem inicial', col: 'col-12',
      placeholder: 'Ex.: Quero o combo do dia!', hint: 'Opcional. Já vem escrita na conversa (até 200 caracteres).',
      hidden: exceto('WhatsApp'),
    },
    { type: 'texto', name: 'qrInstagram', label: 'Usuário do Instagram', col: 'col-md-6', placeholder: '@padariacentro', hidden: exceto('Instagram') },
    {
      type: 'texto', name: 'qrChavePix', label: 'Chave Pix', col: 'col-md-6',
      placeholder: 'CPF, CNPJ, e-mail, +55 telefone ou chave aleatória', hidden: exceto('Pix'),
    },
    { type: 'texto', name: 'qrNomeRecebedor', label: 'Nome de quem recebe', col: 'col-md-6', placeholder: 'Até 25 letras', hidden: exceto('Pix') },
    { type: 'texto', name: 'qrCidadeRecebedor', label: 'Cidade de quem recebe', col: 'col-md-6', placeholder: 'Até 15 letras', hidden: exceto('Pix') },
    {
      type: 'moeda', name: 'qrValor', label: 'Valor (opcional)', col: 'col-md-6',
      hint: 'Em branco: quem paga digita o valor.', hidden: exceto('Pix'),
    },
    {
      type: 'texto', name: 'qrLegenda', label: 'Texto embaixo do QR code', col: 'col-12',
      placeholder: 'Ex.: Peça pelo WhatsApp', hint: 'Opcional, até 40 caracteres. Em branco, vai um texto padrão do tipo.',
      hidden: form => !tipoQr(form),
    },
    {
      type: 'select', name: 'qrPosicao', label: 'Posição na tela', col: 'col-md-6', value: 'InferiorDireito',
      options: [
        { value: 'InferiorDireito', label: 'Canto inferior direito' },
        { value: 'InferiorEsquerdo', label: 'Canto inferior esquerdo' },
        { value: 'SuperiorDireito', label: 'Canto superior direito' },
        { value: 'SuperiorEsquerdo', label: 'Canto superior esquerdo' },
      ],
      hidden: form => !tipoQr(form),
    },
    {
      type: 'select', name: 'qrTamanho', label: 'Tamanho', col: 'col-md-6', value: 'Medio',
      options: [
        { value: 'Pequeno', label: 'Pequeno' },
        { value: 'Medio', label: 'Médio (recomendado)' },
        { value: 'Grande', label: 'Grande (para ler de longe)' },
      ],
      hidden: form => !tipoQr(form),
    },
  ];
}

/** Do DTO para os campos do formulário (o destino vai no campo do tipo). */
export function camposQrDaMidia(m: MidiaDto): Record<string, unknown> {
  const destino = m.qrDestino ?? null;
  return {
    qrTipo: m.qrTipo ?? null,
    qrLink: m.qrTipo === 'Link' ? destino : null,
    qrTelefone: m.qrTipo === 'WhatsApp' || m.qrTipo === 'Telefone' ? destino : null,
    qrInstagram: m.qrTipo === 'Instagram' ? (destino ? `@${destino}` : null) : null,
    qrChavePix: m.qrTipo === 'Pix' ? destino : null,
    qrMensagem: m.qrMensagem ?? null,
    qrNomeRecebedor: m.qrNomeRecebedor ?? null,
    qrCidadeRecebedor: m.qrCidadeRecebedor ?? null,
    qrValor: m.qrValor ?? null,
    qrLegenda: m.qrLegenda ?? null,
    qrPosicao: m.qrPosicao ?? 'InferiorDireito',
    qrTamanho: m.qrTamanho ?? 'Medio',
  };
}
