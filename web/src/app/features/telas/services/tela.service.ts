import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CrudService, Page, PaginatedResponse, QueryParams } from '@atlas/framework';
import { LinhaTela, SituacaoTela, TelaDto } from '../models/tela.model';
import { paraPage, paramsPaginacao } from '../../../shared/utils/paginacao.util';

/**
 * "Onde meus anúncios passam": as TVs ativas cuja playlist tem uma mídia
 * ativa do cliente logado. Somente leitura (telas e grades são da operadora).
 *
 *  - GET /api/v1/business/cliente/telas?searchTerm=
 *  - GET /api/v1/business/cliente/telas/{id}
 */
@Injectable({ providedIn: 'root' })
export class TelaService extends CrudService<TelaDto, number> {
  protected readonly resource = 'api/v1/business/cliente/telas';

  listar(query: QueryParams): Observable<Page<LinhaTela>> {
    return this.http
      .get<PaginatedResponse<TelaDto>>(this.baseUrl, { params: paramsPaginacao(query) })
      .pipe(
        map(resp => paraPage(resp, query)),
        map(page => ({ ...page, content: page.content.map(paraLinhaTela) })),
      );
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

/** Colunas calculadas da grid, iguais na visão da operadora e do cliente. */
export function paraLinhaTela(t: TelaDto): LinhaTela {
  return {
    ...t,
    situacaoRotulo: SITUACAO_TELA_ROTULO[t.situacao] ?? t.situacao,
    playlist: t.playlistNome ?? '— sem playlist —',
    // Sem TV pareada, a última sincronização é da TV anterior (desvinculada): não vale mostrar.
    vistaPorUltimo: t.situacao === 'AguardandoPareamento' ? '—' : tempoDesde(t.ultimaSincronizacao),
    diretorio: t.diretorioAtivo ? 'Ligado' : '—',
    versaoAppRotulo: !t.versaoApp ? '—'
      : `${t.versaoApp}${t.versaoCodigoApp ? ` (${t.versaoCodigoApp})` : ''}${t.tvDeTeste ? ' · teste' : ''}`,
  };
}
