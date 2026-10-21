import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CrudService, Page, PaginatedResponse, QueryParams, comErroLocal } from '@atlas/framework';
import { CodigoDiretorio, CodigoPareamento, LinhaTela, TelaDto, UsoDiretorio } from '../../telas/models/tela.model';
import { paraLinhaTela } from '../../telas/services/tela.service';
import { paraPage, paramsPaginacao } from '../../../shared/utils/paginacao.util';

/**
 * Telas da rede (operadora, dona do hardware).
 *
 *  - GET  /api/v1/business/rede/telas?parceiroId=&ativo=
 *  - GET  /api/v1/business/rede/telas/{id}
 *  - POST /api/v1/business/rede/telas                         → id
 *  - PUT  /api/v1/business/rede/telas/{id}                    → nova versão (host e playlist; a TV continua pareada)
 *  - POST /api/v1/business/rede/telas/{id}/codigo-pareamento?version= → { codigo, expiraEm, version }
 *  - POST /api/v1/business/rede/telas/{id}/codigo-diretorio?version=  → { codigo, url, version } (endereço novo do diretório)
 *  - GET  /api/v1/business/rede/telas/{id}/diretorio/uso?dias=30       → acessos e toques na página do diretório
 *  - POST /api/v1/business/rede/telas/{id}/desvincular|ativar|inativar?version=
 *  - DELETE /api/v1/business/rede/telas/{id}
 */
@Injectable({ providedIn: 'root' })
export class TelaRedeService extends CrudService<TelaDto, number> {
  protected readonly resource = 'api/v1/business/rede/telas';

  listar(query: QueryParams, filtros: { parceiroId?: number | null; ativo?: boolean | null } = {}): Observable<Page<LinhaTela>> {
    return this.http
      .get<PaginatedResponse<TelaDto>>(this.baseUrl, {
        params: paramsPaginacao(query, {
          parceiroId: filtros.parceiroId == null ? null : String(filtros.parceiroId),
          ativo: filtros.ativo == null ? null : String(filtros.ativo),
        }),
      })
      .pipe(
        map(resp => paraPage(resp, query)),
        map(page => ({ ...page, content: page.content.map(paraLinhaTela) })),
      );
  }

  /** Sai de modal com `errosInline: true`: erro no banner do modal. */
  salvar(id: number | null, valor: Partial<TelaDto>, version?: number): Observable<unknown> {
    const corpo = {
      parceiroId: valor.parceiroId ? Number(valor.parceiroId) : 0,
      nome: valor.nome,
      localizacao: valor.localizacao || null,
      identificacaoAparelho: valor.identificacaoAparelho || null,
      playlistId: valor.playlistId ? Number(valor.playlistId) : null,
      diretorioAtivo: !!valor.diretorioAtivo,
      tvDeTeste: !!valor.tvDeTeste,
    };
    return id
      ? this.http.put(`${this.baseUrl}/${id}`, { id, ...corpo, version }, { context: comErroLocal() })
      : this.http.post(this.baseUrl, corpo, { context: comErroLocal() });
  }

  gerarCodigo(id: number, version: number): Observable<CodigoPareamento> {
    return this.http.post<CodigoPareamento>(`${this.baseUrl}/${id}/codigo-pareamento`, null, { params: { version } });
  }

  usoDiretorio(id: number, dias = 30): Observable<UsoDiretorio> {
    return this.http.get<UsoDiretorio>(`${this.baseUrl}/${id}/diretorio/uso`, { params: { dias } });
  }

  trocarCodigoDiretorio(id: number, version: number): Observable<CodigoDiretorio> {
    return this.http.post<CodigoDiretorio>(`${this.baseUrl}/${id}/codigo-diretorio`, null, { params: { version } });
  }

  desvincular(id: number, version: number): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/${id}/desvincular`, null, { params: { version } });
  }

  definirAtivo(id: number, ativo: boolean, version: number): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/${id}/${ativo ? 'ativar' : 'inativar'}`, null, { params: { version } });
  }

  excluir(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
