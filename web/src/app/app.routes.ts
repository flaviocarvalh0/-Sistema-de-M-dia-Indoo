import { Routes } from '@angular/router';
import { authGuard, guestGuard, atlasPermissionGuard } from '@atlas/framework';
import { AppShellComponent } from './app-shell.component';

// Sem `logoSrc` enquanto não houver logotipo: as telas do framework escondem a
// imagem quando o campo vem vazio.
const AUTH_BRAND = {
  sistemaNome: 'Mídia Indoor',
  sistemaSubtitulo: 'Vídeos e avisos nas suas TVs, sempre no ar',
};

const LAUNCHER_BRAND = {
  sistemaNome: 'Mídia Indoor',
  adminRoute: '/atlas-studio',
};

export const routes: Routes = [

  // ── Autenticação (sem layout) ──────────────────────────────────────────
  {
    path: 'login',
    canActivate: [guestGuard],
    data: { ...AUTH_BRAND },
    loadComponent: () => import('@atlas/framework').then(m => m.AtlasLoginComponent)
  },

  {
    path: 'recuperar-senha',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/acesso/recuperar-senha-page.component')
        .then(m => m.RecuperarSenhaPageComponent)
  },

  // ── Páginas de erro globais ────────────────────────────────────────────
  { path: 'erro/403', loadComponent: () => import('@atlas/framework').then(m => m.AccessDeniedComponent) },
  { path: 'erro/500', loadComponent: () => import('@atlas/framework').then(m => m.ServerErrorComponent) },

  // ── Launcher de módulos (portal inicial) ───────────────────────────────
  {
    path: '',
    pathMatch: 'full',
    canActivate: [authGuard],
    data: { ...LAUNCHER_BRAND },
    loadComponent: () => import('@atlas/framework').then(m => m.AtlasModuleLauncherComponent)
  },

  // -------------------------------------------------------------------------------------------------------------------
  // WORKSPACE DO ATLAS STUDIO (App Builder) - Fora do layout padrão (AppShellComponent)
  // -------------------------------------------------------------------------------------------------------------------
  {
    path: 'atlas-studio',
    canActivate: [authGuard, atlasPermissionGuard],
    data: { entity: 'SystemModules', action: 'View' },
    loadComponent: () => import('@atlas/framework').then(m => m.AtlasPainelModulosComponent)
  },
  {
    path: 'atlas-studio/aplicativo/:id',
    canActivate: [authGuard],
    loadComponent: () => import('@atlas/framework').then(m => m.AtlasStudioWorkspaceComponent),
    children: [
      { path: 'menus', canActivate: [atlasPermissionGuard], data: { entity: 'SystemMenus', action: 'View' }, loadComponent: () => import('@atlas/framework').then(m => m.AtlasPainelMenusComponent) },
      { path: 'icones', canActivate: [atlasPermissionGuard], data: { entity: 'SystemIcons', action: 'View' }, loadComponent: () => import('@atlas/framework').then(m => m.AtlasPainelIconesComponent) }
    ]
  },

  // ── Área autenticada (com layout AppShell) ─────────────────────────────
  {
    path: '',
    component: AppShellComponent,
    canActivate: [authGuard],
    children: [

      // Notas de versão: não é entidade do RBAC; o authGuard do pai basta.
      {
        path: 'versoes',
        loadComponent: () =>
          import('./features/versoes/versoes-page.component')
            .then(m => m.VersoesPageComponent)
      },

      // Módulos de negócio, um bloco por módulo do Atlas Studio
      // (api/docs/scripts/studio-indoor.sql): o `path` do pai é o routePrefix
      // do módulo e o de cada filho é o route_key da entidade (nome físico da
      // tabela em kebab-case). Divergir disso faz o link do menu apontar para
      // uma URL que este arquivo não registra.

      // Módulo "Mídia Indoor" (/indoor) — o painel do cliente (anunciante):
      // as mídias dele e onde elas passam.
      {
        path: 'indoor',
        children: [
          {
            path: 'tela',
            canActivate: [atlasPermissionGuard],
            data: { entity: 'tela', action: 'View' },
            loadComponent: () =>
              import('./features/telas/pages/tela-list-page.component')
                .then(m => m.TelaListPageComponent)
          },
          {
            path: 'midia',
            canActivate: [atlasPermissionGuard],
            data: { entity: 'midia', action: 'View' },
            loadComponent: () =>
              import('./features/midias/pages/midia-list-page.component')
                .then(m => m.MidiaListPageComponent)
          },
          {
            path: 'midia/enviar',
            canActivate: [atlasPermissionGuard],
            data: { entity: 'midia', action: 'Create' },
            loadComponent: () =>
              import('./features/midias/pages/envio-midia-page.component')
                .then(m => m.EnvioMidiaPageComponent)
          },
          { path: '', redirectTo: 'midia', pathMatch: 'full' },
        ]
      },

      // Módulo "Rede" (/rede) — a operadora: parceiros (locais), clientes, telas (hardware),
      // playlists (as grades da rede) e fontes de notícias.
      {
        path: 'rede',
        children: [
          {
            // Locais das TVs (não acessam o sistema).
            path: 'parceiro',
            canActivate: [atlasPermissionGuard],
            data: { entity: 'parceiro', action: 'View' },
            loadComponent: () =>
              import('./features/rede/pages/parceiro-list-page.component')
                .then(m => m.ParceiroListPageComponent)
          },
          {
            // Anunciantes: têm acesso ao painel (módulo Mídia Indoor).
            path: 'cliente',
            canActivate: [atlasPermissionGuard],
            data: { entity: 'cliente', action: 'View' },
            loadComponent: () =>
              import('./features/rede/pages/cliente-list-page.component')
                .then(m => m.ClienteListPageComponent)
          },
          {
            // Telas da rede: a operadora é dona do hardware. A mesma entidade
            // aparece somente leitura para o cliente em /indoor/tela.
            path: 'tela',
            canActivate: [atlasPermissionGuard],
            data: { entity: 'tela', action: 'Create' },
            loadComponent: () =>
              import('./features/rede/pages/tela-rede-list-page.component')
                .then(m => m.TelaRedeListPageComponent)
          },
          {
            // Versões do app das TVs: envio do APK e liberação (ADR-021).
            path: 'tela/versoes-app',
            canActivate: [atlasPermissionGuard],
            data: { entity: 'tela', action: 'Create' },
            loadComponent: () =>
              import('./features/app-tv/pages/versoes-app-page.component')
                .then(m => m.VersoesAppPageComponent)
          },
          {
            // Grades da rede: a operadora monta com as mídias dos clientes.
            path: 'playlist',
            canActivate: [atlasPermissionGuard],
            data: { entity: 'playlist', action: 'View' },
            loadComponent: () =>
              import('./features/playlists/pages/playlist-list-page.component')
                .then(m => m.PlaylistListPageComponent)
          },
          {
            path: 'playlist/:id/grade',
            canActivate: [atlasPermissionGuard],
            data: { entity: 'playlist', action: 'View' },
            loadComponent: () =>
              import('./features/playlists/pages/grade-playlist-page.component')
                .then(m => m.GradePlaylistPageComponent)
          },
          {
            // Feeds RSS/Atom; as notícias entram nas playlists que intercalam notícias.
            path: 'fonte-noticia',
            canActivate: [atlasPermissionGuard],
            data: { entity: 'fonte_noticia', action: 'View' },
            loadComponent: () =>
              import('./features/noticias/pages/fonte-noticia-list-page.component')
                .then(m => m.FonteNoticiaListPageComponent)
          },

          { path: '', redirectTo: 'parceiro', pathMatch: 'full' },
        ]
      },

      // Atlas Studio (administração da plataforma)
      { path: 'framework', redirectTo: 'atlas-studio', pathMatch: 'full' },
      { path: 'framework/configuracoes', loadComponent: () => import('@atlas/framework').then(m => m.VisaoGeralComponent) },
      {
        path: 'framework/usuarios', canActivate: [atlasPermissionGuard], data: { entity: 'Users', action: 'View' },
        loadComponent: () => import('@atlas/framework').then(m => m.AtlasPainelUsuariosComponent)
      },
      {
        path: 'framework/grupos', canActivate: [atlasPermissionGuard], data: { entity: 'Roles', action: 'View' },
        loadComponent: () => import('@atlas/framework').then(m => m.AtlasPainelGruposComponent)
      },
      {
        path: 'framework/parametros', canActivate: [atlasPermissionGuard], data: { entity: 'Parameters', action: 'View' },
        loadComponent: () => import('@atlas/framework').then(m => m.AtlasPainelParametrosComponent)
      },
      {
        path: 'framework/smtp', canActivate: [atlasPermissionGuard], data: { entity: 'Settings', action: 'ViewSmtp' },
        loadComponent: () => import('@atlas/framework').then(m => m.AtlasPainelSmtpComponent)
      },

      // Curinga dentro do layout (404 com navbar/sidebar)
      { path: '**', loadComponent: () => import('@atlas/framework').then(m => m.NotFoundComponent) }
    ]
  },
];
