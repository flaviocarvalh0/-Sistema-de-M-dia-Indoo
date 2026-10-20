import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ArmazenamentoRedeService, UsoArmazenamentoRede } from '../services/armazenamento-rede.service';
import { formatarBytes } from '../../../shared/models/indoor.util';

/** Os 10 GB do plano gratuito do Cloudflare R2. */
const LIMITE_GRATUITO = 10 * 1024 * 1024 * 1024;

/**
 * Barra do armazenamento da rede inteira (ADR-025): quanto já está gravado,
 * quanto está sendo enviado e o teto (no máximo 10 GB, o gratuito do R2).
 * Acima do teto, a API recusa novos envios.
 */
@Component({
  selector: 'app-uso-armazenamento-rede',
  standalone: true,
  imports: [DatePipe],
  template: `
    @if (uso(); as u) {
      <div class="card mb-3" data-teste="uso-armazenamento-rede">
        <div class="card-body py-2">
          <div class="d-flex flex-wrap align-items-baseline gap-2 mb-1">
            <strong><i class="bi bi-hdd me-1"></i>Armazenamento da rede</strong>
            <span class="small" [class.text-danger]="u.percentual >= 95" [class.text-warning]="u.percentual >= 70 && u.percentual < 95"
                  data-teste="uso-armazenamento-texto">
              {{ bytes(u.usadoBytes) }} de {{ bytes(u.limiteBytes) }} ({{ u.percentual }}%)
            </span>
            @if (u.reservadoBytes > 0) {
              <span class="small text-muted">· {{ bytes(u.reservadoBytes) }} em envio</span>
            }
            <span class="small text-muted ms-auto">
              @if (u.inventarioEm && u.objetos != null) {
                Bucket medido {{ u.inventarioEm | date:'dd/MM HH:mm' }} · {{ u.objetos }} arquivos
                @if (u.orfaosRemovidos > 0) { · {{ u.orfaosRemovidos }} sem dono apagados ({{ bytes(u.bytesOrfaosRemovidos) }}) }
              } @else if (u.inventarioEm) {
                Soma do banco (este storage não lista o bucket)
              } @else {
                Bucket ainda não medido (o inventário roda de hora em hora)
              }
            </span>
          </div>
          <div class="progress" style="height: 8px" role="progressbar" [attr.aria-valuenow]="u.percentual" aria-valuemin="0" aria-valuemax="100"
               aria-label="Uso do armazenamento da rede">
            <div class="progress-bar" [class.bg-success]="u.percentual < 70" [class.bg-warning]="u.percentual >= 70 && u.percentual < 95"
                 [class.bg-danger]="u.percentual >= 95" [style.width.%]="larguraUsado()"></div>
            <div class="progress-bar bg-secondary opacity-50" [style.width.%]="larguraReservado()"></div>
          </div>
          @if (u.limiteBytes < limiteGratuito) {
            <div class="small text-muted mt-1">Teto configurado abaixo dos 10 GB gratuitos do R2: a folga cobre imprevistos.</div>
          }
          @if (u.percentual >= 95) {
            <div class="small text-danger mt-1">Quase no teto: novos envios que não couberem serão recusados. Exclua mídias paradas ou versões antigas do app.</div>
          }
        </div>
      </div>
    }
  `,
})
export class UsoArmazenamentoRedeComponent implements OnInit {
  private service = inject(ArmazenamentoRedeService);

  readonly limiteGratuito = LIMITE_GRATUITO;
  uso = signal<UsoArmazenamentoRede | null>(null);

  larguraUsado = computed(() => {
    const u = this.uso();
    return u ? Math.min(100, (u.usadoBytes / u.limiteBytes) * 100) : 0;
  });

  larguraReservado = computed(() => {
    const u = this.uso();
    return u ? Math.max(0, Math.min(100 - this.larguraUsado(), (u.reservadoBytes / u.limiteBytes) * 100)) : 0;
  });

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    // Sem permissão ou API fora: o cartão só não aparece.
    this.service.obter().subscribe({ next: u => this.uso.set(u), error: () => this.uso.set(null) });
  }

  bytes(valor: number): string {
    return formatarBytes(valor);
  }
}
