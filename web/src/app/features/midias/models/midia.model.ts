/**
 * Contratos das mídias (backend: Atlas.Application/Business/Indoor/Midias).
 * Enums chegam como NOME (JsonStringEnumConverter).
 */
export type TipoMidia = 'Video' | 'Imagem';

export interface MidiaDto {
  id: number;
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
