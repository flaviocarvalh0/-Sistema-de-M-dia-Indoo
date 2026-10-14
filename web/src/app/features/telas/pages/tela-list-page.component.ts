import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { interval } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AcaoLinha, AtlasAuthService, EventoFiltro, EventoPaginacao, FormModalService,
  ListagemDinamicaComponent, QueryParams,
} from '@atlas/framework';
import { TelaService } from '../services/tela.service';
import { LinhaTela } from '../models/tela.model';
import { TELA_COLUNAS, criarCampoPlaylist } from '../configs/tela.config';
import { PlaylistService } from '../../playlists/services/playlist.service';
import { preencherCampos } from '../../../shared/utils/form.util';

/** Nome físico da tabela — chave do RBAC no front. */
const ENTIDADE = 'tela';

/**
 * As TVs do parceiro, somente leitura. O hardware é da operadora (cadastro,
 * pareamento, desvínculo ficam em Rede → Telas); aqui o parceiro acompanha a
 * conexão e escolhe a playlist de cada TV.
 */
@Component({
  selector: 'app-tela-list-page',
  standalone: true,
  imports: [ListagemDinamicaComponent],
  template: `
    <div class="alert alert-light border small py-2 mb-3">
      <i class="bi bi-info-circle me-1"></i>
      As TVs são instaladas e pareadas pela operadora. Aqui você escolhe o que cada uma toca.
    </div>

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
      mensagemVazio="Nenhuma TV liberada para você ainda. Fale com a operadora."
      [itens]="itens()"
      [totalElementos]="total()"
      [colunas]="colunas"
      [carregando]="carregando()"
      [mostrarNovo]="false"
      [mostrarEditar]="false"
      [mostrarExcluir]="false"
      [acoesLinha]="acoesLinha"
      (recarregar)="carregar()"
      (linhaClicada)="onLinha($event)"
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
  private atlasAuth = inject(AtlasAuthService);
  private destroyRef = inject(DestroyRef);

  readonly colunas = TELA_COLUNAS;
  readonly filtros: { rotulo: string; valor: boolean | null }[] = [
    { rotulo: 'Todas', valor: null },
    { rotulo: 'Ativas', valor: true },
    { rotulo: 'Inativas', valor: false },
  ];

  /** Trocar a playlist é gestão de conteúdo: segue a permissão de alterar playlists. */
  readonly podeTrocarPlaylist = this.atlasAuth.canUpdateEntity('playlist');

  itens = signal<LinhaTela[]>([]);
  total = signal(0);
  carregando = signal(false);
  ativo = signal<boolean | null>(null);

  private query: QueryParams = { pageIndex: 0, pageSize: 10 };

  acoesLinha: AcaoLinha[] = [
    {
      id: 'playlist', label: 'Trocar playlist', icone: 'bi bi-collection-play', classe: 'btn-outline-primary',
      visivel: () => this.podeTrocarPlaylist,
    },
  ];

  ngOnInit(): void {
    this.carregar();
    // A conexão (online/offline) muda sozinha: atualiza a lista a cada minuto.
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

  onLinha(item: LinhaTela): void {
    if (this.podeTrocarPlaylist) this.trocarPlaylist(item);
  }

  onAcaoLinha(ev: { acaoId: string; item: LinhaTela }): void {
    if (ev.acaoId === 'playlist') this.trocarPlaylist(ev.item);
  }

  private trocarPlaylist(tela: LinhaTela): void {
    this.playlistService.listarTodas().subscribe(playlists => {
      const opcoes = playlists.map(p => ({ value: p.id, label: p.ativo ? p.nome : `${p.nome} (inativa)` }));
      this.formModal.open({
        titulo: `Playlist de ${tela.nome}`,
        campos: preencherCampos(criarCampoPlaylist(opcoes), tela),
        largura: '560px',
        errosInline: true,
        registroIdExibicao: tela.id,
        permitirContinuar: false,
        mostrarBotaoSalvarECopiar: false,
        registro: tela,
        persistir: valor => this.service.definirPlaylist(tela.id, valor.playlistId ? Number(valor.playlistId) : null, tela.version),
        msgSucesso: 'Playlist trocada. A TV muda na próxima sincronização.',
      }).subscribe(res => { if (res?.recarregar || res?.acao === 'fechar') this.carregar(); });
    });
  }
}
