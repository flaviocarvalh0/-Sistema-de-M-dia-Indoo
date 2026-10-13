import { Component, effect, inject } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { LayoutComponent, SidebarService, AtlasAuthService } from '@atlas/framework';
import { ADMIN_SIDEBAR_MENU } from './admin.menu';
import { VERSAO_CURTA } from './versao';

/**
 * Casca autenticada da Mídia Indoor. Envolve o LayoutComponent (navbar +
 * sidebar + breadcrumb + <router-outlet>) e orquestra: módulo ativo, carga de
 * menus (server-driven), redirecionamento inteligente e barreira anti-flicker.
 */
@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [LayoutComponent],
  template: `
    <atlas-layout
      sistemaNome="Mídia Indoor"
      sistemaSubtitulo="Vídeos e avisos nas suas TVs"
      sistemaIniciais="MI"
      [versao]="versao"
      versaoRota="/versoes">
    </atlas-layout>
  `
})
export class AppShellComponent {
  /** Carimbada no build por `scripts/gerar-versao.js`. */
  readonly versao = VERSAO_CURTA;

  private sidebar = inject(SidebarService);
  private auth = inject(AtlasAuthService);
  private router = inject(Router);

  private modulos: Array<{ id: number; name?: string; routePrefix?: string }> = [];
  private prefixoAtivo = '';

  private readonly CONTEXTOS_ADM = [{ prefixo: '/framework/', menu: ADMIN_SIDEBAR_MENU }];

  /**
   * Segmentos de 1º nível que o próprio AppShellComponent já reserva como
   * filhos fixos das rotas (`versoes`) — não vêm de `getMyModules()`.
   *
   * Sem esta lista, um módulo de negócio cadastrado (por engano, no Atlas
   * Studio) com o MESMO routePrefix sequestraria a rota: `resolverMenuDoModulo`
   * acharia esse módulo, chamaria `carregarMenusDaApi` com o menu (vazio) dele,
   * e a sidebar esvaziaria ao entrar em `/versoes` — só os itens, porque o
   * link principal/rodapé não dependem do menu do módulo.
   */
  private readonly SEGMENTOS_RESERVADOS = new Set(['versoes']);

  constructor() {
    this.sidebar.configurar({ menuPadrao: [], linkPrincipalPadrao: undefined, contextos: this.CONTEXTOS_ADM });

    // Barreira ligada desde o mount (cobre o getMyModules assíncrono e estado
    // stale do singleton após re-login), evitando flicker do Welcome.
    this.sidebar.carregandoMenus.set(true);

    this.auth.getMyModules().subscribe(mods => {
      this.modulos = (mods as any[]) ?? [];
      this.processarUrl(this.router.url);
    });

    this.router.events
      .pipe(filter(e => e instanceof NavigationEnd))
      .subscribe((e: NavigationEnd) => this.processarUrl(e.urlAfterRedirects || e.url));

    effect(() => {
      // Depende de TODOS os sinais que afetam a decisão — inclusive a matriz de
      // permissões (permissoesUsuario). Assim o effect re-roda quando a matriz
      // chega DEPOIS do menu, reavaliando o redirecionamento (fim do bounce-back).
      this.sidebar.carregandoMenus();
      this.sidebar.menuAtual();
      this.sidebar.linkPrincipalAtual();
      this.auth.permissoesUsuario();
      this.avaliarRedirecionamento(this.router.url);
    });
  }

  private processarUrl(url: string): void {
    this.resolverMenuDoModulo(url);
    this.avaliarRedirecionamento(url);
  }

  private resolverMenuDoModulo(url: string): void {
    const seg = (url.split('?')[0].split('/').filter(Boolean)[0] || '').toLowerCase();

    if (!seg || this.SEGMENTOS_RESERVADOS.has(seg)) {
      // F5 (ou link direto) numa rota reservada (`/versoes`) ANTES de o
      // usuário ter entrado em algum módulo nesta sessão: `prefixoAtivo`
      // nunca foi setado, então sem isto a sidebar fica vazia até a próxima
      // troca de módulo — o refresh perde o contexto porque o app remonta
      // do zero e `/versoes` não é um módulo que dispare `carregarMenusDaApi`.
      // Cai no primeiro módulo acessível como padrão, só para preencher a
      // sidebar; não redireciona a URL, a página pedida continua a mesma.
      if (!this.prefixoAtivo && this.modulos.length > 0) {
        this.carregarMenuPadrao();
        return;
      }
      this.sidebar.carregandoMenus.set(false);
      return;
    }

    const modulo = this.modulos.find(m => (m.routePrefix || '').replace(/^\/+/, '').toLowerCase() === seg);
    if (!modulo) { this.sidebar.carregandoMenus.set(false); return; }

    const prefixo = (modulo.routePrefix || '').replace(/^\/+/, '');
    if (prefixo === this.prefixoAtivo) { this.sidebar.carregandoMenus.set(false); return; }

    this.prefixoAtivo = prefixo;
    this.sidebar.carregarMenusDaApi(modulo.id, { prefixo: '/' + prefixo });
  }

  /**
   * Carrega o menu do primeiro módulo acessível como padrão da sidebar,
   * sem navegar — usado quando a sessão inteira começou numa rota
   * reservada (ver `resolverMenuDoModulo`).
   */
  private carregarMenuPadrao(): void {
    const modulo = this.modulos[0];
    const prefixo = (modulo.routePrefix || '').replace(/^\/+/, '');
    this.prefixoAtivo = prefixo;
    this.sidebar.carregarMenusDaApi(modulo.id, { prefixo: '/' + prefixo });
  }

  private avaliarRedirecionamento(url: string): void {
    // Espera o menu terminar de carregar antes de decidir.
    if (this.sidebar.carregandoMenus()) return;

    const path = url.split('?')[0];
    const segmentos = path.split('/').filter(Boolean);
    if (segmentos.length !== 1) return;

    const seg = segmentos[0].toLowerCase();
    if (this.SEGMENTOS_RESERVADOS.has(seg)) return;

    const ehModulo = this.modulos.some(m => (m.routePrefix || '').replace(/^\/+/, '').toLowerCase() === seg);
    if (!ehModulo) return;

    const destino = this.sidebar.primeiraRotaAcessivel();
    if (destino && destino !== path) {
      this.router.navigateByUrl(destino);
    }
  }
}
