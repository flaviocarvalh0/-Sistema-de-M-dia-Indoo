/**
 * Parceiros: os locais onde a operadora instala as TVs (backend:
 * Atlas.Application/Business/Rede/Parceiros). Não acessam o sistema; se o
 * dono também anuncia, ele é cadastrado como cliente e ligado aqui.
 */
export interface ParceiroDto {
  id: number;
  nome: string;
  nomeResponsavel?: string | null;
  telefone?: string | null;
  email?: string | null;
  documento?: string | null;
  endereco?: string | null;
  cidade?: string | null;
  fusoHorario: string;
  ativo: boolean;
  observacao?: string | null;
  /** Cadastro de cliente do dono do local, quando ele também anuncia. */
  clienteId?: number | null;
  clienteNome?: string | null;
  quantidadeTelas: number;
  created: string;
  createdByName?: string | null;
  lastModified?: string | null;
  lastModifiedByName?: string | null;
  version: number;
}

export type LinhaParceiro = ParceiroDto & { anuncia: string };
