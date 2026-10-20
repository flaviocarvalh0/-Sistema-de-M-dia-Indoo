import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CrudService } from '@atlas/framework';

/** Uso do storage da rede contra o teto (ADR-025). */
export interface UsoArmazenamentoRede {
  /** O que o banco conhece: mídias, trocas pendentes, APKs e imagens das notícias. */
  arquivosBytes: number;
  /** Envios em andamento (URL emitida, arquivo ainda não confirmado). */
  reservadoBytes: number;
  /** Última medição do bucket (nula sem inventário recente). */
  bucketBytes: number | null;
  limiteBytes: number;
  inventarioEm: string | null;
  /** Nulo: o storage não lista o bucket (Supabase) e vale a soma do banco. */
  objetos: number | null;
  orfaosRemovidos: number;
  bytesOrfaosRemovidos: number;
  /** O maior entre o banco e o bucket. */
  usadoBytes: number;
  percentual: number;
  livreBytes: number;
}

/**
 *  - GET /api/v1/business/rede/armazenamento
 */
@Injectable({ providedIn: 'root' })
export class ArmazenamentoRedeService extends CrudService<UsoArmazenamentoRede, number> {
  protected readonly resource = 'api/v1/business/rede/armazenamento';

  obter(): Observable<UsoArmazenamentoRede> {
    return this.http.get<UsoArmazenamentoRede>(this.baseUrl);
  }
}
