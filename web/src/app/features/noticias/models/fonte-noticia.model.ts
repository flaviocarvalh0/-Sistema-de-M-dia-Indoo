/** Contratos das fontes de notícias (backend: Atlas.Application/Business/Rede/FontesNoticia). */
export interface FonteNoticiaDto {
  id: number;
  nome: string;
  urlFeed: string;
  /** Exibido na TV junto da notícia (exigência da licença). */
  credito: string;
  ativo: boolean;
  ultimaColetaEm?: string | null;
  /** Erro da última leitura do feed; nulo quando deu certo. */
  ultimoErro?: string | null;
  quantidadeNoticias: number;
  noticiaMaisRecenteEm?: string | null;
  created: string;
  createdByName?: string | null;
  lastModified?: string | null;
  lastModifiedByName?: string | null;
  version: number;
}

export interface NoticiaDto {
  id: number;
  titulo: string;
  publicadaEm: string;
  coletadaEm: string;
  /** Imagem guardada (link de 1 h); nula sem imagem. */
  urlImagem?: string | null;
  /** Ainda vai para as TVs (mais nova que 48 h). */
  vigente: boolean;
}

export interface ResultadoColeta {
  novas: number;
  removidas: number;
  erro?: string | null;
}

export type LinhaFonteNoticia = FonteNoticiaDto & { situacaoColeta: string; ultimaColeta: string };
