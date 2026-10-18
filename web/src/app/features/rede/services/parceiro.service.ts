import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CrudService, Page, PaginatedResponse, QueryParams, comErroLocal } from '@atlas/framework';
import { LinhaParceiro, ParceiroDto } from '../models/parceiro.model';
import { paraPage, paramsPaginacao } from '../../../shared/utils/paginacao.util';

/**
 * Parceiros da rede (os locais das TVs; só a operadora).
 *
 *  - GET    /api/v1/business/rede/parceiros?ativo=
 *  - GET    /api/v1/business/rede/parceiros/{id}
 *  - POST   /api/v1/business/rede/parceiros               → id
 *  - PUT    /api/v1/business/rede/parceiros/{id}          → nova versão
 *  - POST   /api/v1/business/rede/parceiros/{id}/ativar|inativar?version=
 *  - DELETE /api/v1/business/rede/parceiros/{id}          (só sem telas)
 */
@Injectable({ providedIn: 'root' })
export class ParceiroService extends CrudService<ParceiroDto, number> {
  protected readonly resource = 'api/v1/business/rede/parceiros';

  listar(query: QueryParams, ativo: boolean | null): Observable<Page<LinhaParceiro>> {
    return this.http
      .get<PaginatedResponse<ParceiroDto>>(this.baseUrl, {
        params: paramsPaginacao(query, { ativo: ativo == null ? null : String(ativo) }),
      })
      .pipe(
        map(resp => paraPage(resp, query)),
        map(page => ({ ...page, content: page.content.map(p => ({ ...p, anuncia: p.clienteNome ?? '—' })) })),
      );
  }

  /** Sai de modal com `errosInline: true`: erro no banner do modal. */
  salvar(id: number | null, valor: Partial<ParceiroDto>, version?: number): Observable<unknown> {
    const corpo = {
      nome: valor.nome,
      nomeResponsavel: valor.nomeResponsavel || null,
      telefone: valor.telefone || null,
      email: valor.email || null,
      documento: valor.documento || null,
      endereco: valor.endereco || null,
      cidade: valor.cidade || null,
      fusoHorario: valor.fusoHorario,
      observacao: valor.observacao || null,
      clienteId: valor.clienteId ? Number(valor.clienteId) : null,
    };

    return id
      ? this.http.put(`${this.baseUrl}/${id}`, { id, ...corpo, version }, { context: comErroLocal() })
      : this.http.post(this.baseUrl, corpo, { context: comErroLocal() });
  }

  definirAtivo(id: number, ativo: boolean, version: number): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/${id}/${ativo ? 'ativar' : 'inativar'}`, null, { params: { version } });
  }

  excluir(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
