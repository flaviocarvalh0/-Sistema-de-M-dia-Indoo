import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AcaoLinha, AtlasAuthService, ConfirmModalService, EventoFiltro, EventoPaginacao, FormItemConfig,
  FormModalService, ListagemDinamicaComponent, QueryParams, ToastService,
} from '@atlas/framework';
import { ClienteService } from '../services/cliente.service';
import { LinhaCliente, ClienteDto, SituacaoCliente } from '../models/cliente.model';
import { CLIENTE_COLUNAS, criarCamposBloqueio, criarCamposCliente } from '../configs/cliente.config';
import { preencherCampos } from '../../../shared/utils/form.util';
import { UsoArmazenamentoRedeComponent } from '../components/uso-armazenamento-rede.component';

/** Nome físico da tabela — chave do RBAC no front. */
const ENTIDADE = 'cliente';

/**
 * Clientes da rede (operadora). Cadastrar cria também o usuário do
 * responsável, que recebe a senha temporária por e-mail.
 */
@Component({
  selector: 'app-cliente-list-page',
  standalone: true,
  imports: [ListagemDinamicaComponent, UsoArmazenamentoRedeComponent],
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
          <i class="bi bi-plus-lg me-1"></i>Novo cliente
        </button>
      }
    </div>

    <app-uso-armazenamento-rede />

    <atlas-listagem-dinamica
      titulo="Clientes"
      entityName="${ENTIDADE}"
      campoId="id"
      [autoInjetarId]="false"
      mensagemVazio="Nenhum cliente. Cadastre quem vai anunciar nas TVs: ele recebe o acesso ao painel por e-mail."
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
export class ClienteListPageComponent implements OnInit {
  private service = inject(ClienteService);
  private formModal = inject(FormModalService);
  private confirmModal = inject(ConfirmModalService);
  private toast = inject(ToastService);
  private atlasAuth = inject(AtlasAuthService);
  private router = inject(Router);

  readonly colunas = CLIENTE_COLUNAS;
  /**
   * O "Novo" da grid fica desligado porque ligá-lo liga também o "Clonar": o
   * e-mail do responsável é único, então copiar cliente não tem uso.
   */
  readonly podeCriar = this.atlasAuth.canCreateEntity(ENTIDADE);
  readonly filtros: { rotulo: string; valor: SituacaoCliente | null }[] = [
    { rotulo: 'Todos', valor: null },
    { rotulo: 'Ativos', valor: 'Ativo' },
    { rotulo: 'Bloqueados', valor: 'Bloqueado' },
  ];

  itens = signal<LinhaCliente[]>([]);
  total = signal(0);
  carregando = signal(false);
  situacao = signal<SituacaoCliente | null>(null);

  private query: QueryParams = { pageIndex: 0, pageSize: 10 };

  acoesLinha: AcaoLinha[] = [
    { id: 'relatorio', label: 'Relatório de veiculação', icone: 'bi bi-graph-up', classe: 'btn-outline-primary' },
    {
      id: 'bloquear', label: 'Bloquear', icone: 'bi bi-lock', classe: 'btn-outline-danger',
      visivel: (p: LinhaCliente) => p.situacao !== 'Bloqueado' && this.podeAtualizar(),
    },
    {
      id: 'desbloquear', label: 'Desbloquear', icone: 'bi bi-unlock', classe: 'btn-outline-success',
      visivel: (p: LinhaCliente) => p.situacao === 'Bloqueado' && this.podeAtualizar(),
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

  filtrarSituacao(valor: SituacaoCliente | null): void {
    this.situacao.set(valor);
    this.query = { ...this.query, pageIndex: 0 };
    this.carregar();
  }

  onPaginacao(e: EventoPaginacao): void { this.query = { ...this.query, pageIndex: e.pageIndex, pageSize: e.pageSize }; this.carregar(); }
  onFiltro(e: EventoFiltro): void { this.query = { ...this.query, search: e.busca, pageIndex: 0 }; this.carregar(); }

  onNovo(): void {
    this.abrir(null, criarCamposCliente(false));
  }

  onEditar(item: LinhaCliente): void {
    this.service.getById(item.id).subscribe(cliente => {
      const campos: FormItemConfig[] = [
        { type: 'section', label: 'Cadastro', children: preencherCampos(criarCamposCliente(true), cliente) },
        {
          type: 'section', label: 'Histórico', children: [
            { type: 'history', name: 'historico', label: '', col: 'col-12', entityName: ENTIDADE },
          ],
        },
      ];
      this.abrir(cliente, campos, true);
    });
  }

  onAcaoLinha(ev: { acaoId: string; item: LinhaCliente }): void {
    if (ev.acaoId === 'relatorio') this.router.navigate(['/rede/cliente', ev.item.id, 'relatorio']);
    if (ev.acaoId === 'bloquear') this.bloquear(ev.item);
    if (ev.acaoId === 'desbloquear') this.desbloquear(ev.item);
  }

  private abrir(cliente: ClienteDto | null, campos: FormItemConfig[], abas = false): void {
    this.formModal.open({
      titulo: cliente ? `Editar ${cliente.nome}` : 'Novo cliente',
      entidade: 'Cliente',
      entityName: ENTIDADE,
      recordId: cliente ? String(cliente.id) : null,
      campos,
      tabbedSections: abas,
      largura: '760px',
      errosInline: true,
      // Só "Salvar e fechar": a API devolve id/versão, não o registro.
      permitirContinuar: false,
      mostrarBotaoSalvarECopiar: false,
      registro: cliente,
      persistir: (valor, atual) => this.service.salvar(atual?.id ?? null, valor, atual?.version),
      msgSucesso: cliente
        ? 'Cliente salvo.'
        : 'Cliente cadastrado. O responsável recebe a senha temporária por e-mail.',
    }).subscribe(res => { if (res?.recarregar || res?.acao === 'fechar' || res?.acao === 'copiar') this.carregar(); });
  }

  private bloquear(p: LinhaCliente): void {
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
      msgSucesso: 'Cliente bloqueado.',
    }).subscribe(res => { if (res?.recarregar || res?.acao === 'fechar') this.carregar(); });
  }

  private desbloquear(p: LinhaCliente): void {
    this.confirmModal.confirm({
      title: 'Desbloquear cliente',
      message: `${p.nome} volta a ter acesso ao painel e as mídias dele voltam às grades. Confirma?`,
      confirmText: 'Sim, desbloquear',
      cancelText: 'Cancelar',
      iconClass: 'bi bi-unlock text-success',
    }).subscribe(ok => {
      if (!ok) return;
      this.service.desbloquear(p.id, p.version).subscribe(() => {
        this.toast.success('Cliente desbloqueado.');
        this.carregar();
      });
    });
  }

  private podeAtualizar(): boolean {
    return this.atlasAuth.canUpdateEntity(ENTIDADE);
  }
}
