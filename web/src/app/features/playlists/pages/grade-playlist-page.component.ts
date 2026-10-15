import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { AtlasAuthService, ToastService, extrairMensagemErro } from '@atlas/framework';
import { PlaylistService } from '../services/playlist.service';
import { PlaylistDto } from '../models/playlist.model';
import { MidiaRedeService } from '../services/midia-rede.service';
import { TIPO_MIDIA_ROTULO, descreverPeriodo } from '../../midias/services/midia.service';
import { MidiaDto, TipoMidia } from '../../midias/models/midia.model';
import {
  DIAS_SEMANA, DIAS_UTEIS, FIM_DE_SEMANA, TODOS_OS_DIAS, descreverDias, formatarDuracao,
} from '../../../shared/models/indoor.util';

/** Item da grade em edição (o `id` some nos itens ainda não salvos). */
interface ItemEdicao {
  chave: number;
  id?: number;
  midiaId: number;
  parceiroNome: string;
  titulo: string;
  tipo: TipoMidia;
  duracaoSegundos: number;
  diasSemana: number;
  posicaoFixa: boolean;
  midiaAtiva: boolean;
  periodo: string;
}

let sequenciaChave = 0;

/**
 * Grade da playlist, montada pela operadora com as mídias ativas de todos os
 * parceiros: a sequência do loop (arrastar para reordenar) e os dias da
 * semana de cada item. A mesma mídia pode entrar mais de uma vez (o seletor
 * só avisa "já na grade"). Salva a grade inteira de uma vez
 * (`PUT {id}/itens`), com a versão para não sobrescrever edição concorrente.
 */
@Component({
  selector: 'app-grade-playlist-page',
  standalone: true,
  imports: [FormsModule, RouterLink, CdkDropList, CdkDrag, CdkDragHandle],
  styles: [`
    .item-grade { background: #fff; }
    .item-grade.cdk-drag-preview { box-shadow: 0 6px 20px rgba(0, 0, 0, .15); border-radius: 8px; }
    .item-grade.cdk-drag-placeholder { opacity: .35; }
    .cdk-drop-list-dragging .item-grade:not(.cdk-drag-placeholder) { transition: transform 200ms ease; }
    .alca { cursor: grab; }
    .dia { min-width: 2.6rem; }
    .seletor { max-height: 280px; overflow-y: auto; }
  `],
  template: `
    <div class="d-flex flex-wrap align-items-center gap-2 mb-3">
      <a routerLink="/rede/playlist" class="btn btn-sm btn-outline-secondary" aria-label="Voltar">
        <i class="bi bi-arrow-left"></i>
      </a>
      <div class="me-auto">
        <h4 class="mb-0">{{ playlist()?.nome ?? 'Playlist' }}</h4>
        <div class="small text-muted">
          {{ itens().length }} {{ itens().length === 1 ? 'item' : 'itens' }} · ciclo de {{ duracaoCiclo() }}
          @if (playlist(); as p) { · toca em {{ p.quantidadeTelas }} {{ p.quantidadeTelas === 1 ? 'tela' : 'telas' }} }
          @if (playlist()?.ativo === false) { · <span class="text-warning">inativa</span> }
        </div>
        @if (playlist(); as p) {
          <div class="small text-muted" data-teste="composicao">
            <i class="bi bi-shuffle me-1"></i>{{ resumoComposicao(p) }}
          </div>
        }
      </div>
      @if (podeEditar) {
        <button type="button" class="btn btn-outline-secondary" [disabled]="!alterada() || salvando()" (click)="descartar()">
          Descartar
        </button>
        <button type="button" class="btn btn-primary" [disabled]="!alterada() || salvando()" (click)="salvar()">
          <i class="bi bi-check2 me-1"></i>Salvar grade
        </button>
      }
    </div>

    @if (erro()) {
      <div class="alert alert-danger py-2" role="alert">{{ erro() }}</div>
    }

    @if (podeEditar) {
      <div class="card mb-3">
        <div class="card-body py-2">
          <div class="d-flex flex-wrap gap-2 align-items-center mb-2">
            <span class="small fw-semibold me-1">Adicionar mídia</span>
            <input type="search" class="form-control form-control-sm" style="max-width: 280px"
                   placeholder="Buscar por título ou parceiro…" aria-label="Buscar mídia"
                   [ngModel]="busca()" (ngModelChange)="busca.set($event)" />
            <select class="form-select form-select-sm" style="max-width: 240px" aria-label="Filtrar por parceiro"
                    [ngModel]="parceiroFiltro()" (ngModelChange)="parceiroFiltro.set($event)">
              <option [ngValue]="null">Todos os parceiros</option>
              @for (p of parceiros(); track p.id) {
                <option [ngValue]="p.id">{{ p.nome }}</option>
              }
            </select>
            <span class="small text-muted">Só mídias ativas entram na grade.</span>
          </div>
          @if (!midias().length && !carregando()) {
            <div class="small text-muted">Nenhuma mídia ativa na rede — os parceiros enviam e ativam em Mídia Indoor → Mídias.</div>
          } @else if (!midiasFiltradas().length) {
            <div class="small text-muted">Nenhuma mídia ativa com esse filtro.</div>
          } @else {
            <ul class="list-group list-group-flush seletor" aria-label="Mídias disponíveis">
              @for (m of midiasFiltradas(); track m.id) {
                <li class="list-group-item px-1 py-1 d-flex flex-wrap align-items-center gap-2">
                  <i class="bi" [class.bi-camera-video]="m.tipo === 'Video'" [class.bi-image]="m.tipo === 'Imagem'"></i>
                  <div class="flex-grow-1" style="min-width: 200px">
                    <span class="fw-semibold">{{ m.titulo }}</span>
                    <span class="small text-muted"> · {{ m.parceiroNome }} · {{ rotuloTipo(m.tipo) }}, {{ duracao(m.duracaoSegundos) }}</span>
                    @if (m.parceiroBloqueado) { <span class="badge bg-danger ms-1">parceiro bloqueado</span> }
                  </div>
                  @if (vezesNaGrade(m.id); as vezes) {
                    <span class="badge text-bg-info" [title]="'Esta mídia já está na grade ' + vezes + 'x'">Já adicionado ({{ vezes }}×)</span>
                  }
                  <button type="button" class="btn btn-sm btn-outline-primary" (click)="adicionar(m)">
                    <i class="bi bi-plus-lg me-1"></i>Adicionar ao fim
                  </button>
                </li>
              }
            </ul>
          }
        </div>
      </div>
    }

    @if (carregando()) {
      <div class="text-muted">Carregando…</div>
    } @else if (!itens().length) {
      <div class="card"><div class="card-body text-muted text-center py-5">
        A grade está vazia: as telas desta playlist ficam sem nada para tocar.
      </div></div>
    } @else {
      <ol class="list-unstyled mb-0" cdkDropList [cdkDropListDisabled]="!podeEditar" (cdkDropListDropped)="soltar($event)">
        @for (item of itens(); track item.chave; let i = $index) {
          <li class="item-grade card mb-2" cdkDrag>
            <div class="card-body py-2 d-flex flex-wrap align-items-center gap-3">
              @if (podeEditar) {
                <i class="bi bi-grip-vertical fs-5 text-muted alca" cdkDragHandle aria-label="Arrastar para reordenar"></i>
              }
              <span class="badge text-bg-light border">{{ i + 1 }}</span>
              <button type="button" class="btn btn-sm px-1 py-0 border-0"
                      [disabled]="!podeEditar"
                      [attr.aria-pressed]="item.posicaoFixa"
                      [title]="item.posicaoFixa ? 'Posição fixa: não entra no sorteio. Clique para soltar.' : 'Fixar nesta posição (não entra no sorteio)'"
                      (click)="alternarFixa(item)">
                <i class="bi" [class.bi-pin-angle-fill]="item.posicaoFixa" [class.text-primary]="item.posicaoFixa"
                   [class.bi-pin-angle]="!item.posicaoFixa" [class.text-muted]="!item.posicaoFixa"></i>
              </button>
              <div class="flex-grow-1" style="min-width: 200px">
                <div class="fw-semibold">
                  <i class="bi me-1" [class.bi-camera-video]="item.tipo === 'Video'" [class.bi-image]="item.tipo === 'Imagem'"></i>
                  {{ item.titulo }}
                  <span class="small text-muted fw-normal">· {{ item.parceiroNome }}</span>
                  @if (!item.midiaAtiva) { <span class="badge bg-warning text-dark ms-1">mídia inativa</span> }
                </div>
                <div class="small text-muted">{{ duracao(item.duracaoSegundos) }} · {{ item.periodo }} · {{ descreverDias(item.diasSemana) }}</div>
              </div>
              <div class="d-flex flex-wrap gap-1 align-items-center" role="group" [attr.aria-label]="'Dias de ' + item.titulo">
                @for (d of dias; track d.bit) {
                  <button type="button" class="btn btn-sm dia"
                          [class.btn-primary]="temDia(item, d.bit)"
                          [class.btn-outline-secondary]="!temDia(item, d.bit)"
                          [disabled]="!podeEditar" [title]="d.nome"
                          (click)="alternarDia(item, d.bit)">{{ d.curto }}</button>
                }
                @if (podeEditar) {
                  <div class="ms-1">
                    <select class="form-select form-select-sm" aria-label="Atalho de dias" (change)="aplicarAtalho(item, $event)">
                      <option value="">Atalho…</option>
                      <option [value]="todos">Todos os dias</option>
                      <option [value]="uteis">Seg a sex</option>
                      <option [value]="fimDeSemana">Fim de semana</option>
                    </select>
                  </div>
                  <button type="button" class="btn btn-sm btn-outline-danger ms-1" title="Tirar da grade" (click)="remover(i)">
                    <i class="bi bi-x-lg"></i>
                  </button>
                }
              </div>
            </div>
          </li>
        }
      </ol>
    }
  `,
})
export class GradePlaylistPageComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(PlaylistService);
  private midiaRede = inject(MidiaRedeService);
  private toast = inject(ToastService);
  private atlasAuth = inject(AtlasAuthService);

  readonly dias = DIAS_SEMANA;
  readonly todos = TODOS_OS_DIAS;
  readonly uteis = DIAS_UTEIS;
  readonly fimDeSemana = FIM_DE_SEMANA;
  readonly descreverDias = descreverDias;
  readonly duracao = formatarDuracao;
  readonly podeEditar = this.atlasAuth.canUpdateEntity('playlist');

  playlist = signal<PlaylistDto | null>(null);
  itens = signal<ItemEdicao[]>([]);
  midias = signal<MidiaDto[]>([]);
  carregando = signal(true);
  salvando = signal(false);
  erro = signal<string | null>(null);
  private original = signal('');

  busca = signal('');
  parceiroFiltro = signal<number | null>(null);

  /** Parceiros que têm mídia ativa, para o filtro do seletor. */
  parceiros = computed(() => {
    const porId = new Map<number, string>();
    for (const m of this.midias()) porId.set(m.parceiroId, m.parceiroNome);
    return [...porId].map(([id, nome]) => ({ id, nome })).sort((a, b) => a.nome.localeCompare(b.nome));
  });

  midiasFiltradas = computed(() => {
    const termo = this.busca().trim().toLowerCase();
    const parceiro = this.parceiroFiltro();
    return this.midias().filter(m =>
      (parceiro == null || m.parceiroId === parceiro)
      && (!termo || m.titulo.toLowerCase().includes(termo) || m.parceiroNome.toLowerCase().includes(termo)));
  });

  /** Quantas vezes cada mídia já aparece na grade em edição. */
  private contagem = computed(() => {
    const vezes = new Map<number, number>();
    for (const i of this.itens()) vezes.set(i.midiaId, (vezes.get(i.midiaId) ?? 0) + 1);
    return vezes;
  });

  duracaoCiclo = computed(() => formatarDuracao(this.itens().reduce((s, i) => s + i.duracaoSegundos, 0)));
  alterada = computed(() => this.assinatura(this.itens()) !== this.original());

  private get id(): number {
    return Number(this.route.snapshot.paramMap.get('id'));
  }

  ngOnInit(): void {
    this.carregar();
    if (this.podeEditar) this.midiaRede.listarAtivas().subscribe(m => this.midias.set(m));
  }

  rotuloTipo(tipo: TipoMidia): string {
    return TIPO_MIDIA_ROTULO[tipo] ?? tipo;
  }

  vezesNaGrade(midiaId: number): number {
    return this.contagem().get(midiaId) ?? 0;
  }

  soltar(ev: CdkDragDrop<ItemEdicao[]>): void {
    const lista = [...this.itens()];
    moveItemInArray(lista, ev.previousIndex, ev.currentIndex);
    this.itens.set(lista);
  }

  adicionar(m: MidiaDto): void {
    this.itens.update(lista => [...lista, {
      chave: ++sequenciaChave,
      midiaId: m.id,
      posicaoFixa: false,
      parceiroNome: m.parceiroNome,
      titulo: m.titulo,
      tipo: m.tipo,
      duracaoSegundos: m.duracaoSegundos,
      diasSemana: TODOS_OS_DIAS,
      midiaAtiva: m.ativo,
      periodo: descreverPeriodo(m.inicioExibicao, m.fimExibicao),
    }]);
  }

  remover(indice: number): void {
    this.itens.update(lista => lista.filter((_, i) => i !== indice));
  }

  alternarFixa(item: ItemEdicao): void {
    this.itens.update(lista => lista.map(i => (i.chave === item.chave ? { ...i, posicaoFixa: !i.posicaoFixa } : i)));
  }

  /** "Ordem sorteada a cada volta · 1 notícia a cada 3 anúncios (10 s) · relógio · clima". */
  resumoComposicao(p: PlaylistDto): string {
    const partes = [p.embaralharAnuncios ? 'Ordem sorteada a cada volta (📌 ficam no lugar)' : 'Ordem fixa'];
    if (p.duracaoMaximaVoltaSegundos > 0) {
      const voltas = this.voltasParaTodos(p.duracaoMaximaVoltaSegundos);
      partes.push(`voltas de ${formatarDuracao(p.duracaoMaximaVoltaSegundos)}`
        + (voltas > 1 ? `: todos passam a cada ${voltas} voltas (📌 em toda volta)` : ': cabe tudo numa volta'));
    }
    const informativos = ['notícias', p.cartaoTempo ? 'tempo' : '', p.cartaoCotacoes ? 'cotações' : ''].filter(Boolean).join(', ');
    partes.push(p.noticiaACadaAnuncios > 0
      ? `1 informativo (${informativos}) a cada ${p.noticiaACadaAnuncios} anúncio(s) (${p.duracaoNoticiaSegundos} s)`
      : 'sem informativos');
    if (p.exibirRelogio) partes.push('relógio');
    if (p.exibirClima) partes.push('clima');
    return partes.join(' · ');
  }

  /**
   * Quantas voltas o carrossel leva para passar todos os não fixados
   * (aproximado: soma das durações, sem o filtro do dia).
   */
  voltasParaTodos(limite: number): number {
    const fixos = this.itens().filter(i => i.posicaoFixa).reduce((s, i) => s + i.duracaoSegundos, 0);
    const soltos = this.itens().filter(i => !i.posicaoFixa).reduce((s, i) => s + i.duracaoSegundos, 0);
    const porVolta = Math.max(limite - fixos, 1);
    return soltos === 0 ? 1 : Math.max(1, Math.ceil(soltos / porVolta));
  }

  temDia(item: ItemEdicao, bit: number): boolean {
    return (item.diasSemana & bit) !== 0;
  }

  alternarDia(item: ItemEdicao, bit: number): void {
    const dias = item.diasSemana ^ bit;
    if (dias === 0) {
      this.toast.warning('O item precisa de pelo menos um dia. Para tirar da grade, use o X.');
      return;
    }
    this.atualizarDias(item, dias);
  }

  aplicarAtalho(item: ItemEdicao, ev: Event): void {
    const select = ev.target as HTMLSelectElement;
    if (select.value) this.atualizarDias(item, Number(select.value));
    select.value = '';
  }

  descartar(): void {
    const p = this.playlist();
    if (p) this.aplicar(p);
    this.erro.set(null);
  }

  salvar(): void {
    const p = this.playlist();
    if (!p) return;
    this.salvando.set(true);
    this.erro.set(null);
    const itens = this.itens().map(i => ({ id: i.id ?? null, midiaId: i.midiaId, diasSemana: i.diasSemana, posicaoFixa: i.posicaoFixa }));
    this.service.definirItens(p.id, itens, p.version).subscribe({
      next: () => {
        this.salvando.set(false);
        this.toast.success('Grade salva. As TVs recebem na próxima sincronização.');
        // Recarrega para pegar os ids dos itens novos e a versão nova.
        this.carregar();
      },
      error: err => {
        this.salvando.set(false);
        this.erro.set(extrairMensagemErro(err) || 'Não foi possível salvar a grade.');
      },
    });
  }

  private carregar(): void {
    this.carregando.set(true);
    this.service.getById(this.id).subscribe({
      next: p => {
        this.aplicar(p);
        this.carregando.set(false);
      },
      error: () => this.carregando.set(false),
    });
  }

  private aplicar(p: PlaylistDto): void {
    this.playlist.set(p);
    const itens: ItemEdicao[] = (p.itens ?? []).map(i => ({
      chave: ++sequenciaChave,
      id: i.id,
      midiaId: i.midiaId,
      posicaoFixa: i.posicaoFixa,
      parceiroNome: i.parceiroNome,
      titulo: i.titulo,
      tipo: i.tipo,
      duracaoSegundos: i.duracaoSegundos,
      diasSemana: i.diasSemana,
      midiaAtiva: i.midiaAtiva,
      periodo: descreverPeriodo(i.inicioExibicao, i.fimExibicao),
    }));
    this.itens.set(itens);
    this.original.set(this.assinatura(itens));
  }

  private atualizarDias(item: ItemEdicao, dias: number): void {
    this.itens.update(lista => lista.map(i => (i.chave === item.chave ? { ...i, diasSemana: dias } : i)));
  }

  private assinatura(itens: ItemEdicao[]): string {
    return JSON.stringify(itens.map(i => [i.id ?? null, i.midiaId, i.diasSemana, i.posicaoFixa]));
  }
}
