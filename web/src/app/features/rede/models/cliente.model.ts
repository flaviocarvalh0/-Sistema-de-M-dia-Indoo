/**
 * Contratos da Rede (backend: Atlas.Application/Business/Rede/Clientes).
 * Enums chegam como NOME (JsonStringEnumConverter).
 */
export type SituacaoCliente = 'Ativo' | 'Bloqueado';

export interface ClienteDto {
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
  /** Quantas mídias o cliente pode ter ativas ao mesmo tempo (plano). */
  limiteMidiasAtivas: number;
  midiasAtivas: number;
  quantidadeTelas: number;
  situacao: SituacaoCliente;
  /** Aceitou aparecer no diretório das TVs (decisão do próprio cliente; aqui é só leitura). */
  exibirNoDiretorioPublico: boolean;
  diretorioAceitoEm?: string | null;
  motivoBloqueio?: string | null;
  observacaoInterna?: string | null;
  created: string;
  createdByName?: string | null;
  lastModified?: string | null;
  lastModifiedByName?: string | null;
  version: number;
}

/** Linha da grid: o DTO mais as colunas calculadas. */
export type LinhaCliente = ClienteDto & { usoArmazenamento: string; usoMidiasAtivas: string; diretorio: string };
