/**
 * Contratos das mídias (backend: Atlas.Application/Business/Indoor/Midias).
 * Enums chegam como NOME (JsonStringEnumConverter).
 */
export type TipoMidia = 'Video' | 'Imagem';

export interface MidiaDto {
  id: number;
  /** Parceiro anunciante dono da mídia. */
  parceiroId: number;
  parceiroNome: string;
  /** Anunciante bloqueado: a mídia sai das TVs até o desbloqueio. */
  parceiroBloqueado: boolean;
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
  /** Só no detalhe: endereço temporário para pré-visualizar. */
  urlVisualizacao?: string | null;
  created: string;
  createdByName?: string | null;
  lastModified?: string | null;
  lastModifiedByName?: string | null;
  version: number;
}

/** Linha da grid: o DTO mais as colunas calculadas. */
export type LinhaMidia = MidiaDto & { tipoRotulo: string; tamanho: string; duracao: string; periodo: string };

export interface UsoArmazenamento {
  usadoBytes: number;
  cotaBytes: number;
  tamanhoMaximoArquivoBytes: number;
  /** Mídias ativas agora e quantas o plano do parceiro permite. */
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
