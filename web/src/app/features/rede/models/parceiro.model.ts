/**
 * Contratos da Rede (backend: Atlas.Application/Business/Rede/Parceiros).
 * Enums chegam como NOME (JsonStringEnumConverter).
 */
export type SituacaoParceiro = 'Ativo' | 'Bloqueado';

export interface ParceiroDto {
  id: number;
  nome: string;
  nomeResponsavel?: string | null;
  email: string;
  telefone?: string | null;
  documento?: string | null;
  cidade?: string | null;
  fusoHorario: string;
  cotaArmazenamentoMb: number;
  armazenamentoUsadoBytes: number;
  quantidadeTelas: number;
  situacao: SituacaoParceiro;
  motivoBloqueio?: string | null;
  observacaoInterna?: string | null;
  created: string;
  createdByName?: string | null;
  lastModified?: string | null;
  lastModifiedByName?: string | null;
  version: number;
}

/** Linha da grid: o DTO mais as colunas calculadas. */
export type LinhaParceiro = ParceiroDto & { usoArmazenamento: string };
