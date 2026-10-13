import { HttpParams } from '@angular/common/http';
import { Page, PaginatedResponse, QueryParams, itensDeResposta } from '@atlas/framework';

/**
 * Parâmetros do contrato Atlas (`pageNumber` base 1, `pageSize`, `searchTerm`
 * e filtros) a partir do `QueryParams` da grid (base 0).
 */
export function paramsPaginacao(query: QueryParams, extras: Record<string, string | null | undefined> = {}): HttpParams {
  let params = new HttpParams()
    .set('pageNumber', String((query.pageIndex ?? 0) + 1))
    .set('pageSize', String(query.pageSize ?? 10));

  if (query.search) params = params.set('searchTerm', query.search);

  for (const [campo, valor] of Object.entries(extras)) {
    if (valor != null && valor !== '') params = params.set(campo, valor);
  }

  return params;
}

/** Envelope `PaginatedList` do backend → `Page<T>` da grid. */
export function paraPage<T>(resp: PaginatedResponse<T>, query: QueryParams): Page<T> {
  const pageIndex = query.pageIndex ?? 0;
  const pageSize = query.pageSize ?? 10;
  const content = itensDeResposta(resp);
  const totalElements = resp?.totalCount ?? content.length;
  const totalPages = resp?.totalPages ?? Math.ceil(totalElements / (pageSize || 1));
  return { content, totalElements, totalPages, pageIndex, pageSize };
}
