import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ToastService, extrairMensagemErro } from '@atlas/framework';
import { RelatorioService } from './relatorio.service';
import { RelatorioVeiculacaoDto } from './relatorio.model';

type Atalho = 'mes' | 'mesPassado' | 'semana' | 'livre';

/**
 * Relatório de veiculação (ADR-023): o que as TVs registraram que exibiram e
 * quantas pessoas leram o QR. O cliente vê o dele (`/indoor/midia/relatorio`);
 * a operadora vê o de qualquer cliente (`/rede/cliente/:id/relatorio`).
 * "Copiar resumo" monta o texto para mandar ao anunciante pelo WhatsApp.
 */
@Component({
  selector: 'app-relatorio-veiculacao-page',
  standalone: true,
  imports: [DatePipe, DecimalPipe, FormsModule, RouterLink],
  template: `
    <div class="d-flex flex-wrap align-items-center gap-2 mb-3">
      <a [routerLink]="voltar()" class="btn btn-sm btn-outline-secondary"><i class="bi bi-arrow-left me-1"></i>Voltar</a>
      <h4 class="mb-0 me-auto">Relatório de veiculação @if (r(); as d) { <small class="text-muted">· {{ d.clienteNome }}</small> }</h4>
      <div class="btn-group btn-group-sm" role="group" aria-label="Período">
        @for (a of atalhos; track a.id) {
          <button type="button" class="btn" [class.btn-primary]="atalho() === a.id" [class.btn-outline-primary]="atalho() !== a.id"
                  (click)="escolher(a.id)">{{ a.rotulo }}</button>
        }
      </div>
      <input type="date" class="form-control form-control-sm w-auto" aria-label="De" [(ngModel)]="de" (change)="livre()" />
      <input type="date" class="form-control form-control-sm w-auto" aria-label="Até" [(ngModel)]="ate" (change)="livre()" />
    </div>

    @if (erro(); as e) { <div class="alert alert-danger">{{ e }}</div> }

    @if (r(); as d) {
      <div class="alert alert-secondary py-2 small" data-teste="aviso-tela-ligada">
        <i class="bi bi-info-circle me-1"></i>
        As exibições são registradas pelo app da TV quando o anúncio sai da tela. O relatório
        <strong>ainda não sabe se a TV estava ligada</strong> e na entrada certa. Período de
        {{ d.de | date:'dd/MM/yyyy' }} a {{ d.ate | date:'dd/MM/yyyy' }}.
      </div>

      <div class="row g-3 mb-3">
        @for (c of cartoes(); track c.rotulo) {
          <div class="col-6 col-lg">
            <div class="card h-100"><div class="card-body py-2">
              <div class="small text-muted">{{ c.rotulo }}</div>
              <div class="fs-3 fw-semibold" [attr.data-teste]="c.teste">{{ c.valor }}</div>
              @if (c.detalhe) { <div class="small text-muted">{{ c.detalhe }}</div> }
            </div></div>
          </div>
        }
      </div>

      <div class="d-flex mb-3">
        <button type="button" class="btn btn-sm btn-success" (click)="copiarResumo(d)" data-teste="copiar-resumo">
          <i class="bi bi-whatsapp me-1"></i>Copiar resumo para o WhatsApp
        </button>
      </div>

      @if (d.exibicoes === 0 && d.leiturasQr === 0 && d.toquesDiretorio === 0) {
        <div class="card"><div class="card-body text-muted">Nenhuma exibição registrada neste período.</div></div>
      } @else {
        <div class="card mb-3">
          <div class="card-header"><strong>Por horário</strong> <span class="small text-muted">(hora local de cada TV)</span></div>
          <div class="card-body">
            <div class="d-flex align-items-end gap-1" style="height: 120px" data-teste="por-hora">
              @for (v of d.porHora; track $index) {
                <div class="flex-fill bg-primary rounded-top" [style.height.%]="altura(v)" [title]="$index + 'h: ' + v + ' exibições'"
                     [style.opacity]="v ? 1 : .15" style="min-height: 2px"></div>
              }
            </div>
            <div class="d-flex justify-content-between small text-muted mt-1"><span>0h</span><span>6h</span><span>12h</span><span>18h</span><span>23h</span></div>
          </div>
        </div>

        <div class="row g-3">
          <div class="col-lg-6">
            <div class="card h-100">
              <div class="card-header"><strong>Por TV</strong></div>
              <div class="table-responsive"><table class="table table-sm mb-0">
                <thead><tr><th>TV</th><th>Local</th><th class="text-end">Exibições</th><th class="text-end">Tempo na tela</th></tr></thead>
                <tbody>
                  @for (t of d.porTela; track t.telaId) {
                    <tr><td>{{ t.nome }}</td><td>{{ t.parceiro ?? '—' }}</td><td class="text-end">{{ t.exibicoes | number }}</td><td class="text-end">{{ tempo(t.segundosNaTela) }}</td></tr>
                  }
                </tbody>
              </table></div>
            </div>
          </div>
          <div class="col-lg-6">
            <div class="card h-100">
              <div class="card-header"><strong>Por anúncio</strong></div>
              <div class="table-responsive"><table class="table table-sm mb-0">
                <thead><tr><th>Anúncio</th><th class="text-end">Exibições</th><th class="text-end">Leituras do QR</th></tr></thead>
                <tbody>
                  @for (m of d.porMidia; track m.midiaId) {
                    <tr>
                      <td>{{ m.titulo }}</td>
                      <td class="text-end">{{ m.exibicoes | number }}</td>
                      <td class="text-end">
                        @if (m.qrRastreado || m.leiturasQr) { {{ m.leiturasQr | number }} <span class="small text-muted">({{ m.leitoresUnicosQr }} {{ m.leitoresUnicosQr === 1 ? 'única' : 'únicas' }})</span> }
                        @else { <span class="small text-muted">não rastreado</span> }
                      </td>
                    </tr>
                  }
                </tbody>
              </table></div>
            </div>
          </div>
          <div class="col-12">
            <div class="card">
              <div class="card-header"><strong>Por dia</strong></div>
              <div class="table-responsive"><table class="table table-sm mb-0">
                <thead><tr><th>Dia</th><th class="text-end">Exibições</th><th class="text-end">Leituras do QR</th><th class="text-end">Toques no diretório</th></tr></thead>
                <tbody>
                  @for (dia of d.porDia; track dia.data) {
                    <tr><td>{{ dia.data | date:'EEE, dd/MM':'':'pt-BR' }}</td><td class="text-end">{{ dia.exibicoes | number }}</td><td class="text-end">{{ dia.leiturasQr | number }}</td><td class="text-end">{{ dia.toquesDiretorio | number }}</td></tr>
                  }
                </tbody>
              </table></div>
            </div>
          </div>
        </div>
      }
    } @else if (!erro()) {
      <div class="text-muted">Carregando…</div>
    }
  `,
})
export class RelatorioVeiculacaoPageComponent implements OnInit {
  private service = inject(RelatorioService);
  private toast = inject(ToastService);
  private rota = inject(ActivatedRoute);

  readonly atalhos: { id: Atalho; rotulo: string }[] = [
    { id: 'mes', rotulo: 'Este mês' },
    { id: 'mesPassado', rotulo: 'Mês passado' },
    { id: 'semana', rotulo: 'Últimos 7 dias' },
  ];

  /** Nulo = o próprio cliente logado; número = visão da operadora. */
  clienteId: number | null = null;
  de = '';
  ate = '';
  atalho = signal<Atalho>('mes');
  r = signal<RelatorioVeiculacaoDto | null>(null);
  erro = signal<string | null>(null);

  maximoHora = computed(() => Math.max(1, ...(this.r()?.porHora ?? [0])));

  cartoes = computed(() => {
    const d = this.r();
    if (!d) return [];
    const pico = this.horaDePico(d);
    return [
      { rotulo: 'Exibições', valor: d.exibicoes.toLocaleString('pt-BR'), teste: 'total-exibicoes',
        detalhe: `${d.porTela.length} TV(s)${d.interrompidas ? ` · ${d.interrompidas} interrompida(s)` : ''}` },
      { rotulo: 'Tempo na tela', valor: this.tempo(d.segundosNaTela), teste: 'tempo-na-tela',
        detalhe: d.semConexao ? `${d.semConexao} sem internet na TV (tocou do que já tinha)` : '' },
      { rotulo: 'Leituras do QR', valor: d.leiturasQr.toLocaleString('pt-BR'), teste: 'leituras-qr',
        detalhe: `${d.leitoresUnicosQr} leitor(es) único(s) por dia` },
      { rotulo: 'Toques no diretório', valor: d.toquesDiretorio.toLocaleString('pt-BR'), teste: 'toques-diretorio',
        detalhe: d.toquesPorContato.length
          ? d.toquesPorContato.map(c => `${c.rotulo} ${c.toques}`).join(' · ')
          : 'contatos na página do QR da TV' },
      { rotulo: 'Horário de pico', valor: pico == null ? '—' : `${pico}h`, teste: 'pico', detalhe: 'mais exibições' },
    ];
  });

  ngOnInit(): void {
    const id = this.rota.snapshot.paramMap.get('id');
    this.clienteId = id ? Number(id) : null;
    this.escolher('mes');
  }

  voltar(): string {
    return this.clienteId == null ? '/indoor/midia' : '/rede/cliente';
  }

  escolher(atalho: Atalho): void {
    this.atalho.set(atalho);
    const hoje = new Date();
    if (atalho === 'mes') {
      this.de = iso(new Date(hoje.getFullYear(), hoje.getMonth(), 1));
      this.ate = iso(hoje);
    } else if (atalho === 'mesPassado') {
      this.de = iso(new Date(hoje.getFullYear(), hoje.getMonth() - 1, 1));
      this.ate = iso(new Date(hoje.getFullYear(), hoje.getMonth(), 0));
    } else if (atalho === 'semana') {
      this.de = iso(new Date(hoje.getFullYear(), hoje.getMonth(), hoje.getDate() - 6));
      this.ate = iso(hoje);
    }
    this.carregar();
  }

  livre(): void {
    this.atalho.set('livre');
    if (this.de && this.ate) this.carregar();
  }

  carregar(): void {
    this.erro.set(null);
    this.service.obter(this.clienteId, this.de || null, this.ate || null).subscribe({
      next: d => this.r.set(d),
      error: e => { this.r.set(null); this.erro.set(extrairMensagemErro(e) || 'Não foi possível carregar o relatório.'); },
    });
  }

  altura(v: number): number {
    return (v / this.maximoHora()) * 100;
  }

  tempo(segundos: number): string {
    if (segundos < 60) return `${segundos} s`;
    const minutos = Math.floor(segundos / 60);
    if (minutos < 60) return `${minutos} min`;
    const h = Math.floor(minutos / 60);
    return `${h} h ${minutos % 60} min`;
  }

  copiarResumo(d: RelatorioVeiculacaoDto): void {
    const pico = this.horaDePico(d);
    const periodo = `${br(d.de)} a ${br(d.ate)}`;
    const linhas = [
      `*Mídia Indoor · ${d.clienteNome}*`,
      `Período: ${periodo}`,
      `📺 Seu anúncio passou *${d.exibicoes.toLocaleString('pt-BR')} vezes* em ${d.porTela.length} TV(s), ${this.tempo(d.segundosNaTela)} na tela.`,
      d.leiturasQr || d.porMidia.some(m => m.qrRastreado)
        ? `📱 *${d.leiturasQr.toLocaleString('pt-BR')} leituras* do QR code (${d.leitoresUnicosQr} pessoas diferentes por dia).`
        : '',
      d.toquesDiretorio
        ? `👆 *${d.toquesDiretorio.toLocaleString('pt-BR')} toques* nos seus contatos no diretório das TVs (${d.pessoasDiretorio} pessoas diferentes por dia).`
        : '',
      pico == null ? '' : `⏰ Horário com mais exibições: ${pico}h.`,
    ].filter(Boolean);
    navigator.clipboard?.writeText(linhas.join('\n')).then(
      () => this.toast.success('Resumo copiado. Cole na conversa do WhatsApp com o anunciante.'),
      () => this.toast.error('Não foi possível copiar.'),
    );
  }

  private horaDePico(d: RelatorioVeiculacaoDto): number | null {
    const max = Math.max(0, ...d.porHora);
    return max === 0 ? null : d.porHora.indexOf(max);
  }
}

function iso(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function br(isoData: string): string {
  const [a, m, d] = isoData.split('-');
  return `${d}/${m}/${a}`;
}
