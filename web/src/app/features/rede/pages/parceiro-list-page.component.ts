import { Component, OnInit, inject, signal } from '@angular/core';
import {
  AcaoLinha, AtlasAuthService, CampoOption, ConfirmModalService, EventoFiltro, EventoPaginacao, FormItemConfig,
  FormModalService, ListagemDinamicaComponent, QueryParams, ToastService,
} from '@atlas/framework';
import { ParceiroService } from '../services/parceiro.service';
import { ClienteService } from '../services/cliente.service';
import { LinhaParceiro, ParceiroDto } from '../models/parceiro.model';
import { PARCEIRO_COLUNAS, criarCamposParceiro } from '../configs/parceiro.config';
import { preencherCampos } from '../../../shared/utils/form.util';

/** Nome físico da tabela — chave do RBAC no front. */
const ENTIDADE = 'parceiro';

/**
 * Parceiros (operadora): os locais onde as TVs ficam. Não acessam o sistema;
 * quem anuncia é cliente. O parceiro que também anuncia é ligado ao cadastro
 * de cliente dele.
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
                  [class.btn-primary]="ativo() === f.valor"
                  [class.btn-outline-primary]="ativo() !== f.valor"
                  (click)="filtrarAtivo(f.valor)">{{ f.rotulo }}</button>
        }
      </div>
      <span class="small text-muted ms-2">Os locais onde as TVs ficam. Eles não acessam o sistema: quem anuncia é cadastrado em Clientes.</span>
    </div>

    <atlas-listagem-dinamica
      titulo="Parceiros"
      entityName="${ENTIDADE}"
      campoId="id"
      [autoInjetarId]="false"
      mensagemVazio="Nenhum parceiro. Cadastre o estabelecimento onde a TV vai ficar e depois a tela em Rede → Telas."
      [itens]="itens()"
      [totalElementos]="total()"
      [colunas]="colunas"
      [carregando]="carregando()"
      [acoesLinha]="acoesLinha"
      (criarNovo)="onNovo()"
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
export class ParceiroListPageComponent implements OnInit {
  private service = inject(ParceiroService);
  private clienteService = inject(ClienteService);
  private formModal = inject(FormModalService);
  private confirmModal = inject(ConfirmModalService);
  private toast = inject(ToastService);
  private atlasAuth = inject(AtlasAuthService);

  readonly colunas = PARCEIRO_COLUNAS;
  readonly filtros: { rotulo: string; valor: boolean | null }[] = [
    { rotulo: 'Todos', valor: null },
    { rotulo: 'Ativos', valor: true },
    { rotulo: 'Inativos', valor: false },
  ];

  itens = signal<LinhaParceiro[]>([]);
  total = signal(0);
  carregando = signal(false);
  ativo = signal<boolean | null>(null);
  clientes = signal<CampoOption<number>[]>([]);

  private query: QueryParams = { pageIndex: 0, pageSize: 10 };

  acoesLinha: AcaoLinha[] = [
    {
      id: 'inativar', label: 'Inativar', icone: 'bi bi-pause-circle', classe: 'btn-outline-secondary',
      visivel: (p: LinhaParceiro) => p.ativo && this.podeAtualizar(),
    },
    {
      id: 'ativar', label: 'Ativar', icone: 'bi bi-play-circle', classe: 'btn-outline-success',
      visivel: (p: LinhaParceiro) => !p.ativo && this.podeAtualizar(),
    },
  ];

  ngOnInit(): void {
    this.carregar();
    // A lista de clientes só existe para quem pode vê-la (operadora).
    if (this.atlasAuth.canViewEntity('cliente')) {
      this.clienteService.listar({ pageIndex: 0, pageSize: 1000 }, null).subscribe(page =>
        this.clientes.set(page.content.map(c => ({ value: c.id, label: c.situacao === 'Bloqueado' ? `${c.nome} (bloqueado)` : c.nome }))));
    }
  }

  carregar(): void {
    this.carregando.set(true);
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

  onNovo(): void {
    this.abrir(null, criarCamposParceiro(this.clientes()));
  }

  /** "Clonar": outra unidade do mesmo dono (mesmo cliente, cidade e fuso). */
  onCopiar(item: LinhaParceiro): void {
    const base = { ...item, nome: `${item.nome} (cópia)`, endereco: null, observacao: null };
    this.abrir(null, preencherCampos(criarCamposParceiro(this.clientes()), base));
  }

  onEditar(item: LinhaParceiro): void {
    this.service.getById(item.id).subscribe(parceiro => {
      const campos: FormItemConfig[] = [
        { type: 'section', label: 'Cadastro', children: preencherCampos(criarCamposParceiro(this.clientes()), parceiro) },
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
    if (ev.acaoId === 'inativar' || ev.acaoId === 'ativar') {
      const ativo = ev.acaoId === 'ativar';
      this.service.definirAtivo(ev.item.id, ativo, ev.item.version).subscribe(() => {
        this.toast.success(ativo ? 'Parceiro ativado.' : 'Parceiro inativado: sai da lista ao cadastrar telas. As TVs dele seguem como estão.');
        this.carregar();
      });
    }
  }

  onExcluir(p: LinhaParceiro): void {
    this.confirmModal.confirm({
      title: 'Excluir parceiro',
      message: p.quantidadeTelas > 0
        ? `"${p.nome}" tem ${p.quantidadeTelas} tela(s): mova ou exclua as telas antes, ou inative o parceiro.`
        : `Excluir "${p.nome}"?`,
      confirmText: 'Sim, excluir',
      cancelText: 'Cancelar',
      iconClass: 'bi bi-trash text-danger',
    }).subscribe(ok => {
      if (!ok) return;
      this.service.excluir(p.id).subscribe(() => {
        this.toast.success('Parceiro excluído.');
        this.carregar();
      });
    });
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
      permitirContinuar: false,
      mostrarBotaoSalvarECopiar: false,
      registro: parceiro,
      persistir: (valor, atual) => this.service.salvar(atual?.id ?? null, valor, atual?.version),
      msgSucesso: parceiro ? 'Parceiro salvo.' : 'Parceiro cadastrado. Agora cadastre a TV dele em Rede → Telas.',
    }).subscribe(res => { if (res?.recarregar || res?.acao === 'fechar' || res?.acao === 'copiar') this.carregar(); });
  }

  private podeAtualizar(): boolean {
    return this.atlasAuth.canUpdateEntity(ENTIDADE);
  }
}
