import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { interval } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AcaoLinha, AtlasAuthService, ConfirmModalService, EventoFiltro, EventoPaginacao, FormItemConfig,
  FormModalService, ListagemDinamicaComponent, QueryParams, ToastService,
} from '@atlas/framework';
import { TelaService } from '../services/tela.service';
import { LinhaTela, TelaDto } from '../models/tela.model';
import { TELA_COLUNAS, criarCamposTela } from '../configs/tela.config';
import { PlaylistService } from '../../playlists/services/playlist.service';
import { preencherCampos } from '../../../shared/utils/form.util';

/** Nome físico da tabela — chave do RBAC no front. */
const ENTIDADE = 'tela';

/** Código exibido em destaque depois de "Gerar código". */
interface CodigoExibido {
  tela: string;
  codigo: string;
  expiraEm: number;
}

/**
 * Telas (TVs) do parceiro: cadastro, playlist de cada uma, pareamento por
 * código (digitado no app da TV) e a situação da conexão.
 */
@Component({
  selector: 'app-tela-list-page',
  standalone: true,
  imports: [ListagemDinamicaComponent, DatePipe],
  template: `
    @if (codigo(); as c) {
      <div class="card border-primary mb-3">
        <div class="card-body d-flex flex-wrap align-items-center gap-4">
          <div>
            <div class="small text-muted">Código de pareamento — {{ c.tela }}</div>
            <div class="display-5 fw-bold font-monospace" style="letter-spacing: .3em">{{ c.codigo }}</div>
          </div>
          <div class="flex-grow-1 small">
            Abra o app <strong>Mídia Indoor</strong> na TV e digite este código.
            @if (restante() > 0) {
              <div class="text-muted">Vale por mais {{ restanteTexto() }} (até {{ c.expiraEm | date:'dd/MM HH:mm' }}).</div>
            } @else {
              <div class="text-danger">Expirou. Gere outro.</div>
            }
          </div>
          <button type="button" class="btn btn-outline-secondary btn-sm" (click)="fecharCodigo()">
            <i class="bi bi-check2 me-1"></i>Feito
          </button>
        </div>
      </div>
    }

    <div class="btn-group btn-group-sm mb-3" role="group" aria-label="Filtrar por situação">
      @for (f of filtros; track f.rotulo) {
        <button type="button" class="btn"
                [class.btn-primary]="ativo() === f.valor"
                [class.btn-outline-primary]="ativo() !== f.valor"
                (click)="filtrarAtivo(f.valor)">{{ f.rotulo }}</button>
      }
    </div>

    <atlas-listagem-dinamica
      titulo="Telas"
      entityName="${ENTIDADE}"
      campoId="id"
      [autoInjetarId]="false"
      mensagemVazio="Nenhuma tela. Cadastre a primeira TV e gere o código para pareá-la."
      [itens]="itens()"
      [totalElementos]="total()"
      [colunas]="colunas"
      [carregando]="carregando()"
      [acoesLinha]="acoesLinha"
      (criarNovo)="onNova()"
      (recarregar)="carregar()"
      (linhaClicada)="onEditar($event)"
      (editar)="onEditar($event)"
      (excluir)="onExcluir($event)"
      (clonar)="onCopiar($event)"
      (acaoLinha)="onAcaoLinha($event)"
      (onPaginationChange)="onPaginacao($event)"
      (onFilterChange)="onFiltro($event)">
    </atlas-listagem-dinamica>
  `,
})
export class TelaListPageComponent implements OnInit {
  private service = inject(TelaService);
  private playlistService = inject(PlaylistService);
  private formModal = inject(FormModalService);
  private confirmModal = inject(ConfirmModalService);
  private toast = inject(ToastService);
  private atlasAuth = inject(AtlasAuthService);
  private destroyRef = inject(DestroyRef);

  readonly colunas = TELA_COLUNAS;
  readonly filtros: { rotulo: string; valor: boolean | null }[] = [
    { rotulo: 'Todas', valor: null },
    { rotulo: 'Ativas', valor: true },
    { rotulo: 'Inativas', valor: false },
  ];

  itens = signal<LinhaTela[]>([]);
  total = signal(0);
  carregando = signal(false);
  ativo = signal<boolean | null>(null);
  codigo = signal<CodigoExibido | null>(null);
  private agora = signal(Date.now());

  restante = computed(() => Math.max(0, Math.floor(((this.codigo()?.expiraEm ?? 0) - this.agora()) / 1000)));
  restanteTexto = computed(() => {
    const s = this.restante();
    if (s >= 3600) return `${Math.floor(s / 3600)} h ${String(Math.floor((s % 3600) / 60)).padStart(2, '0')} min`;
    return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
  });

  private query: QueryParams = { pageIndex: 0, pageSize: 10 };

  acoesLinha: AcaoLinha[] = [
    {
      id: 'codigo', label: (t: LinhaTela) => (t.situacao === 'AguardandoPareamento' ? 'Gerar código' : 'Parear outra TV'),
      icone: 'bi bi-qr-code', classe: 'btn-outline-primary',
      visivel: (t: LinhaTela) => t.ativo && this.podeAtualizar(),
    },
    {
      id: 'desvincular', label: 'Desvincular TV', icone: 'bi bi-link-45deg', classe: 'btn-outline-danger',
      visivel: (t: LinhaTela) => t.situacao !== 'AguardandoPareamento' && this.podeAtualizar(),
    },
    {
      id: 'inativar', label: 'Inativar', icone: 'bi bi-pause-circle', classe: 'btn-outline-warning',
      visivel: (t: LinhaTela) => t.ativo && this.podeAtualizar(),
    },
    {
      id: 'ativar', label: 'Ativar', icone: 'bi bi-check-circle', classe: 'btn-outline-success',
      visivel: (t: LinhaTela) => !t.ativo && this.podeAtualizar(),
    },
  ];

  ngOnInit(): void {
    this.carregar();
    interval(1000).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.agora.set(Date.now()));
    // A situação (online/offline) muda sozinha: atualiza a lista a cada minuto.
    interval(60_000).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.carregar(false));
  }

  carregar(mostrarCarregando = true): void {
    if (mostrarCarregando) this.carregando.set(true);
    this.service.listar(this.query, this.ativo()).subscribe({
      next: page => {
        this.itens.set(page.content);
        this.total.set(page.totalElements);
        this.carregando.set(false);
      },
      error: () => this.carregando.set(false),
    });
  }

  filtrarAtivo(valor: boolean | null): void {
    this.ativo.set(valor);
    this.query = { ...this.query, pageIndex: 0 };
    this.carregar();
  }

  onPaginacao(e: EventoPaginacao): void { this.query = { ...this.query, pageIndex: e.pageIndex, pageSize: e.pageSize }; this.carregar(); }
  onFiltro(e: EventoFiltro): void { this.query = { ...this.query, search: e.busca, pageIndex: 0 }; this.carregar(); }

  onNova(): void {
    this.abrir(null);
  }

  onEditar(item: LinhaTela): void {
    this.service.getById(item.id).subscribe(tela => this.abrir(tela));
  }

  /** Nova tela com o local e a playlist de outra (cada TV pareia com o próprio código). */
  onCopiar(item: LinhaTela): void {
    this.abrir(null, { nome: `${item.nome} (cópia)`, localizacao: item.localizacao, playlistId: item.playlistId });
  }

  onAcaoLinha(ev: { acaoId: string; item: LinhaTela }): void {
    if (ev.acaoId === 'codigo') this.gerarCodigo(ev.item);
    if (ev.acaoId === 'desvincular') this.desvincular(ev.item);
    if (ev.acaoId === 'ativar') this.definirAtivo(ev.item, true);
    if (ev.acaoId === 'inativar') this.definirAtivo(ev.item, false);
  }

  onExcluir(t: LinhaTela): void {
    this.confirmModal.confirm({
      title: 'Excluir tela',
      message: t.situacao === 'AguardandoPareamento'
        ? `Excluir "${t.nome}"?`
        : `"${t.nome}" tem uma TV pareada, que perde o acesso e para de receber grade. Confirma?`,
      confirmText: 'Sim, excluir',
      cancelText: 'Cancelar',
      iconClass: 'bi bi-trash text-danger',
    }).subscribe(ok => {
      if (!ok) return;
      this.service.excluir(t.id).subscribe(() => {
        this.toast.success('Tela excluída.');
        this.carregar();
      });
    });
  }

  fecharCodigo(): void {
    this.codigo.set(null);
    this.carregar();
  }

  private abrir(tela: TelaDto | null, modelo?: Partial<TelaDto>): void {
    this.playlistService.listarTodas().subscribe(playlists => {
      const opcoes = playlists.map(p => ({ value: p.id, label: p.ativo ? p.nome : `${p.nome} (inativa)` }));
      const dados = modelo ? preencherCampos(criarCamposTela(opcoes), modelo) : criarCamposTela(opcoes);
      const campos: FormItemConfig[] = tela
        ? [
          { type: 'section', label: 'Tela', children: preencherCampos(dados, tela) },
          {
            type: 'section', label: 'Histórico', children: [
              { type: 'history', name: 'historico', label: '', col: 'col-12', entityName: ENTIDADE },
            ],
          },
        ]
        : dados;

      this.formModal.open({
        titulo: tela ? `Editar ${tela.nome}` : 'Nova tela',
        entidade: 'Tela',
        entityName: ENTIDADE,
        recordId: tela ? String(tela.id) : null,
        campos,
        tabbedSections: !!tela,
        largura: '640px',
        errosInline: true,
        permitirContinuar: false,
        mostrarBotaoSalvarECopiar: false,
        registro: tela,
        persistir: (valor, atual) => this.service.salvar(atual?.id ?? null, valor, atual?.version),
        msgSucesso: tela ? 'Tela salva.' : 'Tela cadastrada. Use "Gerar código" para parear a TV.',
      }).subscribe(res => { if (res?.recarregar || res?.acao === 'fechar') this.carregar(); });
    });
  }

  private gerarCodigo(t: LinhaTela): void {
    const pedir = () => this.service.gerarCodigo(t.id, t.version).subscribe(r => {
      this.codigo.set({ tela: t.nome, codigo: r.codigo, expiraEm: new Date(r.expiraEm).getTime() });
      this.carregar(false);
    });

    if (t.situacao === 'AguardandoPareamento') {
      pedir();
      return;
    }
    this.confirmModal.confirm({
      title: 'Parear outra TV',
      message: `A TV atual de "${t.nome}" continua funcionando até outra TV usar o código. Gerar código?`,
      confirmText: 'Gerar código',
      cancelText: 'Cancelar',
      iconClass: 'bi bi-qr-code text-primary',
    }).subscribe(ok => { if (ok) pedir(); });
  }

  private desvincular(t: LinhaTela): void {
    this.confirmModal.confirm({
      title: 'Desvincular TV',
      message: `A TV de "${t.nome}" perde o acesso na próxima sincronização e a tela volta a aguardar pareamento. Confirma?`,
      confirmText: 'Sim, desvincular',
      cancelText: 'Cancelar',
      iconClass: 'bi bi-link-45deg text-danger',
    }).subscribe(ok => {
      if (!ok) return;
      this.service.desvincular(t.id, t.version).subscribe(() => {
        this.toast.success('TV desvinculada.');
        this.carregar();
      });
    });
  }

  private definirAtivo(t: LinhaTela, ativo: boolean): void {
    this.service.definirAtivo(t.id, ativo, t.version).subscribe(() => {
      this.toast.success(ativo ? 'Tela ativada.' : 'Tela inativada: a TV continua no que já baixou, sem receber grade nova.');
      this.carregar();
    });
  }

  private podeAtualizar(): boolean {
    return this.atlasAuth.canUpdateEntity(ENTIDADE);
  }
}
