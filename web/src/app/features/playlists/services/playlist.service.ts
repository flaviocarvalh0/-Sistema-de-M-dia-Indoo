import { Injectable } from '@angular/core';
import { Observable, forkJoin, map, of, switchMap } from 'rxjs';
import { CrudService, Page, PaginatedResponse, QueryParams, comErroLocal } from '@atlas/framework';
import { ItemGrade, LinhaPlaylist, PlaylistDto } from '../models/playlist.model';
import { paraPage, paramsPaginacao } from '../../../shared/utils/paginacao.util';
import { formatarDuracao } from '../../../shared/models/indoor.util';

/**
 * Playlists da rede, montadas pela operadora com as mídias dos clientes
 * (reutilizáveis: uma playlist toca em N telas).
 *
 *  - GET  /api/v1/business/rede/playlists?ativo=
 *  - GET  /api/v1/business/rede/playlists/{id}          (traz os itens)
 *  - POST /api/v1/business/rede/playlists               → id
 *  - PUT  /api/v1/business/rede/playlists/{id}          → nova versão
 *  - PUT  /api/v1/business/rede/playlists/{id}/itens    → nova versão (grade inteira, na ordem)
 *  - POST /api/v1/business/rede/playlists/{id}/ativar|inativar?version=
 *  - DELETE /api/v1/business/rede/playlists/{id}
 */
@Injectable({ providedIn: 'root' })
export class PlaylistService extends CrudService<PlaylistDto, number> {
  protected readonly resource = 'api/v1/business/rede/playlists';

  listar(query: QueryParams, ativo: boolean | null = null): Observable<Page<LinhaPlaylist>> {
    return this.http
      .get<PaginatedResponse<PlaylistDto>>(this.baseUrl, {
        params: paramsPaginacao(query, { ativo: ativo == null ? null : String(ativo) }),
      })
      .pipe(
        map(resp => paraPage(resp, query)),
        map(page => ({
          ...page,
          content: page.content.map(p => ({
            ...p,
            duracao: formatarDuracao(p.duracaoTotalSegundos),
            noticias: p.noticiaACadaAnuncios > 0 ? `1 a cada ${p.noticiaACadaAnuncios}${cartoes(p)}` : '—',
          })),
        })),
      );
  }

  /** Todas as playlists, para o select da tela. */
  listarTodas(): Observable<PlaylistDto[]> {
    return this.listar({ pageIndex: 0, pageSize: 1000 }).pipe(map(p => p.content));
  }

  /** Sai de modal com `errosInline: true`: erro no banner do modal. */
  salvar(id: number | null, valor: Partial<PlaylistDto>, version?: number): Observable<unknown> {
    const corpo = corpoPlaylist(valor);
    return id
      ? this.http.put(`${this.baseUrl}/${id}`, { id, ...corpo, version }, { context: comErroLocal() })
      : this.http.post(this.baseUrl, corpo, { context: comErroLocal() });
  }

  /**
   * Cria uma playlist nova com a grade de outra (mesmas mídias, ordem e dias).
   * Mídias inativas ficam de fora: item novo só entra com mídia ativa. A
   * composição (notícias, embaralhar, relógio, clima) vem da original.
   */
  duplicar(origemId: number, valor: Partial<PlaylistDto>): Observable<number> {
    return this.getById(origemId).pipe(
      switchMap(origem => this.http
        .post<number>(this.baseUrl, corpoPlaylist({ ...origem, nome: valor.nome, descricao: valor.descricao }), { context: comErroLocal() })
        .pipe(map(novaId => ({ origem, novaId })))),
      switchMap(({ origem, novaId }) => forkJoin([of(origem), this.getById(novaId)])),
      switchMap(([origem, nova]) => {
        const itens = (origem.itens ?? [])
          .filter(i => i.midiaAtiva)
          .map(i => ({ midiaId: i.midiaId, diasSemana: i.diasSemana, posicaoFixa: i.posicaoFixa }));
        return itens.length
          ? this.http.put<number>(`${this.baseUrl}/${nova.id}/itens`, { id: nova.id, version: nova.version, itens }, { context: comErroLocal() })
            .pipe(map(() => nova.id))
          : of(nova.id);
      }),
    );
  }

  definirItens(id: number, itens: ItemGrade[], version: number): Observable<number> {
    return this.http.put<number>(`${this.baseUrl}/${id}/itens`, { id, version, itens }, { context: comErroLocal() });
  }

  definirAtivo(id: number, ativo: boolean, version: number): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/${id}/${ativo ? 'ativar' : 'inativar'}`, null, { params: { version } });
  }

  excluir(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** Corpo do POST/PUT: nome, descrição e a composição da fila (com os padrões da API). */
function corpoPlaylist(valor: Partial<PlaylistDto>) {
  return {
    nome: valor.nome,
    descricao: valor.descricao || null,
    noticiaACadaAnuncios: Number(valor.noticiaACadaAnuncios ?? 0) || 0,
    duracaoNoticiaSegundos: Number(valor.duracaoNoticiaSegundos ?? 10) || 10,
    embaralharAnuncios: !!valor.embaralharAnuncios,
    duracaoMaximaVoltaSegundos: Number(valor.duracaoMaximaVoltaSegundos ?? 0) || 0,
    exibirRelogio: valor.exibirRelogio ?? true,
    exibirClima: valor.exibirClima ?? true,
    cartaoTempo: !!valor.cartaoTempo,
    cartaoCotacoes: !!valor.cartaoCotacoes,
  };
}

/** " (+ tempo, cotações)" quando a playlist usa os cartões. */
function cartoes(p: PlaylistDto): string {
  const nomes = [p.cartaoTempo ? 'tempo' : null, p.cartaoCotacoes ? 'cotações' : null].filter(Boolean);
  return nomes.length ? ` (+ ${nomes.join(', ')})` : '';
}
