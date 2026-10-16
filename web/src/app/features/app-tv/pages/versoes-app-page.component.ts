import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpEventType } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { filter, from, last, switchMap, tap } from 'rxjs';
import { ConfirmModalService, ToastService, extrairMensagemErro } from '@atlas/framework';
import { VersaoAppService } from '../services/versao-app.service';
import { DISTRIBUICAO_ROTULO, DistribuicaoVersao, VersaoAppDto, VersoesAppDto } from '../models/versao-app.model';
import { formatarBytes } from '../../../shared/models/indoor.util';
import { calcularSha256 } from '../../../shared/utils/hash.util';

/** Espelha VersaoApp.TamanhoMaximoBytes na API. */
const TAMANHO_MAXIMO_BYTES = 50 * 1024 * 1024;

/**
 * Versões do app das TVs (ADR-021). A operadora envia o APK, libera primeiro
 * para as TVs de teste e depois para todas; cada TV baixa e instala sozinha
 * na sincronização seguinte. Suspender interrompe a distribuição (quem já
 * instalou continua na versão).
 */
@Component({
  selector: 'app-versoes-app-page',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink],
  template: `
    <div class="d-flex flex-wrap align-items-center gap-2 mb-3">
      <a routerLink="/rede/tela" class="btn btn-sm btn-outline-secondary"><i class="bi bi-arrow-left me-1"></i>Telas</a>
      <h4 class="mb-0 me-auto">Versões do app das TVs</h4>
    </div>

    @if (dados(); as d) {
      @if (d.telasSemAtualizacaoAutomatica > 0) {
        <div class="alert alert-warning py-2" data-teste="telas-sem-atualizacao">
          {{ d.telasSemAtualizacaoAutomatica }} de {{ d.telasPareadas }} TV(s) pareada(s) estão com um app antigo, sem atualização
          automática: instale a versão nova nelas uma vez (pendrive ou adb). Depois disso elas se atualizam sozinhas.
        </div>
      }
    }

    <div class="card mb-3">
      <div class="card-header"><strong>Enviar versão nova</strong></div>
      <div class="card-body">
        <div class="row g-3">
          <div class="col-12">
            <label class="form-label" for="apk">Arquivo APK</label>
            <input id="apk" type="file" class="form-control" accept=".apk,application/vnd.android.package-archive"
                   (change)="escolher($event)" [disabled]="enviando()" />
            <div class="form-text">
              Gere com <code>flutter build apk --release</code> (até 50 MB). O mesmo APK serve para todas as TVs.
            </div>
          </div>
          <div class="col-md-3">
            <label class="form-label" for="codigo">Código da versão</label>
            <input id="codigo" type="number" min="1" class="form-control" [(ngModel)]="codigo" [disabled]="enviando()" />
            <div class="form-text">O número depois do <code>+</code> no pubspec (ex.: 1.2.0+<strong>7</strong>). Precisa ser maior que o das TVs.</div>
          </div>
          <div class="col-md-3">
            <label class="form-label" for="nome">Nome</label>
            <input id="nome" type="text" class="form-control" maxlength="30" placeholder="Ex.: 1.2.0" [(ngModel)]="nome" [disabled]="enviando()" />
          </div>
          <div class="col-md-6">
            <label class="form-label" for="notas">O que mudou</label>
            <input id="notas" type="text" class="form-control" maxlength="500" placeholder="Opcional" [(ngModel)]="notas" [disabled]="enviando()" />
          </div>
        </div>
        @if (erro(); as e) {
          <div class="alert alert-danger py-2 mt-3 mb-0">{{ e }}</div>
        }
        <div class="d-flex align-items-center gap-3 mt-3">
          <button type="button" class="btn btn-primary" [disabled]="!podeEnviar()" (click)="enviar()">
            <i class="bi bi-cloud-upload me-1"></i>Enviar
          </button>
          @if (enviando()) {
            <div class="progress flex-grow-1" style="height: 1.25rem">
              <div class="progress-bar" [style.width.%]="progresso()">{{ etapa() }} {{ progresso() }}%</div>
            </div>
          }
        </div>
        <div class="form-text mt-2">A versão entra <strong>suspensa</strong>: nenhuma TV instala até você liberar abaixo.</div>
      </div>
    </div>

    <div class="card">
      <div class="card-header d-flex align-items-center">
        <strong class="me-auto">Versões enviadas</strong>
        <button type="button" class="btn btn-sm btn-outline-secondary" title="Recarregar" (click)="carregar()">
          <i class="bi bi-arrow-clockwise"></i>
        </button>
      </div>
      @if (versoes().length === 0) {
        <div class="card-body text-muted">Nenhuma versão enviada ainda.</div>
      } @else {
        <div class="table-responsive">
          <table class="table table-hover align-middle mb-0">
            <thead>
              <tr>
                <th>Versão</th>
                <th>Enviada</th>
                <th>Tamanho</th>
                <th>TVs nesta versão</th>
                <th>Distribuição</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (v of versoes(); track v.id) {
                <tr [attr.data-versao]="v.codigo">
                  <td>
                    <strong>{{ v.nome }}</strong> <span class="text-muted">({{ v.codigo }})</span>
                    @if (v.notas) { <div class="small text-muted">{{ v.notas }}</div> }
                  </td>
                  <td>{{ v.created | date:'dd/MM/yyyy HH:mm' }}</td>
                  <td>{{ tamanho(v.tamanhoBytes) }}</td>
                  <td>{{ v.telasNestaVersao }}</td>
                  <td>
                    <select class="form-select form-select-sm w-auto" [attr.aria-label]="'Distribuição de ' + v.nome"
                            [ngModel]="v.distribuicao" (ngModelChange)="distribuir(v, $event)">
                      @for (opcao of distribuicoes; track opcao) {
                        <option [ngValue]="opcao">{{ rotulo[opcao] }}</option>
                      }
                    </select>
                  </td>
                  <td class="text-end">
                    @if (v.distribuicao === 'Suspensa') {
                      <button type="button" class="btn btn-sm btn-outline-danger" title="Excluir" (click)="excluir(v)">
                        <i class="bi bi-trash"></i>
                      </button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <div class="card-footer small text-muted">
          Cada TV instala a maior versão liberada para ela com código maior que o seu. "Só TVs de teste" vale para as telas
          marcadas como <strong>TV de teste</strong> no cadastro. O Android não volta para uma versão menor: para desfazer,
          envie uma versão nova com a correção.
        </div>
      }
    </div>
  `,
})
export class VersoesAppPageComponent implements OnInit {
  private service = inject(VersaoAppService);
  private toast = inject(ToastService);
  private confirmModal = inject(ConfirmModalService);

  readonly distribuicoes: DistribuicaoVersao[] = ['Suspensa', 'Teste', 'Todas'];
  readonly rotulo = DISTRIBUICAO_ROTULO;

  dados = signal<VersoesAppDto | null>(null);
  versoes = computed(() => this.dados()?.versoes ?? []);

  arquivo = signal<File | null>(null);
  codigo: number | null = null;
  nome = '';
  notas = '';
  enviando = signal(false);
  etapa = signal('');
  progresso = signal(0);
  erro = signal<string | null>(null);

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.service.listar().subscribe(d => {
      this.dados.set(d);
      // Sugere o próximo código.
      if (this.codigo == null && d.versoes.length > 0) this.codigo = Math.max(...d.versoes.map(v => v.codigo)) + 1;
    });
  }

  tamanho(bytes: number): string {
    return formatarBytes(bytes);
  }

  escolher(evento: Event): void {
    const arquivo = (evento.target as HTMLInputElement).files?.[0] ?? null;
    this.erro.set(null);
    if (arquivo && !arquivo.name.toLowerCase().endsWith('.apk')) {
      this.erro.set('Escolha o arquivo .apk gerado pelo Flutter.');
      this.arquivo.set(null);
      return;
    }
    if (arquivo && arquivo.size > TAMANHO_MAXIMO_BYTES) {
      this.erro.set(`O APK tem ${formatarBytes(arquivo.size)}; o limite é ${formatarBytes(TAMANHO_MAXIMO_BYTES)}. Gere com --split-per-abi.`);
      this.arquivo.set(null);
      return;
    }
    this.arquivo.set(arquivo);
  }

  podeEnviar(): boolean {
    return !this.enviando() && !!this.arquivo() && !!this.codigo && this.codigo > 0 && this.nome.trim().length > 0;
  }

  enviar(): void {
    const arquivo = this.arquivo();
    if (!arquivo || !this.podeEnviar()) return;

    this.enviando.set(true);
    this.erro.set(null);
    this.etapa.set('Conferindo');
    this.progresso.set(0);

    let hash = '';
    from(calcularSha256(arquivo)).pipe(
      tap(h => { hash = h; this.etapa.set('Enviando'); }),
      switchMap(() => this.service.prepararEnvio(arquivo.size)),
      switchMap(envio => this.service.enviarArquivo(envio, arquivo).pipe(
        tap(ev => {
          if (ev.type === HttpEventType.UploadProgress && ev.total) this.progresso.set(Math.round((ev.loaded / ev.total) * 100));
        }),
        filter(ev => ev.type === HttpEventType.Response),
        last(),
        switchMap(() => this.service.criar({
          codigo: Number(this.codigo),
          nome: this.nome.trim(),
          notas: this.notas.trim() || null,
          chave: envio.chave,
          hash,
        })),
      )),
    ).subscribe({
      next: () => {
        this.toast.success(`Versão ${this.nome.trim()} enviada. Libere para as TVs de teste quando quiser.`);
        this.enviando.set(false);
        this.arquivo.set(null);
        this.codigo = null;
        this.nome = '';
        this.notas = '';
        this.carregar();
      },
      error: err => {
        this.enviando.set(false);
        this.erro.set(extrairMensagemErro(err) || 'Não foi possível enviar o APK. Tente de novo.');
      },
    });
  }

  distribuir(v: VersaoAppDto, distribuicao: DistribuicaoVersao): void {
    if (distribuicao === v.distribuicao) return;
    const aplicar = () => this.service.definirDistribuicao(v.id, distribuicao, v.version).subscribe({
      next: () => {
        this.toast.success(distribuicao === 'Suspensa'
          ? `Versão ${v.nome} suspensa: nenhuma TV nova instala.`
          : `Versão ${v.nome} liberada (${DISTRIBUICAO_ROTULO[distribuicao]}). As TVs instalam na próxima sincronização.`);
        this.carregar();
      },
      error: () => this.carregar(),
    });

    if (distribuicao !== 'Todas') {
      aplicar();
      return;
    }
    this.confirmModal.confirm({
      title: 'Liberar para todas as TVs',
      message: `Todas as TVs com versão menor que ${v.codigo} vão baixar e instalar ${v.nome} na próxima sincronização. `
        + 'Testou numa TV de teste antes? Confirma?',
      confirmText: 'Liberar para todas',
      cancelText: 'Cancelar',
      iconClass: 'bi bi-cloud-arrow-down text-primary',
    }).subscribe(ok => (ok ? aplicar() : this.carregar()));
  }

  excluir(v: VersaoAppDto): void {
    this.confirmModal.confirm({
      title: 'Excluir versão',
      message: `Excluir a versão ${v.nome} (${v.codigo}) e o APK dela?`,
      confirmText: 'Sim, excluir',
      cancelText: 'Cancelar',
      iconClass: 'bi bi-trash text-danger',
    }).subscribe(ok => {
      if (!ok) return;
      this.service.excluir(v.id).subscribe(() => {
        this.toast.success('Versão excluída.');
        this.carregar();
      });
    });
  }
}
