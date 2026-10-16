import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AcaoLinha, AtlasAuthService, ConfirmModalService, EventoFiltro, EventoPaginacao, FormItemConfig,
  FormModalService, ListagemDinamicaComponent, QueryParams, ToastService,
} from '@atlas/framework';
import { PlaylistService } from '../services/playlist.service';
import { LinhaPlaylist, PlaylistDto } from '../models/playlist.model';
import { PLAYLIST_COLUNAS, criarCamposPlaylist } from '../configs/playlist.config';
import { preencherCampos } from '../../../shared/utils/form.util';
import { TrocasPendentesComponent } from '../components/trocas-pendentes.component';

/** Nome físico da tabela — chave do RBAC no front. */
const ENTIDADE = 'playlist';

/**
 * Playlists da rede (operadora). Aqui só nome e descrição; a sequência de mídias e
 * os dias de cada uma ficam na página da grade (`/rede/playlist/:id/grade`).
 */
@Component({
  selector: 'app-playlist-list-page',
  standalone: true,
  imports: [ListagemDinamicaComponent, TrocasPendentesComponent],
  template: `
    <app-trocas-pendentes></app-trocas-pendentes>

    <atlas-listagem-dinamica
      titulo="Playlists"
      entityName="${ENTIDADE}"
      campoId="id"
      [autoInjetarId]="false"
      mensagemVazio="Nenhuma playlist. Crie uma, monte a grade e escolha em quais telas ela toca."
      [itens]="itens()"
      [totalElementos]="total()"
      [colunas]="colunas"
      [carregando]="carregando()"
      [acoesLinha]="acoesLinha"
      (criarNovo)="onNova()"
      (recarregar)="carregar()"
      (linhaClicada)="onGrade($event)"
      (editar)="onEditar($event)"
      (excluir)="onExcluir($event)"
      (clonar)="onDuplicar($event)"
      (acaoLinha)="onAcaoLinha($event)"
      (onPaginationChange)="onPaginacao($event)"
      (onFilterChange)="onFiltro($event)">
    </atlas-listagem-dinamica>
  `,
})
export class PlaylistListPageComponent implements OnInit {
  private service = inject(PlaylistService);
  private formModal = inject(FormModalService);
  private confirmModal = inject(ConfirmModalService);
  private toast = inject(ToastService);
  private atlasAuth = inject(AtlasAuthService);
  private router = inject(Router);

  readonly colunas = PLAYLIST_COLUNAS;

  itens = signal<LinhaPlaylist[]>([]);
  total = signal(0);
  carregando = signal(false);

  private query: QueryParams = { pageIndex: 0, pageSize: 10 };

  acoesLinha: AcaoLinha[] = [
    { id: 'grade', label: 'Montar grade', icone: 'bi bi-list-ol', classe: 'btn-outline-primary' },
    {
      id: 'inativar', label: 'Inativar', icone: 'bi bi-pause-circle', classe: 'btn-outline-warning',
      visivel: (p: LinhaPlaylist) => p.ativo && this.podeAtualizar(),
    },
    {
      id: 'ativar', label: 'Ativar', icone: 'bi bi-check-circle', classe: 'btn-outline-success',
      visivel: (p: LinhaPlaylist) => !p.ativo && this.podeAtualizar(),
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
      },
      error: () => this.carregando.set(false),
    });
  }

  onPaginacao(e: EventoPaginacao): void { this.query = { ...this.query, pageIndex: e.pageIndex, pageSize: e.pageSize }; this.carregar(); }
  onFiltro(e: EventoFiltro): void { this.query = { ...this.query, search: e.busca, pageIndex: 0 }; this.carregar(); }

  onNova(): void {
    this.abrir(null, criarCamposPlaylist());
  }

  onEditar(item: LinhaPlaylist): void {
    this.service.getById(item.id).subscribe(playlist => {
      const campos: FormItemConfig[] = [
        { type: 'section', label: 'Playlist', children: preencherCampos(criarCamposPlaylist(), playlist) },
        {
          type: 'section', label: 'Histórico', children: [
            { type: 'history', name: 'historico', label: '', col: 'col-12', entityName: ENTIDADE },
          ],
        },
      ];
      this.abrir(playlist, campos, true);
    });
  }

  /**
   * "Clonar" da grid: playlist nova com a mesma grade, para variar a partir
   * dela. A grid entrega a cópia sem `id`; o nome é único na rede e
   * localiza a original entre as linhas carregadas.
   */
  onDuplicar(copia: LinhaPlaylist): void {
    const item = this.itens().find(p => p.nome === copia.nome);
    if (!item) return;
    this.formModal.open({
      titulo: `Duplicar ${item.nome}`,
      entidade: 'Playlist',
      entityName: ENTIDADE,
      recordId: null,
      campos: preencherCampos(criarCamposPlaylist(), { nome: `${item.nome} (cópia)`, descricao: item.descricao }),
      largura: '640px',
      errosInline: true,
      permitirContinuar: false,
      mostrarBotaoSalvarECopiar: false,
      persistir: valor => this.service.duplicar(item.id, valor),
      msgSucesso: 'Playlist duplicada com a mesma grade.',
    }).subscribe(res => { if (res?.recarregar || res?.acao === 'fechar') this.carregar(); });
  }

  onGrade(item: LinhaPlaylist): void {
    this.router.navigate(['/rede/playlist', item.id, 'grade']);
  }

  onAcaoLinha(ev: { acaoId: string; item: LinhaPlaylist }): void {
    if (ev.acaoId === 'grade') this.onGrade(ev.item);
    if (ev.acaoId === 'ativar') this.definirAtivo(ev.item, true);
    if (ev.acaoId === 'inativar') this.definirAtivo(ev.item, false);
  }

  onExcluir(p: LinhaPlaylist): void {
    if (p.quantidadeTelas > 0) {
      this.toast.warning(`"${p.nome}" toca em ${p.quantidadeTelas} tela(s). Troque a playlist dessas telas antes.`);
      return;
    }
    this.confirmModal.confirm({
      title: 'Excluir playlist',
      message: `"${p.nome}" e a grade dela serão excluídas (as mídias continuam na biblioteca). Confirma?`,
      confirmText: 'Sim, excluir',
      cancelText: 'Cancelar',
      iconClass: 'bi bi-trash text-danger',
    }).subscribe(ok => {
      if (!ok) return;
      this.service.excluir(p.id).subscribe(() => {
        this.toast.success('Playlist excluída.');
        this.carregar();
      });
    });
  }

  private abrir(playlist: PlaylistDto | null, campos: FormItemConfig[], abas = false): void {
    this.formModal.open({
      titulo: playlist ? `Editar ${playlist.nome}` : 'Nova playlist',
      entidade: 'Playlist',
      entityName: ENTIDADE,
      recordId: playlist ? String(playlist.id) : null,
      campos,
      tabbedSections: abas,
      largura: '640px',
      errosInline: true,
      permitirContinuar: false,
      mostrarBotaoSalvarECopiar: false,
      registro: playlist,
      persistir: (valor, atual) => this.service.salvar(atual?.id ?? null, valor, atual?.version),
      msgSucesso: playlist ? 'Playlist salva.' : 'Playlist criada. Agora monte a grade.',
    }).subscribe(res => { if (res?.recarregar || res?.acao === 'fechar') this.carregar(); });
  }

  private definirAtivo(p: LinhaPlaylist, ativo: boolean): void {
    this.service.definirAtivo(p.id, ativo, p.version).subscribe(() => {
      this.toast.success(ativo ? 'Playlist ativada.' : 'Playlist inativada: as telas dela ficam sem grade.');
      this.carregar();
    });
  }

  private podeAtualizar(): boolean {
    return this.atlasAuth.canUpdateEntity(ENTIDADE);
  }
}
