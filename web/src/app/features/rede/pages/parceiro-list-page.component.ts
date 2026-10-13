import { Component, OnInit, inject, signal } from '@angular/core';
import {
  AcaoLinha, AtlasAuthService, ConfirmModalService, EventoFiltro, EventoPaginacao, FormItemConfig,
  FormModalService, ListagemDinamicaComponent, QueryParams, ToastService,
} from '@atlas/framework';
import { ParceiroService } from '../services/parceiro.service';
import { LinhaParceiro, ParceiroDto, SituacaoParceiro } from '../models/parceiro.model';
import { PARCEIRO_COLUNAS, criarCamposBloqueio, criarCamposParceiro } from '../configs/parceiro.config';
import { preencherCampos } from '../../../shared/utils/form.util';

/** Nome físico da tabela — chave do RBAC no front. */
const ENTIDADE = 'parceiro';

/**
 * Parceiros da rede (operadora). Cadastrar cria também o usuário do
 * responsável, que recebe a senha temporária por e-mail.
 */
@Component({
  selector: 'app-parceiro-list-page',
  standalone: true,
  imports: [ListagemDinamicaComponent],
  template: `
    <div class="d-flex flex-wrap align-items-center gap-2 mb-3">
      <div class="btn-group btn-group-sm" role="group" aria-label="Filtrar por situação">
        @for (f of filtros; track f.rotulo) {
          <button type="button" class="btn"
                  [class.btn-primary]="situacao() === f.valor"
                  [class.btn-outline-primary]="situacao() !== f.valor"
                  (click)="filtrarSituacao(f.valor)">{{ f.rotulo }}</button>
        }
      </div>
      @if (podeCriar) {
        <button type="button" class="btn btn-sm btn-primary ms-auto" (click)="onNovo()">
          <i class="bi bi-plus-lg me-1"></i>Novo parceiro
        </button>
      }
    </div>

    <atlas-listagem-dinamica
      titulo="Parceiros"
      entityName="${ENTIDADE}"
      campoId="id"
      [autoInjetarId]="false"
      mensagemVazio="Nenhum parceiro. Cadastre o primeiro para ele começar a usar as TVs."
      [itens]="itens()"
      [totalElementos]="total()"
      [colunas]="colunas"
      [carregando]="carregando()"
      [mostrarExcluir]="false"
      [acoesLinha]="acoesLinha"
      [mostrarNovo]="false"
      (recarregar)="carregar()"
      (linhaClicada)="onEditar($event)"
      (editar)="onEditar($event)"
      (acaoLinha)="onAcaoLinha($event)"
      (onPaginationChange)="onPaginacao($event)"
      (onFilterChange)="onFiltro($event)">
    </atlas-listagem-dinamica>
  `,
})
export class ParceiroListPageComponent implements OnInit {
  private service = inject(ParceiroService);
  private formModal = inject(FormModalService);
  private confirmModal = inject(ConfirmModalService);
  private toast = inject(ToastService);
  private atlasAuth = inject(AtlasAuthService);

  readonly colunas = PARCEIRO_COLUNAS;
  /**
   * O "Novo" da grid fica desligado porque ligá-lo liga também o "Clonar": o
   * e-mail do responsável é único, então copiar parceiro não tem uso.
   */
  readonly podeCriar = this.atlasAuth.canCreateEntity(ENTIDADE);
  readonly filtros: { rotulo: string; valor: SituacaoParceiro | null }[] = [
    { rotulo: 'Todos', valor: null },
    { rotulo: 'Ativos', valor: 'Ativo' },
    { rotulo: 'Bloqueados', valor: 'Bloqueado' },
  ];

  itens = signal<LinhaParceiro[]>([]);
  total = signal(0);
  carregando = signal(false);
  situacao = signal<SituacaoParceiro | null>(null);

  private query: QueryParams = { pageIndex: 0, pageSize: 10 };

  acoesLinha: AcaoLinha[] = [
    {
      id: 'bloquear', label: 'Bloquear', icone: 'bi bi-lock', classe: 'btn-outline-danger',
      visivel: (p: LinhaParceiro) => p.situacao !== 'Bloqueado' && this.podeAtualizar(),
    },
    {
      id: 'desbloquear', label: 'Desbloquear', icone: 'bi bi-unlock', classe: 'btn-outline-success',
      visivel: (p: LinhaParceiro) => p.situacao === 'Bloqueado' && this.podeAtualizar(),
    },
  ];

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.carregando.set(true);
    this.service.listar(this.query, this.situacao()).subscribe({
      next: page => {
        this.itens.set(page.content);
        this.total.set(page.totalElements);
        this.carregando.set(false);
      },
      error: () => this.carregando.set(false),
    });
  }

  filtrarSituacao(valor: SituacaoParceiro | null): void {
    this.situacao.set(valor);
    this.query = { ...this.query, pageIndex: 0 };
    this.carregar();
  }

  onPaginacao(e: EventoPaginacao): void { this.query = { ...this.query, pageIndex: e.pageIndex, pageSize: e.pageSize }; this.carregar(); }
  onFiltro(e: EventoFiltro): void { this.query = { ...this.query, search: e.busca, pageIndex: 0 }; this.carregar(); }

  onNovo(): void {
    this.abrir(null, criarCamposParceiro(false));
  }

  onEditar(item: LinhaParceiro): void {
    this.service.getById(item.id).subscribe(parceiro => {
      const campos: FormItemConfig[] = [
        { type: 'section', label: 'Cadastro', children: preencherCampos(criarCamposParceiro(true), parceiro) },
        {
          type: 'section', label: 'Histórico', children: [
            { type: 'history', name: 'historico', label: '', col: 'col-12', entityName: ENTIDADE },
          ],
        },
      ];
      this.abrir(parceiro, campos, true);
    });
  }

  onAcaoLinha(ev: { acaoId: string; item: LinhaParceiro }): void {
    if (ev.acaoId === 'bloquear') this.bloquear(ev.item);
    if (ev.acaoId === 'desbloquear') this.desbloquear(ev.item);
  }

  private abrir(parceiro: ParceiroDto | null, campos: FormItemConfig[], abas = false): void {
    this.formModal.open({
      titulo: parceiro ? `Editar ${parceiro.nome}` : 'Novo parceiro',
      entidade: 'Parceiro',
      entityName: ENTIDADE,
      recordId: parceiro ? String(parceiro.id) : null,
      campos,
      tabbedSections: abas,
      largura: '760px',
      errosInline: true,
      // Só "Salvar e fechar": a API devolve id/versão, não o registro.
      permitirContinuar: false,
      mostrarBotaoSalvarECopiar: false,
      registro: parceiro,
      persistir: (valor, atual) => this.service.salvar(atual?.id ?? null, valor, atual?.version),
      msgSucesso: parceiro
        ? 'Parceiro salvo.'
        : 'Parceiro cadastrado. O responsável recebe a senha temporária por e-mail.',
    }).subscribe(res => { if (res?.recarregar || res?.acao === 'fechar' || res?.acao === 'copiar') this.carregar(); });
  }

  private bloquear(p: LinhaParceiro): void {
    this.formModal.open({
      titulo: `Bloquear ${p.nome}`,
      campos: criarCamposBloqueio(),
      largura: '560px',
      errosInline: true,
      registroIdExibicao: p.id,
      mostrarBotaoSalvarECopiar: false,
      permitirContinuar: false,
      registro: p,
      persistir: valor => this.service.bloquear(p.id, valor.motivo, p.version),
      msgSucesso: 'Parceiro bloqueado.',
    }).subscribe(res => { if (res?.recarregar || res?.acao === 'fechar') this.carregar(); });
  }

  private desbloquear(p: LinhaParceiro): void {
    this.confirmModal.confirm({
      title: 'Desbloquear parceiro',
      message: `${p.nome} volta a ter acesso ao painel e as TVs voltam a receber a grade. Confirma?`,
      confirmText: 'Sim, desbloquear',
      cancelText: 'Cancelar',
      iconClass: 'bi bi-unlock text-success',
    }).subscribe(ok => {
      if (!ok) return;
      this.service.desbloquear(p.id, p.version).subscribe(() => {
        this.toast.success('Parceiro desbloqueado.');
        this.carregar();
      });
    });
  }

  private podeAtualizar(): boolean {
    return this.atlasAuth.canUpdateEntity(ENTIDADE);
  }
}
