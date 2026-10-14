import { TipoMidia } from '../../midias/models/midia.model';

/** Contratos das playlists (backend: Atlas.Application/Business/Rede/Playlists). */
export interface PlaylistDto {
  id: number;
  nome: string;
  descricao?: string | null;
  ativo: boolean;
  quantidadeItens: number;
  quantidadeTelas: number;
  duracaoTotalSegundos: number;
  /** Composição da fila que a TV monta: uma notícia a cada N anúncios (0 = sem). */
  noticiaACadaAnuncios: number;
  duracaoNoticiaSegundos: number;
  embaralharAnuncios: boolean;
  exibirRelogio: boolean;
  exibirClima: boolean;
  /** Cartões no rodízio dos informativos, junto com as notícias. */
  cartaoTempo: boolean;
  cartaoCotacoes: boolean;
  /** Só no detalhe (`GET /{id}`), na ordem do loop. */
  itens?: ItemPlaylistDto[] | null;
  created: string;
  createdByName?: string | null;
  lastModified?: string | null;
  lastModifiedByName?: string | null;
  version: number;
}

export interface ItemPlaylistDto {
  id: number;
  ordem: number;
  midiaId: number;
  /** Parceiro anunciante dono da mídia. */
  parceiroId: number;
  parceiroNome: string;
  titulo: string;
  tipo: TipoMidia;
  duracaoSegundos: number;
  /** Bits dos dias (domingo = 1 … sábado = 64; 127 = todos). */
  diasSemana: number;
  /** Fica no lugar quando a playlist embaralha os anúncios. */
  posicaoFixa: boolean;
  midiaAtiva: boolean;
  inicioExibicao?: string | null;
  fimExibicao?: string | null;
}

/** Item como o PUT `{id}/itens` recebe: sem `id` é item novo. */
export interface ItemGrade {
  id?: number | null;
  midiaId: number;
  diasSemana: number;
  posicaoFixa: boolean;
}

export type LinhaPlaylist = PlaylistDto & { duracao: string; noticias: string };
