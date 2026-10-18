import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormModalService, ToastService } from '@atlas/framework';
import { MidiaRedeService } from '../services/midia-rede.service';
import { MidiaDto } from '../../midias/models/midia.model';
import { formatarBytes } from '../../../shared/models/indoor.util';

/**
 * Trocas de arquivo que os anunciantes enviaram para mídias que já estão no
 * ar (ADR-020). A operadora compara o arquivo atual com o novo e aprova ou
 * recusa com motivo; até lá as TVs seguem com o atual. Some quando não há nada.
 */
@Component({
  selector: 'app-trocas-pendentes',
  standalone: true,
  imports: [DatePipe],
  template: `
    @if (trocas().length > 0) {
      <div class="card border-warning mb-3" data-teste="trocas-pendentes">
        <div class="card-header d-flex align-items-center gap-2">
          <i class="bi bi-hourglass-split text-warning"></i>
          <strong class="me-auto">Trocas de arquivo para aprovar ({{ trocas().length }})</strong>
          <span class="small text-muted">As TVs seguem com o arquivo atual até você aprovar.</span>
        </div>
        <ul class="list-group list-group-flush">
          @for (m of trocas(); track m.id) {
            <li class="list-group-item" [attr.data-midia]="m.id">
              <div class="d-flex flex-wrap align-items-center gap-2">
                <div class="me-auto">
                  <strong>{{ m.titulo }}</strong> <span class="text-muted">· {{ m.clienteNome }}</span>
                  <div class="small text-muted">
                    Enviado {{ m.pendenteEnviadoEm | date:'dd/MM HH:mm' }} · {{ tamanho(m.pendenteTamanhoBytes) }}
                    · em {{ m.quantidadePlaylists }} playlist(s)
                  </div>
                </div>
                <button type="button" class="btn btn-sm btn-outline-secondary" (click)="comparar(m)">
                  <i class="bi bi-eye me-1"></i>Comparar
                </button>
                <button type="button" class="btn btn-sm btn-success" (click)="aprovar(m)">
                  <i class="bi bi-check-lg me-1"></i>Aprovar
                </button>
                <button type="button" class="btn btn-sm btn-outline-danger" (click)="recusar(m)">
                  <i class="bi bi-x-lg me-1"></i>Recusar
                </button>
              </div>
              @if (comparando()?.id === m.id) {
                <div class="d-flex flex-wrap gap-3 mt-3" data-teste="comparacao">
                  @for (lado of lados(); track lado.rotulo) {
                    <figure class="mb-0" style="width: 320px; max-width: 100%">
                      <figcaption class="small text-muted mb-1">{{ lado.rotulo }}</figcaption>
                      @if (lado.video) {
                        <video [src]="lado.url" controls muted class="w-100 rounded border bg-dark" style="aspect-ratio: 16/9"></video>
                      } @else {
                        <img [src]="lado.url" [alt]="lado.rotulo" class="w-100 rounded border bg-dark" style="aspect-ratio: 16/9; object-fit: contain" />
                      }
                    </figure>
                  }
                </div>
              }
            </li>
          }
        </ul>
      </div>
    }
  `,
})
export class TrocasPendentesComponent implements OnInit {
  private service = inject(MidiaRedeService);
  private toast = inject(ToastService);
  private formModal = inject(FormModalService);

  trocas = signal<MidiaDto[]>([]);
  comparando = signal<MidiaDto | null>(null);
  lados = signal<{ rotulo: string; url: string; video: boolean }[]>([]);

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.service.listarTrocasPendentes().subscribe(lista => {
      this.trocas.set(lista);
      if (!lista.some(m => m.id === this.comparando()?.id)) this.comparando.set(null);
    });
  }

  tamanho(bytes?: number | null): string {
    return bytes ? formatarBytes(bytes) : '';
  }

  comparar(m: MidiaDto): void {
    if (this.comparando()?.id === m.id) {
      this.comparando.set(null);
      return;
    }
    this.service.getById(m.id).subscribe(detalhe => {
      this.lados.set([
        { rotulo: 'No ar agora', url: detalhe.urlVisualizacao ?? '', video: detalhe.tipo === 'Video' },
        { rotulo: 'Arquivo novo', url: detalhe.urlVisualizacaoPendente ?? '', video: detalhe.pendenteTipo === 'Video' },
      ]);
      this.comparando.set(m);
    });
  }

  aprovar(m: MidiaDto): void {
    this.service.aprovarTroca(m.id, m.version).subscribe(() => {
      this.toast.success(`Troca aprovada: as TVs baixam o arquivo novo de "${m.titulo}" na próxima sincronização.`);
      this.carregar();
    });
  }

  recusar(m: MidiaDto): void {
    this.formModal.open({
      titulo: `Recusar troca de "${m.titulo}"`,
      ocultarAcoesCabecalho: true,
      campos: [{
        type: 'textarea', name: 'motivo', label: 'Motivo (o anunciante recebe por e-mail)', required: true, col: 'col-12',
        placeholder: 'Ex.: a imagem está cortada; envie em 1920×1080.',
      }],
      largura: '560px',
      errosInline: true,
      permitirContinuar: false,
      mostrarBotaoSalvarECopiar: false,
      registro: m,
      persistir: (valor: { motivo: string }, atual: MidiaDto) => this.service.recusarTroca(atual.id, valor.motivo, atual.version),
      msgSucesso: 'Troca recusada. O arquivo atual segue no ar.',
    }).subscribe(res => { if (res?.recarregar || res?.acao === 'fechar') this.carregar(); });
  }
}
