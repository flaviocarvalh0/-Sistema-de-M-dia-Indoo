import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CrudService, PaginatedResponse, comErroLocal, itensDeResposta } from '@atlas/framework';
import { MidiaDto } from '../../midias/models/midia.model';
import { paramsPaginacao } from '../../../shared/utils/paginacao.util';

/**
 * Mídias de todos os parceiros, somente leitura, para a operadora montar as
 * grades (o criativo é do anunciante).
 *
 *  - GET /api/v1/business/rede/midias?parceiroId=&ativo=&tipo=&searchTerm=
 *  - GET /api/v1/business/rede/midias/{id}   (traz urlVisualizacao e urlVisualizacaoPendente)
 *  - POST /api/v1/business/rede/midias/{id}/aprovar-troca?version=
 *  - POST /api/v1/business/rede/midias/{id}/recusar-troca  { id, motivo, version }
 */
@Injectable({ providedIn: 'root' })
export class MidiaRedeService extends CrudService<MidiaDto, number> {
  protected readonly resource = 'api/v1/business/rede/midias';

  /** Todas as mídias ativas da rede: só elas podem entrar na grade. */
  listarAtivas(): Observable<MidiaDto[]> {
    return this.http
      .get<PaginatedResponse<MidiaDto>>(this.baseUrl, {
        params: paramsPaginacao({ pageIndex: 0, pageSize: 1000 }, { ativo: 'true' }),
      })
      .pipe(map(resp => itensDeResposta(resp)));
  }

  /** Trocas de arquivo aguardando aprovação, mais antigas primeiro. */
  listarTrocasPendentes(): Observable<MidiaDto[]> {
    return this.http
      .get<PaginatedResponse<MidiaDto>>(this.baseUrl, {
        params: paramsPaginacao({ pageIndex: 0, pageSize: 200 }, { trocaPendente: 'true' }),
      })
      .pipe(map(resp => itensDeResposta(resp)));
  }

  aprovarTroca(id: number, version: number): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/${id}/aprovar-troca`, null, { params: { version } });
  }

  /** Sai de modal com `errosInline: true`: erro no banner do modal. */
  recusarTroca(id: number, motivo: string, version: number): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/${id}/recusar-troca`, { id, motivo, version }, { context: comErroLocal() });
  }
}
