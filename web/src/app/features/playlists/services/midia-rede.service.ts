import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CrudService, PaginatedResponse, itensDeResposta } from '@atlas/framework';
import { MidiaDto } from '../../midias/models/midia.model';
import { paramsPaginacao } from '../../../shared/utils/paginacao.util';

/**
 * Mídias de todos os parceiros, somente leitura, para a operadora montar as
 * grades (o criativo é do anunciante).
 *
 *  - GET /api/v1/business/rede/midias?parceiroId=&ativo=&tipo=&searchTerm=
 *  - GET /api/v1/business/rede/midias/{id}   (traz urlVisualizacao)
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
}
