import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { QRCodeComponent } from 'angularx-qrcode';
import {
  AcaoLinha, AtlasAuthService, ConfirmModalService, EventoFiltro, EventoPaginacao, FormItemConfig,
  FormModalService, ListagemDinamicaComponent, QueryParams, ToastService,
} from '@atlas/framework';
import { MidiaService } from '../services/midia.service';
import { LinhaMidia, MidiaDto, TipoMidia, UsoArmazenamento } from '../models/midia.model';
import { MIDIA_COLUNAS, TIPO_QR_ROTULO, camposQrDaMidia, criarCamposMidia, criarCamposQrCode } from '../configs/midia.config';
import { preencherCampos } from '../../../shared/utils/form.util';
import { formatarBytes } from '../../../shared/models/indoor.util';

/** Nome físico da tabela — chave do RBAC no front. */
const ENTIDADE = 'midia';

/**
 * Biblioteca de vídeos e imagens do parceiro. O envio e a troca de arquivo
 * têm página própria (`/indoor/midia/enviar`); aqui se edita título, duração
 * e período, o QR code exibido por cima da mídia, ativa/inativa (dentro do
 * limite do plano) e exclui (só mídia fora de playlists). As grades são
 * montadas pela operadora.
 */
@Component({
  selector: 'app-midia-list-page',
  standalone: true,
  imports: [ListagemDinamicaComponent, QRCodeComponent],
  template: `
    @if (uso(); as u) {
      <div class="card mb-3">
        <div class="card-body py-2">
          <div class="d-flex justify-content-between small mb-1">
            <span><i class="bi bi-hdd me-1"></i>Espaço usado</span>
            <span>{{ usoTexto() }}</span>
          </div>
          <div class="progress" role="progressbar" style="height: 8px"
               [attr.aria-valuenow]="percentualUso()" aria-valuemin="0" aria-valuemax="100">
            <div class="progress-bar" [class.bg-danger]="percentualUso() >= 90" [style.width.%]="percentualUso()"></div>
          </div>
          <div class="d-flex flex-wrap justify-content-between gap-2 small mt-2" data-teste="plano">
            <span>
              <i class="bi bi-collection-play me-1"></i>Mídias ativas: <strong>{{ u.midiasAtivas }} de {{ u.limiteMidiasAtivas }}</strong>
              @if (u.midiasAtivas >= u.limiteMidiasAtivas) { <span class="badge text-bg-warning ms-1">plano completo</span> }
            </span>
            @if (u.midiasAtivas >= u.limiteMidiasAtivas) {
              <span class="text-muted">Para trocar o anúncio, use "Substituir arquivo" ou inative uma mídia antes de ativar outra.</span>
            }
          </div>
        </div>
      </div>
    }

    <div class="d-flex flex-wrap align-items-center gap-2 mb-3">
      <div class="btn-group btn-group-sm" role="group" aria-label="Filtrar por tipo">
        @for (f of filtrosTipo; track f.rotulo) {
          <button type="button" class="btn"
                  [class.btn-primary]="tipo() === f.valor"
                  [class.btn-outline-primary]="tipo() !== f.valor"
                  (click)="filtrarTipo(f.valor)">{{ f.rotulo }}</button>
        }
      </div>
      <div class="btn-group btn-group-sm" role="group" aria-label="Filtrar por situação">
        @for (f of filtrosAtivo; track f.rotulo) {
          <button type="button" class="btn"
                  [class.btn-primary]="ativo() === f.valor"
                  [class.btn-outline-primary]="ativo() !== f.valor"
                  (click)="filtrarAtivo(f.valor)">{{ f.rotulo }}</button>
        }
      </div>
      @if (podeCriar) {
        <button type="button" class="btn btn-sm btn-primary ms-auto" (click)="onEnviar()">
          <i class="bi bi-cloud-arrow-up me-1"></i>Enviar mídia
        </button>
      }
    </div>

    <atlas-listagem-dinamica
      titulo="Mídias"
      entityName="${ENTIDADE}"
      campoId="id"
      [autoInjetarId]="false"
      mensagemVazio="Nenhuma mídia. Envie um vídeo ou imagem; a operadora inclui nas grades das TVs."
      [itens]="itens()"
      [totalElementos]="total()"
      [colunas]="colunas"
      [carregando]="carregando()"
      [acoesLinha]="acoesLinha"
      [mostrarNovo]="false"
      (recarregar)="carregar()"
      (linhaClicada)="onEditar($event)"
      (editar)="onEditar($event)"
      (excluir)="onExcluir($event)"
      (acaoLinha)="onAcaoLinha($event)"
      (onPaginationChange)="onPaginacao($event)"
      (onFilterChange)="onFiltro($event)">
    </atlas-listagem-dinamica>

    @if (qrAberto(); as m) {
      <div class="card mt-3" data-teste="previa-qr" style="max-width: 760px">
        <div class="card-header d-flex align-items-center gap-2">
          <strong class="me-auto">QR code de {{ m.titulo }}</strong>
          <button type="button" class="btn btn-sm btn-outline-secondary" (click)="qrAberto.set(null)" aria-label="Fechar">
            <i class="bi bi-x-lg"></i>
          </button>
        </div>
        <div class="card-body d-flex flex-wrap gap-4 align-items-center">
          <div class="text-center">
            <qrcode [qrdata]="m.qrConteudo!" [width]="220" [margin]="2" errorCorrectionLevel="M" />
            <div class="small fw-semibold mt-1">{{ m.qrLegenda || legendaPadrao(m) }}</div>
          </div>
          <div class="small flex-grow-1" style="min-width: 240px">
            <div><span class="text-muted">Tipo:</span> {{ rotuloQr(m) }}</div>
            <div class="text-break"><span class="text-muted">Abre:</span> {{ m.qrConteudo }}</div>
            <div class="mt-2 text-muted">
              Aponte a câmera do celular para conferir antes de ir para as TVs. Na TV ele aparece no
              {{ posicaoTexto(m) }}, por cima da mídia.
            </div>
          </div>
        </div>
      </div>
    }
  `,
})
export class MidiaListPageComponent implements OnInit {
  private service = inject(MidiaService);
  private formModal = inject(FormModalService);
  private confirmModal = inject(ConfirmModalService);
  private toast = inject(ToastService);
  private atlasAuth = inject(AtlasAuthService);
  private router = inject(Router);

  readonly colunas = MIDIA_COLUNAS;
  /**
   * O "Novo" da grid fica desligado porque ligá-lo liga também o "Clonar", que
   * não faz sentido para arquivo; o envio tem botão próprio.
   */
  readonly podeCriar = this.atlasAuth.canCreateEntity(ENTIDADE);
  readonly filtrosTipo: { rotulo: string; valor: TipoMidia | null }[] = [
    { rotulo: 'Todas', valor: null },
    { rotulo: 'Vídeos', valor: 'Video' },
    { rotulo: 'Imagens', valor: 'Imagem' },
  ];
  readonly filtrosAtivo: { rotulo: string; valor: boolean | null }[] = [
    { rotulo: 'Qualquer situação', valor: null },
    { rotulo: 'Ativas', valor: true },
    { rotulo: 'Inativas', valor: false },
  ];

  itens = signal<LinhaMidia[]>([]);
  total = signal(0);
  carregando = signal(false);
  tipo = signal<TipoMidia | null>(null);
  ativo = signal<boolean | null>(null);
  uso = signal<UsoArmazenamento | null>(null);
  /** Mídia com a prévia do QR code aberta. */
  qrAberto = signal<LinhaMidia | null>(null);

  rotuloQr(m: MidiaDto): string {
    return m.qrTipo ? TIPO_QR_ROTULO[m.qrTipo] : '';
  }

  /** Igual a MontadorQrCode.LegendaPadrao na API. */
  legendaPadrao(m: MidiaDto): string {
    return ({ WhatsApp: 'Fale no WhatsApp', Instagram: 'Siga no Instagram', Telefone: 'Ligue para nós', Pix: 'Pague com Pix' } as Record<string, string>)[m.qrTipo ?? ''] ?? 'Aponte a câmera';
  }

  posicaoTexto(m: MidiaDto): string {
    return ({
      InferiorEsquerdo: 'canto inferior esquerdo', SuperiorDireito: 'canto superior direito', SuperiorEsquerdo: 'canto superior esquerdo',
    } as Record<string, string>)[m.qrPosicao ?? ''] ?? 'canto inferior direito';
  }

  percentualUso = computed(() => {
    const u = this.uso();
    return u && u.cotaBytes > 0 ? Math.min(100, Math.round((u.usadoBytes / u.cotaBytes) * 100)) : 0;
  });
  usoTexto = computed(() => {
    const u = this.uso();
    return u ? `${formatarBytes(u.usadoBytes)} de ${formatarBytes(u.cotaBytes)} (${this.percentualUso()}%)` : '';
  });

  private query: QueryParams = { pageIndex: 0, pageSize: 10 };

  acoesLinha: AcaoLinha[] = [
    { id: 'visualizar', label: 'Pré-visualizar', icone: 'bi bi-play-circle', classe: 'btn-outline-secondary' },
    { id: 'qr', label: 'Ver QR code', icone: 'bi bi-qr-code', classe: 'btn-outline-secondary', visivel: (m: LinhaMidia) => !!m.qrTipo },
    {
      id: 'substituir', label: 'Substituir arquivo', icone: 'bi bi-arrow-repeat', classe: 'btn-outline-primary',
      visivel: () => this.podeAtualizar(),
    },
    {
      id: 'inativar', label: 'Inativar', icone: 'bi bi-pause-circle', classe: 'btn-outline-warning',
      visivel: (m: LinhaMidia) => m.ativo && this.podeAtualizar(),
    },
    {
      id: 'ativar', label: 'Ativar', icone: 'bi bi-check-circle', classe: 'btn-outline-success',
      visivel: (m: LinhaMidia) => !m.ativo && this.podeAtualizar(),
    },
  ];

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.carregando.set(true);
    this.service.listar(this.query, { tipo: this.tipo(), ativo: this.ativo() }).subscribe({
      next: page => {
        this.itens.set(page.content);
        const qr = this.qrAberto();
        if (qr) this.qrAberto.set(page.content.find(m => m.id === qr.id && m.qrTipo) ?? null);
        this.total.set(page.totalElements);
        this.carregando.set(false);
      },
      error: () => this.carregando.set(false),
    });
    this.service.usoArmazenamento().subscribe(u => this.uso.set(u));
  }

  filtrarTipo(valor: TipoMidia | null): void {
    this.tipo.set(valor);
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

  onEnviar(): void {
    this.router.navigate(['/indoor/midia/enviar']);
  }

  onEditar(item: LinhaMidia): void {
    this.service.getById(item.id).subscribe(midia => {
      const campos: FormItemConfig[] = [
        { type: 'section', label: 'Mídia', children: preencherCampos(criarCamposMidia(midia.tipo === 'Video'), midia) },
        { type: 'section', label: 'QR code', children: preencherCampos(criarCamposQrCode(), camposQrDaMidia(midia)) },
        {
          type: 'section', label: 'Histórico', children: [
            { type: 'history', name: 'historico', label: '', col: 'col-12', entityName: ENTIDADE },
          ],
        },
      ];
      this.formModal.open({
        titulo: `Editar ${midia.titulo}`,
        entidade: 'Mídia',
        entityName: ENTIDADE,
        recordId: String(midia.id),
        campos,
        tabbedSections: true,
        largura: '720px',
        errosInline: true,
        permitirContinuar: false,
        mostrarBotaoSalvarECopiar: false,
        registro: midia,
        persistir: (valor, atual: MidiaDto) => this.service.atualizar(atual.id, valor, atual.version),
        msgSucesso: 'Mídia salva. As TVs recebem a mudança na próxima sincronização.',
      }).subscribe(res => { if (res?.recarregar || res?.acao === 'fechar') this.carregar(); });
    });
  }

  onAcaoLinha(ev: { acaoId: string; item: LinhaMidia }): void {
    if (ev.acaoId === 'visualizar') this.visualizar(ev.item);
    if (ev.acaoId === 'qr') this.qrAberto.set(ev.item);
    if (ev.acaoId === 'substituir') this.router.navigate(['/indoor/midia/enviar'], { queryParams: { substituir: ev.item.id } });
    if (ev.acaoId === 'ativar') this.definirAtivo(ev.item, true);
    if (ev.acaoId === 'inativar') this.definirAtivo(ev.item, false);
  }

  onExcluir(m: LinhaMidia): void {
    if (m.quantidadePlaylists > 0) {
      this.toast.warning(`"${m.titulo}" está em ${m.quantidadePlaylists} grade(s) da operadora. Inative a mídia ou peça à operadora para tirá-la.`);
      return;
    }
    this.confirmModal.confirm({
      title: 'Excluir mídia',
      message: `"${m.titulo}" sai da biblioteca e o arquivo é apagado. Confirma?`,
      confirmText: 'Sim, excluir',
      cancelText: 'Cancelar',
      iconClass: 'bi bi-trash text-danger',
    }).subscribe(ok => {
      if (!ok) return;
      this.service.excluir(m.id).subscribe(() => {
        this.toast.success('Mídia excluída.');
        this.carregar();
      });
    });
  }

  /** A URL de leitura é temporária: pede o detalhe na hora e abre em outra aba. */
  private visualizar(m: LinhaMidia): void {
    // Abre a aba antes da requisição: depois do subscribe o navegador bloqueia como pop-up.
    const aba = window.open('', '_blank');
    this.service.getById(m.id).subscribe({
      next: midia => {
        if (midia.urlVisualizacao && aba) aba.location.href = midia.urlVisualizacao;
        else {
          aba?.close();
          this.toast.warning('Pré-visualização indisponível no momento.');
        }
      },
      error: () => aba?.close(),
    });
  }

  private definirAtivo(m: LinhaMidia, ativo: boolean): void {
    this.service.definirAtivo(m.id, ativo, m.version).subscribe(() => {
      this.toast.success(ativo ? 'Mídia ativada.' : 'Mídia inativada: sai do loop das TVs, mas continua nas grades.');
      this.carregar();
    });
  }

  private podeAtualizar(): boolean {
    return this.atlasAuth.canUpdateEntity(ENTIDADE);
  }
}
