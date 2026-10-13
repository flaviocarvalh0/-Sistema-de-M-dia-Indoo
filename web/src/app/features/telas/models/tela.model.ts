/**
 * Contratos das telas (backend: Atlas.Application/Business/Indoor/Telas).
 * Enums chegam como NOME (JsonStringEnumConverter).
 */
export type SituacaoTela = 'AguardandoPareamento' | 'Online' | 'Offline';

export interface TelaDto {
  id: number;
  nome: string;
  localizacao?: string | null;
  playlistId?: number | null;
  playlistNome?: string | null;
  ativo: boolean;
  situacao: SituacaoTela;
  pareadaEm?: string | null;
  ultimaSincronizacao?: string | null;
  versaoApp?: string | null;
  /** Código ainda válido para parear (some ao parear ou ao expirar). */
  codigoPareamento?: string | null;
  codigoPareamentoExpiraEm?: string | null;
  created: string;
  createdByName?: string | null;
  lastModified?: string | null;
  lastModifiedByName?: string | null;
  version: number;
}

export interface CodigoPareamento {
  codigo: string;
  expiraEm: string;
  version: number;
}

export type LinhaTela = TelaDto & { situacaoRotulo: string; playlist: string; vistaPorUltimo: string };
