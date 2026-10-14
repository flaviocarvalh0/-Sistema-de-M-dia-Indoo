import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpEventType } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, filter, last, switchMap, tap } from 'rxjs';
import { ToastService, extrairMensagemErro } from '@atlas/framework';
import { MidiaService } from '../services/midia.service';
import { MidiaDto, UsoArmazenamento } from '../models/midia.model';
import { DURACAO_MAXIMA_SEGUNDOS } from '../configs/midia.config';
import { formatarBytes } from '../../../shared/models/indoor.util';

/** Tipos aceitos pela API (TiposArquivoMidia). */
const TIPOS_ACEITOS: Record<string, 'Video' | 'Imagem'> = {
  'video/mp4': 'Video',
  'image/jpeg': 'Imagem',
  'image/png': 'Imagem',
};

const DURACAO_PADRAO_IMAGEM = 15;

type Etapa = 'escolha' | 'hash' | 'envio' | 'registro';

/**
 * Envio de mídia em três passos, sem o arquivo passar pela API:
 *  1. POST envio → URL assinada do storage (a API confere tipo, tamanho e cota);
 *  2. PUT do arquivo direto no storage, com progresso;
 *  3. POST da mídia com o SHA-256 calculado aqui — a API confere o arquivo
 *     enviado e as TVs usam o hash para validar o download.
 *
 * Com `?substituir={id}`, troca o arquivo de uma mídia existente
 * (`PUT {id}/arquivo`): ela continua nas mesmas grades e ativa, e as TVs
 * baixam o arquivo novo pelo hash novo. É o caminho para trocar o anúncio
 * sem esbarrar no limite de mídias ativas do plano.
 */
@Component({
  selector: 'app-envio-midia-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="d-flex align-items-center mb-3">
      <a routerLink="/indoor/midia" class="btn btn-sm btn-outline-secondary me-2" aria-label="Voltar">
        <i class="bi bi-arrow-left"></i>
      </a>
      <h4 class="mb-0">{{ substituindo() ? 'Substituir arquivo' : 'Enviar mídia' }}</h4>
    </div>

    @if (substituindo(); as m) {
      <div class="alert alert-info small py-2" style="max-width: 760px">
        <i class="bi bi-arrow-repeat me-1"></i>
        Trocando o arquivo de <strong>{{ m.titulo }}</strong>. A mídia continua nas mesmas grades, com o mesmo
        título e período; as TVs baixam o arquivo novo na próxima sincronização.
      </div>
    } @else if (planoCompleto()) {
      <div class="alert alert-warning small py-2" style="max-width: 760px" data-teste="aviso-plano">
        <i class="bi bi-exclamation-triangle me-1"></i>
        Seu plano permite {{ uso()?.limiteMidiasAtivas }} mídia(s) ativa(s) e todas estão em uso: esta nova será
        salva <strong>inativa</strong>. Para trocar o anúncio no ar, use "Substituir arquivo" na mídia atual.
      </div>
    }

    <div class="card" style="max-width: 760px">
      <div class="card-body">
        <div class="mb-3">
          <label class="form-label fw-semibold" for="arquivo">Arquivo</label>
          <input id="arquivo" type="file" class="form-control" accept=".mp4,.jpg,.jpeg,.png,video/mp4,image/jpeg,image/png"
                 [disabled]="enviando()" (change)="onArquivo($event)">
          <div class="form-text">
            Vídeo MP4 ou imagem JPG/PNG, até {{ limiteArquivo() }}.
            @if (uso(); as u) { Disponível: {{ disponivel() }}. }
          </div>
        </div>

        @if (arquivo(); as a) {
          <div class="row g-3 mb-3">
            <div class="col-md-5">
              @if (previa()) {
                @if (tipo() === 'Video') {
                  <video [src]="previa()" class="w-100 rounded border" muted controls
                         (loadedmetadata)="onMetadados($event)"></video>
                } @else {
                  <img [src]="previa()" class="w-100 rounded border" alt="Prévia da imagem">
                }
              }
              <div class="small text-muted mt-1">{{ a.name }} · {{ tamanhoArquivo() }}</div>
            </div>
            <div class="col-md-7">
              @if (!substituindo()) {
                <div class="mb-3">
                  <label class="form-label" for="titulo">Título</label>
                  <input id="titulo" class="form-control" maxlength="150" [(ngModel)]="titulo" [disabled]="enviando()">
                </div>
              }
              <div class="mb-3">
                <label class="form-label" for="duracao">Duração na tela (s)</label>
                <input id="duracao" type="number" class="form-control" min="1" [max]="duracaoMaxima"
                       [(ngModel)]="duracaoSegundos" [disabled]="enviando()">
                <div class="form-text">
                  {{ tipo() === 'Video' ? 'Preenchida com a duração do vídeo.' : 'Tempo que a imagem fica na tela.' }}
                </div>
              </div>
              @if (!substituindo()) {
                <div class="row g-2">
                  <div class="col-6">
                    <label class="form-label" for="inicio">Exibir a partir de</label>
                    <input id="inicio" type="date" class="form-control" [(ngModel)]="inicioExibicao" [disabled]="enviando()">
                  </div>
                  <div class="col-6">
                    <label class="form-label" for="fim">Exibir até</label>
                    <input id="fim" type="date" class="form-control" [(ngModel)]="fimExibicao" [disabled]="enviando()">
                  </div>
                </div>
              }
            </div>
          </div>
        }

        @if (etapa() !== 'escolha') {
          <div class="mb-3">
            <div class="small mb-1">{{ textoEtapa() }}</div>
            <div class="progress" role="progressbar" [attr.aria-valuenow]="progresso()" aria-valuemin="0" aria-valuemax="100">
              <div class="progress-bar progress-bar-striped progress-bar-animated" [style.width.%]="progresso()"></div>
            </div>
          </div>
        }

        @if (erro()) {
          <div class="alert alert-danger py-2" role="alert">{{ erro() }}</div>
        }

        <div class="d-flex justify-content-end gap-2">
          <a routerLink="/indoor/midia" class="btn btn-outline-secondary" [class.disabled]="enviando()">Cancelar</a>
          <button type="button" class="btn btn-primary" [disabled]="!podeEnviar()" (click)="enviar()">
            <i class="bi bi-cloud-arrow-up me-1"></i>{{ substituindo() ? 'Substituir' : 'Enviar' }}
          </button>
        </div>
      </div>
    </div>
  `,
})
export class EnvioMidiaPageComponent implements OnInit {
  private service = inject(MidiaService);
  private toast = inject(ToastService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  readonly duracaoMaxima = DURACAO_MAXIMA_SEGUNDOS;

  arquivo = signal<File | null>(null);
  previa = signal<string | null>(null);
  uso = signal<UsoArmazenamento | null>(null);
  /** A mídia cujo arquivo está sendo trocado (`?substituir=`). */
  substituindo = signal<MidiaDto | null>(null);
  etapa = signal<Etapa>('escolha');
  progresso = signal(0);
  erro = signal<string | null>(null);

  titulo = '';
  duracaoSegundos: number | null = DURACAO_PADRAO_IMAGEM;
  inicioExibicao = '';
  fimExibicao = '';

  tipo = computed(() => {
    const a = this.arquivo();
    return a ? TIPOS_ACEITOS[a.type] ?? null : null;
  });
  enviando = computed(() => this.etapa() !== 'escolha');
  tamanhoArquivo = computed(() => formatarBytes(this.arquivo()?.size));
  limiteArquivo = computed(() => formatarBytes(this.uso()?.tamanhoMaximoArquivoBytes ?? 50 * 1024 * 1024));
  /** O arquivo trocado libera o espaço dele. */
  private livreBytes = computed(() => {
    const u = this.uso();
    return u ? Math.max(0, u.cotaBytes - u.usadoBytes + (this.substituindo()?.tamanhoBytes ?? 0)) : 0;
  });
  disponivel = computed(() => (this.uso() ? formatarBytes(this.livreBytes()) : ''));
  planoCompleto = computed(() => {
    const u = this.uso();
    return !!u && u.midiasAtivas >= u.limiteMidiasAtivas;
  });
  textoEtapa = computed(() => ({
    escolha: '',
    hash: 'Conferindo o arquivo…',
    envio: `Enviando… ${this.progresso()}%`,
    registro: 'Salvando…',
  })[this.etapa()]);

  ngOnInit(): void {
    this.service.usoArmazenamento().subscribe(u => this.uso.set(u));
    const id = Number(this.route.snapshot.queryParamMap.get('substituir'));
    if (id) {
      this.service.getById(id).subscribe({
        next: m => this.substituindo.set(m),
        error: () => this.router.navigate(['/indoor/midia']),
      });
    }
  }

  onArquivo(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const arquivo = input.files?.[0] ?? null;
    this.erro.set(null);
    this.limparPrevia();
    this.arquivo.set(null);
    if (!arquivo) return;

    const tipo = TIPOS_ACEITOS[arquivo.type];
    const u = this.uso();
    if (!tipo) {
      this.erro.set('Formato não aceito. Use vídeo MP4 ou imagem JPG/PNG.');
    } else if (u && arquivo.size > u.tamanhoMaximoArquivoBytes) {
      this.erro.set(`O arquivo tem ${formatarBytes(arquivo.size)}; o limite é ${formatarBytes(u.tamanhoMaximoArquivoBytes)}.`);
    } else if (u && arquivo.size > this.livreBytes()) {
      this.erro.set(`Sem espaço: restam ${this.disponivel()}. Exclua mídias que não usa mais.`);
    }
    if (this.erro()) {
      input.value = '';
      return;
    }

    this.arquivo.set(arquivo);
    this.previa.set(URL.createObjectURL(arquivo));
    const atual = this.substituindo();
    if (atual) {
      this.titulo = atual.titulo;
      // Imagem nova mantém a duração na tela de antes; vídeo lê a do arquivo.
      this.duracaoSegundos = tipo === 'Imagem' ? atual.duracaoSegundos : null;
    } else {
      this.titulo = arquivo.name.replace(/\.[^.]+$/, '').replace(/[_-]+/g, ' ').trim().slice(0, 150);
      this.duracaoSegundos = tipo === 'Imagem' ? DURACAO_PADRAO_IMAGEM : null;
    }
  }

  onMetadados(ev: Event): void {
    const duracao = (ev.target as HTMLVideoElement).duration;
    if (isFinite(duracao) && duracao > 0) {
      this.duracaoSegundos = Math.min(DURACAO_MAXIMA_SEGUNDOS, Math.max(1, Math.round(duracao)));
    }
  }

  podeEnviar(): boolean {
    const d = Number(this.duracaoSegundos);
    return !!this.arquivo() && !!this.tipo() && !this.enviando()
      && !!this.titulo.trim() && d >= 1 && d <= DURACAO_MAXIMA_SEGUNDOS
      && (!this.inicioExibicao || !this.fimExibicao || this.inicioExibicao <= this.fimExibicao);
  }

  async enviar(): Promise<void> {
    const arquivo = this.arquivo();
    if (!arquivo || !this.podeEnviar()) return;

    this.erro.set(null);
    this.progresso.set(0);
    this.etapa.set('hash');

    let hash: string | null;
    try {
      hash = await calcularSha256(arquivo);
    } catch {
      // Sem crypto.subtle (página fora de HTTPS): a API calcula sozinha.
      hash = null;
    }

    const atual = this.substituindo();
    const nasceInativa = !atual && this.planoCompleto();
    this.etapa.set('envio');
    this.service.prepararEnvio(arquivo.type, arquivo.size).pipe(
      switchMap(envio => this.enviarArquivo(envio.urlEnvio, arquivo).pipe(
        tap(() => this.etapa.set('registro')),
        switchMap(() => atual
          ? this.service.substituirArquivo({
            id: atual.id,
            chave: envio.chave,
            tipoConteudo: arquivo.type,
            hash,
            duracaoSegundos: Number(this.duracaoSegundos),
            version: atual.version,
          })
          : this.service.criar({
            titulo: this.titulo.trim(),
            chave: envio.chave,
            tipoConteudo: arquivo.type,
            hash,
            duracaoSegundos: Number(this.duracaoSegundos),
            inicioExibicao: this.inicioExibicao || null,
            fimExibicao: this.fimExibicao || null,
          })),
      )),
    ).subscribe({
      next: () => {
        if (atual) this.toast.success('Arquivo substituído. As TVs baixam o novo na próxima sincronização.');
        else if (nasceInativa) this.toast.warning('Mídia enviada como inativa: o limite de mídias ativas do seu plano está completo.');
        else this.toast.success('Mídia enviada e ativa. A operadora inclui nas grades das TVs.');
        this.limparPrevia();
        this.router.navigate(['/indoor/midia']);
      },
      error: err => {
        this.etapa.set('escolha');
        this.erro.set(extrairMensagemErro(err) || 'Não foi possível enviar o arquivo. Tente de novo.');
      },
    });
  }

  private enviarArquivo(url: string, arquivo: File): Observable<unknown> {
    return this.service.enviarArquivo(url, arquivo).pipe(
      tap(ev => {
        if (ev.type === HttpEventType.UploadProgress && ev.total) {
          this.progresso.set(Math.round((ev.loaded / ev.total) * 100));
        }
      }),
      filter(ev => ev.type === HttpEventType.Response),
      last(),
    );
  }

  private limparPrevia(): void {
    const url = this.previa();
    if (url) URL.revokeObjectURL(url);
    this.previa.set(null);
  }
}

/** SHA-256 em hexadecimal minúsculo, como a API e o player calculam. */
async function calcularSha256(arquivo: File): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', await arquivo.arrayBuffer());
  return Array.from(new Uint8Array(digest), b => b.toString(16).padStart(2, '0')).join('');
}
