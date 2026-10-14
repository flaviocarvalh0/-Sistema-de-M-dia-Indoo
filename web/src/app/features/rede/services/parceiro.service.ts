import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CrudService, Page, PaginatedResponse, QueryParams, comErroLocal } from '@atlas/framework';
import { LinhaParceiro, ParceiroDto, SituacaoParceiro } from '../models/parceiro.model';
import { paraPage, paramsPaginacao } from '../../../shared/utils/paginacao.util';
import { formatarBytes } from '../../../shared/models/indoor.util';

/**
 * Parceiros da rede (operadora).
 *
 *  - GET  /api/v1/business/rede/parceiros?situacao=
 *  - GET  /api/v1/business/rede/parceiros/{id}
 *  - POST /api/v1/business/rede/parceiros                 → id (cria o usuário do responsável)
 *  - PUT  /api/v1/business/rede/parceiros/{id}            → nova versão
 *  - POST /api/v1/business/rede/parceiros/{id}/bloquear | desbloquear
 */
@Injectable({ providedIn: 'root' })
export class ParceiroService extends CrudService<ParceiroDto, number> {
  protected readonly resource = 'api/v1/business/rede/parceiros';

  listar(query: QueryParams, situacao: SituacaoParceiro | null): Observable<Page<LinhaParceiro>> {
    return this.http
      .get<PaginatedResponse<ParceiroDto>>(this.baseUrl, { params: paramsPaginacao(query, { situacao }) })
      .pipe(
        map(resp => paraPage(resp, query)),
        map(page => ({
          ...page,
          content: page.content.map(p => ({
            ...p,
            usoArmazenamento: `${formatarBytes(p.armazenamentoUsadoBytes)} de ${p.cotaArmazenamentoMb} MB`,
            usoMidiasAtivas: `${p.midiasAtivas} de ${p.limiteMidiasAtivas}`,
          })),
        })),
      );
  }

  /** Sai de modal com `errosInline: true`: erro no banner do modal. */
  salvar(id: number | null, valor: Partial<ParceiroDto>, version?: number): Observable<unknown> {
    const corpo = {
      nome: valor.nome,
      nomeResponsavel: valor.nomeResponsavel,
      telefone: valor.telefone || null,
      documento: valor.documento || null,
      cidade: valor.cidade || null,
      fusoHorario: valor.fusoHorario,
      cotaArmazenamentoMb: Number(valor.cotaArmazenamentoMb),
      limiteMidiasAtivas: Number(valor.limiteMidiasAtivas),
      observacaoInterna: valor.observacaoInterna || null,
    };

    return id
      ? this.http.put(`${this.baseUrl}/${id}`, { id, ...corpo, version }, { context: comErroLocal() })
      : this.http.post(this.baseUrl, { ...corpo, email: valor.email }, { context: comErroLocal() });
  }

  bloquear(id: number, motivo: string, version: number): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/${id}/bloquear`, { id, motivo, version }, { context: comErroLocal() });
  }

  desbloquear(id: number, version: number): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/${id}/desbloquear`, { id, version });
  }
}
