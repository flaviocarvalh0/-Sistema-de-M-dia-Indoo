/**
 * Contratos das telas (backend: Atlas.Application/Business/Rede/Telas e Indoor/Telas).
 * Enums chegam como NOME (JsonStringEnumConverter).
 */
export type SituacaoTela = 'AguardandoPareamento' | 'Online' | 'Offline';

/**
 * A tela como a operadora (Rede → Telas) e o parceiro (Onde meus anúncios passam)
 * a veem. Para o parceiro, `codigoPareamento` vem sempre vazio.
 */
export interface TelaDto {
  id: number;
  parceiroId: number;
  parceiroNome: string;
  nome: string;
  localizacao?: string | null;
  /** MAC, número de série ou etiqueta anotados na instalação (informativo). */
  identificacaoAparelho?: string | null;
  playlistId?: number | null;
  playlistNome?: string | null;
  ativo: boolean;
  situacao: SituacaoTela;
  pareadaEm?: string | null;
  ultimaSincronizacao?: string | null;
  versaoApp?: string | null;
  /** Código ainda válido para parear (some ao parear ou ao expirar). Só na visão da operadora. */
  codigoPareamento?: string | null;
  codigoPareamentoExpiraEm?: string | null;
  /** A TV mostra o QR code do diretório de anunciantes (ADR-018). */
  diretorioAtivo: boolean;
  /** Código do endereço público. Só na visão da operadora. */
  codigoDiretorio?: string | null;
  /** Endereço público do diretório; nulo sem código ou sem `Indoor:UrlPublica` na API. */
  urlDiretorio?: string | null;
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

/** Endereço novo do diretório ("Trocar endereço"). */
export interface CodigoDiretorio {
  codigo: string;
  url?: string | null;
  version: number;
}

export type LinhaTela = TelaDto & { situacaoRotulo: string; playlist: string; vistaPorUltimo: string; diretorio: string };
