import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CrudService, Page, PaginatedResponse, QueryParams, comErroLocal } from '@atlas/framework';
import { LinhaTela, SituacaoTela, TelaDto } from '../models/tela.model';
import { paraPage, paramsPaginacao } from '../../../shared/utils/paginacao.util';

/**
 * Telas do parceiro do usuário logado. O hardware é da operadora (Rede →
 * Telas): aqui o parceiro só consulta as TVs dele e escolhe a playlist.
 *
 *  - GET /api/v1/business/parceiro/telas?ativo=&playlistId=
 *  - GET /api/v1/business/parceiro/telas/{id}
 *  - PUT /api/v1/business/parceiro/telas/{id}/playlist   { id, playlistId, version } → nova versão
 */
@Injectable({ providedIn: 'root' })
export class TelaService extends CrudService<TelaDto, number> {
  protected readonly resource = 'api/v1/business/parceiro/telas';

  listar(query: QueryParams, ativo: boolean | null = null): Observable<Page<LinhaTela>> {
    return this.http
      .get<PaginatedResponse<TelaDto>>(this.baseUrl, {
        params: paramsPaginacao(query, { ativo: ativo == null ? null : String(ativo) }),
      })
      .pipe(
        map(resp => paraPage(resp, query)),
        map(page => ({ ...page, content: page.content.map(paraLinhaTela) })),
      );
  }

  /** Sai de modal com `errosInline: true`: erro no banner do modal. */
  definirPlaylist(id: number, playlistId: number | null, version: number): Observable<number> {
    return this.http.put<number>(`${this.baseUrl}/${id}/playlist`, { id, playlistId, version }, { context: comErroLocal() });
  }
}

export const SITUACAO_TELA_ROTULO: Record<SituacaoTela, string> = {
  AguardandoPareamento: 'Aguardando pareamento',
  Online: 'Online',
  Offline: 'Offline',
};

/** "agora", "há 5 min", "há 3 h", "há 2 dias" ou "nunca". */
export function tempoDesde(iso: string | null | undefined, agora = Date.now()): string {
  if (!iso) return 'nunca';
  const minutos = Math.floor((agora - new Date(iso).getTime()) / 60000);
  if (minutos < 1) return 'agora';
  if (minutos < 60) return `há ${minutos} min`;
  const horas = Math.floor(minutos / 60);
  if (horas < 24) return `há ${horas} h`;
  const dias = Math.floor(horas / 24);
  return `há ${dias} ${dias === 1 ? 'dia' : 'dias'}`;
}

/** Colunas calculadas da grid, iguais na visão da operadora e do parceiro. */
export function paraLinhaTela(t: TelaDto): LinhaTela {
  return {
    ...t,
    situacaoRotulo: SITUACAO_TELA_ROTULO[t.situacao] ?? t.situacao,
    playlist: t.playlistNome ?? '— sem playlist —',
    // Sem TV pareada, a última sincronização é da TV anterior (desvinculada): não vale mostrar.
    vistaPorUltimo: t.situacao === 'AguardandoPareamento' ? '—' : tempoDesde(t.ultimaSincronizacao),
  };
}
