import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CrudService, Page, PaginatedResponse, QueryParams, comErroLocal } from '@atlas/framework';
import { FonteNoticiaDto, LinhaFonteNoticia, NoticiaDto, ResultadoColeta } from '../models/fonte-noticia.model';
import { paraPage, paramsPaginacao } from '../../../shared/utils/paginacao.util';
import { tempoDesde } from '../../telas/services/tela.service';

/**
 * Feeds de notícias da rede (operadora).
 *
 *  - GET    /api/v1/business/rede/fontes-noticia?ativo=
 *  - GET    /api/v1/business/rede/fontes-noticia/{id}
 *  - GET    /api/v1/business/rede/fontes-noticia/{id}/noticias
 *  - POST   /api/v1/business/rede/fontes-noticia                 → id
 *  - PUT    /api/v1/business/rede/fontes-noticia/{id}            → nova versão
 *  - POST   /api/v1/business/rede/fontes-noticia/{id}/coletar    → { novas, removidas, erro }
 *  - DELETE /api/v1/business/rede/fontes-noticia/{id}
 */
@Injectable({ providedIn: 'root' })
export class FonteNoticiaService extends CrudService<FonteNoticiaDto, number> {
  protected readonly resource = 'api/v1/business/rede/fontes-noticia';

  listar(query: QueryParams): Observable<Page<LinhaFonteNoticia>> {
    return this.http
      .get<PaginatedResponse<FonteNoticiaDto>>(this.baseUrl, { params: paramsPaginacao(query) })
      .pipe(
        map(resp => paraPage(resp, query)),
        map(page => ({ ...page, content: page.content.map(paraLinha) })),
      );
  }

  noticias(id: number): Observable<NoticiaDto[]> {
    return this.http.get<NoticiaDto[]>(`${this.baseUrl}/${id}/noticias`);
  }

  /** Sai de modal com `errosInline: true`: erro no banner do modal. */
  salvar(id: number | null, valor: Partial<FonteNoticiaDto>, version?: number): Observable<unknown> {
    const corpo = {
      nome: valor.nome,
      urlFeed: (valor.urlFeed ?? '').trim(),
      credito: valor.credito,
      ativo: valor.ativo ?? true,
    };
    return id
      ? this.http.put(`${this.baseUrl}/${id}`, { id, ...corpo, version }, { context: comErroLocal() })
      : this.http.post(this.baseUrl, corpo, { context: comErroLocal() });
  }

  coletar(id: number): Observable<ResultadoColeta> {
    return this.http.post<ResultadoColeta>(`${this.baseUrl}/${id}/coletar`, null);
  }

  excluir(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}

function paraLinha(f: FonteNoticiaDto): LinhaFonteNoticia {
  return {
    ...f,
    situacaoColeta: !f.ultimaColetaEm ? 'Aguardando' : f.ultimoErro ? 'Com erro' : 'Ok',
    ultimaColeta: f.ultimaColetaEm ? tempoDesde(f.ultimaColetaEm) : '—',
  };
}
