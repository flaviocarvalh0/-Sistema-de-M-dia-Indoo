import { TipoMidia } from '../../midias/models/midia.model';

/** Contratos das playlists (backend: Atlas.Application/Business/Indoor/Playlists). */
export interface PlaylistDto {
  id: number;
  nome: string;
  descricao?: string | null;
  ativo: boolean;
  quantidadeItens: number;
  quantidadeTelas: number;
  duracaoTotalSegundos: number;
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
  titulo: string;
  tipo: TipoMidia;
  duracaoSegundos: number;
  /** Bits dos dias (domingo = 1 … sábado = 64; 127 = todos). */
  diasSemana: number;
  midiaAtiva: boolean;
  inicioExibicao?: string | null;
  fimExibicao?: string | null;
}

/** Item como o PUT `{id}/itens` recebe: sem `id` é item novo. */
export interface ItemGrade {
  id?: number | null;
  midiaId: number;
  diasSemana: number;
}

export type LinhaPlaylist = PlaylistDto & { duracao: string };
