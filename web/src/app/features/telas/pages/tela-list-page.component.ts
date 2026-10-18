import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { interval } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EventoFiltro, EventoPaginacao, ListagemDinamicaComponent, QueryParams } from '@atlas/framework';
import { TelaService } from '../services/tela.service';
import { LinhaTela } from '../models/tela.model';
import { TELA_COLUNAS } from '../configs/tela.config';

/** Nome físico da tabela — chave do RBAC no front. */
const ENTIDADE = 'tela';

/**
 * "Onde meus anúncios passam": somente leitura. Lista as TVs ativas cuja
 * playlist tem pelo menos uma mídia ativa do cliente logado, com a conexão
 * de cada uma. Telas e grades são da operadora (Rede).
 */
@Component({
  selector: 'app-tela-list-page',
  standalone: true,
  imports: [ListagemDinamicaComponent],
  template: `
    <div class="alert alert-light border small py-2 mb-3">
      <i class="bi bi-info-circle me-1"></i>
      As TVs em que pelo menos uma mídia ativa sua está na programação. A operadora monta as grades;
      mídias inativas não aparecem aqui.
    </div>

    <atlas-listagem-dinamica
      titulo="Onde meus anúncios passam"
      entityName="${ENTIDADE}"
      campoId="id"
      [autoInjetarId]="false"
      mensagemVazio="Nenhuma TV exibe suas mídias ainda. Ative uma mídia e fale com a operadora para entrar na programação."
      [itens]="itens()"
      [totalElementos]="total()"
      [colunas]="colunas"
      [carregando]="carregando()"
      [mostrarNovo]="false"
      [mostrarEditar]="false"
      [mostrarExcluir]="false"
      (recarregar)="carregar()"
      (onPaginationChange)="onPaginacao($event)"
      (onFilterChange)="onFiltro($event)">
    </atlas-listagem-dinamica>
  `,
})
export class TelaListPageComponent implements OnInit {
  private service = inject(TelaService);
  private destroyRef = inject(DestroyRef);

  readonly colunas = TELA_COLUNAS;

  itens = signal<LinhaTela[]>([]);
  total = signal(0);
  carregando = signal(false);

  private query: QueryParams = { pageIndex: 0, pageSize: 10 };

  ngOnInit(): void {
    this.carregar();
    // A conexão (online/offline) muda sozinha: atualiza a lista a cada minuto.
    interval(60_000).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.carregar(false));
  }

  carregar(mostrarCarregando = true): void {
    if (mostrarCarregando) this.carregando.set(true);
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
}
