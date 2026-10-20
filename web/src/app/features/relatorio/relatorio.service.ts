import { Injectable } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CrudService } from '@atlas/framework';
import { RelatorioVeiculacaoDto } from './relatorio.model';

/**
 * Relatório de veiculação (ADR-023).
 *
 *  - GET /api/v1/business/cliente/relatorio?de=&ate=              (o próprio cliente)
 *  - GET /api/v1/business/rede/clientes/{id}/relatorio?de=&ate=   (operadora)
 */
@Injectable({ providedIn: 'root' })
export class RelatorioService extends CrudService<RelatorioVeiculacaoDto, number> {
  protected readonly resource = 'api/v1/business';

  obter(clienteId: number | null, de: string | null, ate: string | null): Observable<RelatorioVeiculacaoDto> {
    let params = new HttpParams();
    if (de) params = params.set('de', de);
    if (ate) params = params.set('ate', ate);
    const caminho = clienteId == null ? 'cliente/relatorio' : `rede/clientes/${clienteId}/relatorio`;
    return this.http.get<RelatorioVeiculacaoDto>(`${this.baseUrl}/${caminho}`, { params });
  }
}
