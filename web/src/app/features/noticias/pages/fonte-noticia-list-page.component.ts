import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import {
  AcaoLinha, AtlasAuthService, ConfirmModalService, EventoFiltro, EventoPaginacao, FormItemConfig,
  FormModalService, ListagemDinamicaComponent, QueryParams, ToastService,
} from '@atlas/framework';
import { FonteNoticiaService } from '../services/fonte-noticia.service';
import { FonteNoticiaDto, LinhaFonteNoticia, NoticiaDto } from '../models/fonte-noticia.model';
import { FONTE_NOTICIA_COLUNAS, criarCamposFonteNoticia } from '../configs/fonte-noticia.config';
import { preencherCampos } from '../../../shared/utils/form.util';

/** Nome físico da tabela — chave do RBAC no front. */
const ENTIDADE = 'fonte_noticia';

/**
 * Feeds de notícias da rede (operadora). A API lê cada fonte ativa a cada
 * hora e guarda as notícias recentes com a imagem; as playlists decidem se
 * intercalam notícias entre os anúncios.
 */
@Component({
  selector: 'app-fonte-noticia-list-page',
  standalone: true,
  imports: [ListagemDinamicaComponent, DatePipe],
  styles: [`
    .miniatura { width: 96px; height: 54px; object-fit: cover; border-radius: 4px; background: #e9ecef; }
  `],
  template: `
    <div class="alert alert-light border small py-2 mb-3">
      <i class="bi bi-info-circle me-1"></i>
      As notícias entram nas TVs das playlists com "Uma notícia a cada N anúncios", com o crédito da fonte.
      Notícias com mais de 48 h saem das TVs.
    </div>

    <atlas-listagem-dinamica
      titulo="Fontes de notícias"
      entityName="${ENTIDADE}"
      campoId="id"
      [autoInjetarId]="false"
      mensagemVazio="Nenhuma fonte. Cadastre um feed RSS com licença para exibição pública (ex.: Agência Brasil)."
      [itens]="itens()"
      [totalElementos]="total()"
      [colunas]="colunas"
      [carregando]="carregando()"
      [acoesLinha]="acoesLinha"
      (criarNovo)="onNova()"
      (recarregar)="carregar()"
      (linhaClicada)="verNoticias($event)"
      (editar)="onEditar($event)"
      (excluir)="onExcluir($event)"
      (acaoLinha)="onAcaoLinha($event)"
      (onPaginationChange)="onPaginacao($event)"
      (onFilterChange)="onFiltro($event)">
    </atlas-listagem-dinamica>

    @if (selecionada(); as f) {
      <div class="card mt-3" data-teste="noticias">
        <div class="card-header d-flex align-items-center gap-2">
          <strong class="me-auto">Notícias de {{ f.nome }}</strong>
          <button type="button" class="btn btn-sm btn-outline-secondary" (click)="selecionada.set(null)" aria-label="Fechar">
            <i class="bi bi-x-lg"></i>
          </button>
        </div>
        @if (f.ultimoErro) {
          <div class="alert alert-danger small m-3 mb-0 py-2">
            <i class="bi bi-exclamation-triangle me-1"></i>Última leitura falhou: {{ f.ultimoErro }}
          </div>
        }
        <ul class="list-group list-group-flush">
          @for (n of noticias(); track n.id) {
            <li class="list-group-item d-flex align-items-center gap-3">
              @if (n.urlImagem) {
                <img class="miniatura" [src]="n.urlImagem" alt="">
              } @else {
                <div class="miniatura d-flex align-items-center justify-content-center text-muted"><i class="bi bi-newspaper"></i></div>
              }
              <div class="flex-grow-1">
                <div [class.text-muted]="!n.vigente">{{ n.titulo }}</div>
                <div class="small text-muted">
                  {{ n.publicadaEm | date: 'dd/MM/yyyy HH:mm' }}
                  @if (!n.vigente) { · <span class="badge text-bg-light border">fora das TVs (mais de 48 h)</span> }
                </div>
              </div>
            </li>
          } @empty {
            <li class="list-group-item text-muted">Nenhuma notícia guardada ainda. Use "Coletar agora".</li>
          }
        </ul>
      </div>
    }
  `,
})
export class FonteNoticiaListPageComponent implements OnInit {
  private service = inject(FonteNoticiaService);
  private formModal = inject(FormModalService);
  private confirmModal = inject(ConfirmModalService);
  private toast = inject(ToastService);
  private atlasAuth = inject(AtlasAuthService);

  readonly colunas = FONTE_NOTICIA_COLUNAS;

  itens = signal<LinhaFonteNoticia[]>([]);
  total = signal(0);
  carregando = signal(false);
  selecionada = signal<LinhaFonteNoticia | null>(null);
  noticias = signal<NoticiaDto[]>([]);

  private query: QueryParams = { pageIndex: 0, pageSize: 10 };

  acoesLinha: AcaoLinha[] = [
    { id: 'noticias', label: 'Ver notícias', icone: 'bi bi-newspaper', classe: 'btn-outline-secondary' },
    {
      id: 'coletar', label: 'Coletar agora', icone: 'bi bi-arrow-repeat', classe: 'btn-outline-primary',
      visivel: () => this.atlasAuth.canUpdateEntity(ENTIDADE),
    },
  ];

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.carregando.set(true);
    this.service.listar(this.query).subscribe({
      next: page => {
        this.itens.set(page.content);
        this.total.set(page.totalElements);
        this.carregando.set(false);
        // Mantém o painel de notícias em dia com a linha recarregada.
        const atual = this.selecionada();
        if (atual) this.selecionada.set(page.content.find(f => f.id === atual.id) ?? atual);
      },
      error: () => this.carregando.set(false),
    });
  }

  onPaginacao(e: EventoPaginacao): void { this.query = { ...this.query, pageIndex: e.pageIndex, pageSize: e.pageSize }; this.carregar(); }
  onFiltro(e: EventoFiltro): void { this.query = { ...this.query, search: e.busca, pageIndex: 0 }; this.carregar(); }

  onNova(): void {
    this.abrir(null, criarCamposFonteNoticia());
  }

  onEditar(item: LinhaFonteNoticia): void {
    this.service.getById(item.id).subscribe(fonte => {
      const campos: FormItemConfig[] = [
        { type: 'section', label: 'Fonte', children: preencherCampos(criarCamposFonteNoticia(), fonte) },
        {
          type: 'section', label: 'Histórico', children: [
            { type: 'history', name: 'historico', label: '', col: 'col-12', entityName: ENTIDADE },
          ],
        },
      ];
      this.abrir(fonte, campos, true);
    });
  }

  onAcaoLinha(ev: { acaoId: string; item: LinhaFonteNoticia }): void {
    if (ev.acaoId === 'noticias') this.verNoticias(ev.item);
    if (ev.acaoId === 'coletar') this.coletar(ev.item);
  }

  verNoticias(f: LinhaFonteNoticia): void {
    this.selecionada.set(f);
    this.noticias.set([]);
    this.service.noticias(f.id).subscribe(n => this.noticias.set(n));
  }

  onExcluir(f: LinhaFonteNoticia): void {
    this.confirmModal.confirm({
      title: 'Excluir fonte',
      message: `"${f.nome}" e as ${f.quantidadeNoticias} notícia(s) guardadas saem da rede (as TVs deixam de exibi-las na próxima sincronização). Confirma?`,
      confirmText: 'Sim, excluir',
      cancelText: 'Cancelar',
      iconClass: 'bi bi-trash text-danger',
    }).subscribe(ok => {
      if (!ok) return;
      this.service.excluir(f.id).subscribe(() => {
        this.toast.success('Fonte excluída.');
        if (this.selecionada()?.id === f.id) this.selecionada.set(null);
        this.carregar();
      });
    });
  }

  private coletar(f: LinhaFonteNoticia): void {
    this.service.coletar(f.id).subscribe(r => {
      if (r.erro) this.toast.warning(`Não foi possível ler o feed: ${r.erro}`);
      else this.toast.success(r.novas ? `${r.novas} notícia(s) nova(s).` : 'Feed lido: nenhuma notícia nova.');
      this.carregar();
      this.verNoticias(f);
    });
  }

  private abrir(fonte: FonteNoticiaDto | null, campos: FormItemConfig[], abas = false): void {
    this.formModal.open({
      titulo: fonte ? `Editar ${fonte.nome}` : 'Nova fonte de notícias',
      entidade: 'Fonte de notícias',
      entityName: ENTIDADE,
      recordId: fonte ? String(fonte.id) : null,
      campos,
      tabbedSections: abas,
      largura: '640px',
      errosInline: true,
      permitirContinuar: false,
      mostrarBotaoSalvarECopiar: false,
      registro: fonte,
      persistir: (valor, atual) => this.service.salvar(atual?.id ?? null, valor, atual?.version),
      msgSucesso: fonte ? 'Fonte salva.' : 'Fonte cadastrada. Use "Coletar agora" para conferir o feed.',
    }).subscribe(res => { if (res?.recarregar || res?.acao === 'fechar') this.carregar(); });
  }
}
