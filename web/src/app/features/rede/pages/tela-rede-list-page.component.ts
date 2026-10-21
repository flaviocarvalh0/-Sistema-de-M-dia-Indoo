import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { interval } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { QRCodeComponent } from 'angularx-qrcode';
import {
  AcaoLinha, AtlasAuthService, CampoOption, ConfirmModalService, EventoFiltro, EventoPaginacao,
  FormItemConfig, FormModalService, ListagemDinamicaComponent, QueryParams, ToastService,
} from '@atlas/framework';
import { TelaRedeService } from '../services/tela-rede.service';
import { ParceiroService } from '../services/parceiro.service';
import { PlaylistService } from '../../playlists/services/playlist.service';
import { LinhaTela, TelaDto, UsoDiretorio } from '../../telas/models/tela.model';
import { TELA_REDE_COLUNAS, criarCamposTelaRede } from '../configs/tela-rede.config';
import { preencherCampos } from '../../../shared/utils/form.util';

/** Nome físico da tabela — chave do RBAC no front. */
const ENTIDADE = 'tela';

/** Código exibido em destaque depois de "Gerar código". */
interface CodigoExibido {
  tela: string;
  parceiro: string;
  codigo: string;
  expiraEm: number;
}

/**
 * Telas da rede (operadora, dona do hardware): cadastro, parceiro (o local
 * onde a TV fica) e playlist de cada TV, pareamento por código e a situação
 * da conexão. O cliente (anunciante) só acompanha, em Mídia Indoor → Onde
 * meus anúncios passam.
 */
@Component({
  selector: 'app-tela-rede-list-page',
  standalone: true,
  imports: [ListagemDinamicaComponent, DatePipe, FormsModule, QRCodeComponent, RouterLink],
  template: `
    @if (codigo(); as c) {
      <div class="card border-primary mb-3">
        <div class="card-body d-flex flex-wrap align-items-center gap-4">
          <div>
            <div class="small text-muted">Código de pareamento — {{ c.tela }} ({{ c.parceiro }})</div>
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

    @if (diretorio(); as t) {
      <div class="card mb-3" data-teste="diretorio-tela">
        <div class="card-header d-flex align-items-center">
          <strong class="me-auto">Diretório de anunciantes — {{ t.nome }} ({{ t.parceiroNome }})</strong>
          <button type="button" class="btn btn-sm btn-outline-secondary" title="Fechar" (click)="diretorio.set(null)">
            <i class="bi bi-x-lg"></i>
          </button>
        </div>
        <div class="card-body d-flex flex-wrap gap-4 align-items-start">
          @if (t.urlDiretorio; as url) {
            <qrcode [qrdata]="url" [width]="200" [margin]="2" errorCorrectionLevel="M"></qrcode>
          }
          <div class="flex-grow-1 small" style="min-width: 260px">
            @if (!t.diretorioAtivo) {
              <div class="alert alert-secondary py-2">Desligado: a TV não mostra o QR code e o endereço responde "Página indisponível". Ligue em <strong>Editar</strong>.</div>
            }
            @if (t.urlDiretorio; as url) {
              <div class="text-muted">Endereço público (é o que o QR code da TV abre):</div>
              <div class="font-monospace fs-6 mb-2 text-break" data-teste="url-diretorio">{{ url }}</div>
              <div class="d-flex flex-wrap gap-2 mb-3">
                <a class="btn btn-sm btn-primary" [href]="url" target="_blank" rel="noopener"><i class="bi bi-box-arrow-up-right me-1"></i>Abrir página</a>
                <button type="button" class="btn btn-sm btn-outline-primary" (click)="copiar(url)"><i class="bi bi-clipboard me-1"></i>Copiar endereço</button>
              </div>
            } @else {
              <div class="alert alert-warning py-2">
                A API está sem endereço público: sem ele a TV não mostra o QR code. Configure o parâmetro
                <code>INDOOR.URL_PUBLICA</code> (Atlas Studio → Parâmetros) com o endereço HTTPS da API na internet
                (<code>localhost</code> não vale: o celular não chega nele).
              </div>
            }
            @if (usoDiretorio(); as u) {
              <div class="border rounded p-2 mb-2 bg-body-tertiary" data-teste="uso-diretorio">
                <div class="fw-semibold mb-1">Últimos {{ u.dias }} dias</div>
                <div class="d-flex flex-wrap gap-3">
                  <span><strong>{{ u.acessos }}</strong> {{ u.acessos === 1 ? 'acesso' : 'acessos' }}
                    <span class="text-muted">({{ u.pessoas }} {{ u.pessoas === 1 ? 'pessoa' : 'pessoas' }} por dia, somadas)</span></span>
                  <span><strong>{{ u.toquesContatos }}</strong> {{ u.toquesContatos === 1 ? 'toque' : 'toques' }} em contatos</span>
                  <span><strong>{{ u.toquesAnuncie }}</strong> em "Quer anunciar aqui?"</span>
                </div>
                @if (u.porAnunciante.length) {
                  <div class="text-muted mt-1">
                    @for (a of u.porAnunciante; track a.clienteId; let ultimo = $last) {
                      {{ a.nome }}: {{ a.toques }}@if (!ultimo) { · }
                    }
                  </div>
                }
              </div>
            }
            <p class="text-muted mb-2">
              A página lista os anunciantes da grade de hoje desta TV que aceitaram aparecer (no perfil de cada um),
              com o estabelecimento primeiro. Mudanças aparecem em até 1 minuto.
            </p>
            @if (podeAtualizar()) {
              <button type="button" class="btn btn-sm btn-outline-danger" (click)="trocarEnderecoDiretorio(t)">
                <i class="bi bi-arrow-repeat me-1"></i>Trocar endereço
              </button>
            }
          </div>
        </div>
      </div>
    }

    <div class="d-flex flex-wrap align-items-center gap-2 mb-3">
      <select class="form-select form-select-sm w-auto" aria-label="Filtrar por parceiro"
              [ngModel]="parceiroId()" (ngModelChange)="filtrarParceiro($event)">
        <option [ngValue]="null">Todos os parceiros</option>
        @for (p of parceiros(); track p.value) {
          <option [ngValue]="p.value">{{ p.label }}</option>
        }
      </select>
      <div class="btn-group btn-group-sm" role="group" aria-label="Filtrar por situação">
        @for (f of filtros; track f.rotulo) {
          <button type="button" class="btn"
                  [class.btn-primary]="ativo() === f.valor"
                  [class.btn-outline-primary]="ativo() !== f.valor"
                  (click)="filtrarAtivo(f.valor)">{{ f.rotulo }}</button>
        }
      </div>
      <a routerLink="/rede/tela/versoes-app" class="btn btn-sm btn-outline-secondary ms-auto" data-teste="versoes-app">
        <i class="bi bi-phone-vibrate me-1"></i>Versões do app
      </a>
    </div>

    <atlas-listagem-dinamica
      titulo="Telas da rede"
      entityName="${ENTIDADE}"
      campoId="id"
      [autoInjetarId]="false"
      mensagemVazio="Nenhuma tela. Cadastre a TV instalada, escolha o estabelecimento e a playlist e gere o código para pareá-la."
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
export class TelaRedeListPageComponent implements OnInit {
  private service = inject(TelaRedeService);
  private parceiroService = inject(ParceiroService);
  private playlistService = inject(PlaylistService);
  private formModal = inject(FormModalService);
  private confirmModal = inject(ConfirmModalService);
  private toast = inject(ToastService);
  private atlasAuth = inject(AtlasAuthService);
  private destroyRef = inject(DestroyRef);

  readonly colunas = TELA_REDE_COLUNAS;
  readonly filtros: { rotulo: string; valor: boolean | null }[] = [
    { rotulo: 'Todas', valor: null },
    { rotulo: 'Ativas', valor: true },
    { rotulo: 'Inativas', valor: false },
  ];

  itens = signal<LinhaTela[]>([]);
  total = signal(0);
  carregando = signal(false);
  ativo = signal<boolean | null>(null);
  parceiroId = signal<number | null>(null);
  parceiros = signal<CampoOption<number>[]>([]);
  playlists = signal<CampoOption<number>[]>([]);
  codigo = signal<CodigoExibido | null>(null);
  diretorio = signal<TelaDto | null>(null);
  usoDiretorio = signal<UsoDiretorio | null>(null);
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
      id: 'diretorio', label: 'Diretório de anunciantes', icone: 'bi bi-people', classe: 'btn-outline-secondary',
      visivel: (t: LinhaTela) => !!t.codigoDiretorio,
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
    this.parceiroService.listar({ pageIndex: 0, pageSize: 1000 }, null).subscribe(page =>
      this.parceiros.set(page.content.map(p => ({ value: p.id, label: p.ativo ? p.nome : `${p.nome} (inativo)` }))));
    this.playlistService.listarTodas().subscribe(lista =>
      this.playlists.set(lista.map(p => ({ value: p.id, label: p.ativo ? p.nome : `${p.nome} (inativa)` }))));
    this.carregar();
    interval(1000).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.agora.set(Date.now()));
    // A conexão (online/offline) muda sozinha: atualiza a lista a cada minuto.
    interval(60_000).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.carregar(false));
  }

  carregar(mostrarCarregando = true): void {
    if (mostrarCarregando) this.carregando.set(true);
    this.service.listar(this.query, { parceiroId: this.parceiroId(), ativo: this.ativo() }).subscribe({
      next: page => {
        this.itens.set(page.content);
        this.total.set(page.totalElements);
        this.carregando.set(false);
      },
      error: () => this.carregando.set(false),
    });
  }

  filtrarParceiro(valor: number | null): void {
    this.parceiroId.set(valor);
    this.query = { ...this.query, pageIndex: 0 };
    this.carregar();
  }

  filtrarAtivo(valor: boolean | null): void {
    this.ativo.set(valor);
    this.query = { ...this.query, pageIndex: 0 };
    this.carregar();
  }

  onPaginacao(e: EventoPaginacao): void { this.query = { ...this.query, pageIndex: e.pageIndex, pageSize: e.pageSize }; this.carregar(); }
  onFiltro(e: EventoFiltro): void { this.query = { ...this.query, search: e.busca, pageIndex: 0 }; this.carregar(); }

  onNova(): void {
    this.abrir(null, this.parceiroId() ? { parceiroId: this.parceiroId()! } : undefined);
  }

  onEditar(item: LinhaTela): void {
    this.service.getById(item.id).subscribe(tela => this.abrir(tela));
  }

  /** "Clonar": outra tela no mesmo parceiro e local (cada TV pareia com o próprio código). */
  onCopiar(item: LinhaTela): void {
    this.abrir(null, { parceiroId: item.parceiroId, nome: `${item.nome} (cópia)`, localizacao: item.localizacao });
  }

  onAcaoLinha(ev: { acaoId: string; item: LinhaTela }): void {
    if (ev.acaoId === 'codigo') this.gerarCodigo(ev.item);
    if (ev.acaoId === 'desvincular') this.desvincular(ev.item);
    if (ev.acaoId === 'diretorio') {
      this.usoDiretorio.set(null);
      this.service.getById(ev.item.id).subscribe(t => this.diretorio.set(t));
      // Sem permissão ou falha: o quadro de uso só não aparece.
      this.service.usoDiretorio(ev.item.id).subscribe({ next: u => this.usoDiretorio.set(u), error: () => this.usoDiretorio.set(null) });
    }
    if (ev.acaoId === 'ativar') this.definirAtivo(ev.item, true);
    if (ev.acaoId === 'inativar') this.definirAtivo(ev.item, false);
  }

  onExcluir(t: LinhaTela): void {
    this.confirmModal.confirm({
      title: 'Excluir tela',
      message: t.situacao === 'AguardandoPareamento'
        ? `Excluir "${t.nome}" (${t.parceiroNome})?`
        : `"${t.nome}" (${t.parceiroNome}) tem uma TV pareada, que perde o acesso e para de receber grade. Confirma?`,
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

  copiar(texto: string): void {
    navigator.clipboard?.writeText(texto).then(
      () => this.toast.success('Endereço copiado.'),
      () => this.toast.error('Não foi possível copiar. Selecione o endereço e copie.'),
    );
  }

  trocarEnderecoDiretorio(t: TelaDto): void {
    this.confirmModal.confirm({
      title: 'Trocar endereço do diretório',
      message: `O endereço atual de "${t.nome}" para de funcionar (inclusive QR codes impressos ou fotografados). `
        + 'A TV mostra o QR code novo na próxima sincronização. Confirma?',
      confirmText: 'Trocar endereço',
      cancelText: 'Cancelar',
      iconClass: 'bi bi-arrow-repeat text-danger',
    }).subscribe(ok => {
      if (!ok) return;
      this.service.trocarCodigoDiretorio(t.id, t.version).subscribe(() => {
        this.toast.success('Endereço trocado.');
        this.service.getById(t.id).subscribe(atual => this.diretorio.set(atual));
        this.carregar(false);
      });
    });
  }

  fecharCodigo(): void {
    this.codigo.set(null);
    this.carregar();
  }

  private abrir(tela: TelaDto | null, modelo?: Partial<TelaDto>): void {
    const base = criarCamposTelaRede(this.parceiros(), this.playlists());
    const dados = tela ?? modelo;
    const campos: FormItemConfig[] = tela
      ? [
        { type: 'section', label: 'Tela', children: preencherCampos(base, tela) },
        {
          type: 'section', label: 'Histórico', children: [
            { type: 'history', name: 'historico', label: '', col: 'col-12', entityName: ENTIDADE },
          ],
        },
      ]
      : dados ? preencherCampos(base, dados) : base;

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
    }).subscribe(res => {
      if (res?.recarregar || res?.acao === 'fechar') this.carregar();
      // O cartão do diretório aberto acompanha a edição (ligar/desligar).
      const aberto = this.diretorio();
      if (aberto && res?.recarregar) this.service.getById(aberto.id).subscribe(atual => this.diretorio.set(atual));
    });
  }

  private gerarCodigo(t: LinhaTela): void {
    const pedir = () => this.service.gerarCodigo(t.id, t.version).subscribe(r => {
      this.codigo.set({ tela: t.nome, parceiro: t.parceiroNome, codigo: r.codigo, expiraEm: new Date(r.expiraEm).getTime() });
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

  podeAtualizar(): boolean {
    return this.atlasAuth.canUpdateEntity(ENTIDADE);
  }
}
