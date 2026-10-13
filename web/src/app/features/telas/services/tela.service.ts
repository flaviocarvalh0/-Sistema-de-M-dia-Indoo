import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CrudService, Page, PaginatedResponse, QueryParams, comErroLocal } from '@atlas/framework';
import { CodigoPareamento, LinhaTela, SituacaoTela, TelaDto } from '../models/tela.model';
import { paraPage, paramsPaginacao } from '../../../shared/utils/paginacao.util';

/**
 * Telas (TVs) do parceiro.
 *
 *  - GET  /api/v1/business/parceiro/telas?ativo=&playlistId=
 *  - GET  /api/v1/business/parceiro/telas/{id}
 *  - POST /api/v1/business/parceiro/telas                         → id
 *  - PUT  /api/v1/business/parceiro/telas/{id}                    → nova versão
 *  - POST /api/v1/business/parceiro/telas/{id}/codigo-pareamento?version= → { codigo, expiraEm, version }
 *  - POST /api/v1/business/parceiro/telas/{id}/desvincular|ativar|inativar?version=
 *  - DELETE /api/v1/business/parceiro/telas/{id}
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
        map(page => ({ ...page, content: page.content.map(paraLinha) })),
      );
  }

  /** Sai de modal com `errosInline: true`: erro no banner do modal. */
  salvar(id: number | null, valor: Partial<TelaDto>, version?: number): Observable<unknown> {
    const corpo = {
      nome: valor.nome,
      localizacao: valor.localizacao || null,
      playlistId: valor.playlistId ? Number(valor.playlistId) : null,
    };
    return id
      ? this.http.put(`${this.baseUrl}/${id}`, { id, ...corpo, version }, { context: comErroLocal() })
      : this.http.post(this.baseUrl, corpo, { context: comErroLocal() });
  }

  gerarCodigo(id: number, version: number): Observable<CodigoPareamento> {
    return this.http.post<CodigoPareamento>(`${this.baseUrl}/${id}/codigo-pareamento`, null, { params: { version } });
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

function paraLinha(t: TelaDto): LinhaTela {
  return {
    ...t,
    situacaoRotulo: SITUACAO_TELA_ROTULO[t.situacao] ?? t.situacao,
    playlist: t.playlistNome ?? '— sem playlist —',
    vistaPorUltimo: tempoDesde(t.ultimaSincronizacao),
  };
}
