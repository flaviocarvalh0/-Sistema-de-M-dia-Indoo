/**
 * Contratos das mídias (backend: Atlas.Application/Business/Indoor/Midias).
 * Enums chegam como NOME (JsonStringEnumConverter).
 */
export type TipoMidia = 'Video' | 'Imagem';
export type TipoQrCode = 'Link' | 'WhatsApp' | 'Instagram' | 'Telefone' | 'Pix';
export type PosicaoQrCode = 'InferiorDireito' | 'InferiorEsquerdo' | 'SuperiorDireito' | 'SuperiorEsquerdo';
export type TamanhoQrCode = 'Pequeno' | 'Medio' | 'Grande';

export interface MidiaDto {
  id: number;
  /** Cliente anunciante dono da mídia. */
  clienteId: number;
  clienteNome: string;
  /** Anunciante bloqueado: a mídia sai das TVs até o desbloqueio. */
  clienteBloqueado: boolean;
  titulo: string;
  tipo: TipoMidia;
  tipoConteudo: string;
  tamanhoBytes: number;
  hash: string;
  duracaoSegundos: number;
  inicioExibicao?: string | null;
  fimExibicao?: string | null;
  ativo: boolean;
  quantidadePlaylists: number;
  /** QR code por cima da mídia (nulo = sem QR code) e o que foi informado. */
  qrTipo?: TipoQrCode | null;
  /** Link, telefone, usuário do Instagram ou chave Pix, conforme o tipo. */
  qrDestino?: string | null;
  qrMensagem?: string | null;
  qrValor?: number | null;
  qrNomeRecebedor?: string | null;
  qrCidadeRecebedor?: string | null;
  qrLegenda?: string | null;
  qrPosicao?: PosicaoQrCode | null;
  qrTamanho?: TamanhoQrCode | null;
  /** O texto codificado (link, tel: ou Pix copia e cola). */
  qrConteudo?: string | null;
  /** Arquivo novo aguardando a aprovação da operadora (as TVs seguem com o atual). */
  trocaPendente?: boolean;
  pendenteTipo?: TipoMidia | null;
  pendenteTamanhoBytes?: number | null;
  pendenteEnviadoEm?: string | null;
  /** Por que a última troca foi recusada. */
  motivoRecusaTroca?: string | null;
  /** Só no detalhe da operadora: endereço temporário do arquivo pendente. */
  urlVisualizacaoPendente?: string | null;
  /** Só no detalhe: endereço temporário para pré-visualizar. */
  urlVisualizacao?: string | null;
  created: string;
  createdByName?: string | null;
  lastModified?: string | null;
  lastModifiedByName?: string | null;
  version: number;
}

/** Linha da grid: o DTO mais as colunas calculadas. */
export type LinhaMidia = MidiaDto & { tipoRotulo: string; tamanho: string; duracao: string; periodo: string; qrCode: string; troca: string };

/** Resposta da troca de arquivo: em grade, vai para a operadora aprovar. */
export interface SubstituicaoResultado {
  version: number;
  aguardandoAprovacao: boolean;
}

export interface UsoArmazenamento {
  usadoBytes: number;
  cotaBytes: number;
  tamanhoMaximoArquivoBytes: number;
  /** Mídias ativas agora e quantas o plano do cliente permite. */
  midiasAtivas: number;
  limiteMidiasAtivas: number;
}

export interface EnvioPreparado {
  chave: string;
  urlEnvio: string;
  expiraEm: string;
}

export interface NovaMidia {
  titulo: string;
  chave: string;
  tipoConteudo: string;
  hash: string | null;
  duracaoSegundos: number;
  inicioExibicao: string | null;
  fimExibicao: string | null;
}

/** `PUT {id}/arquivo`: troca o arquivo e mantém a mídia nas grades. */
export interface SubstituicaoArquivo {
  id: number;
  chave: string;
  tipoConteudo: string;
  hash: string | null;
  duracaoSegundos: number | null;
  version: number;
}
