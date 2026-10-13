import { SidebarAccordionGroup } from '@atlas/framework';

/**
 * Menu ESTÁTICO do Atlas Studio (administração da plataforma), exibido sob o
 * contexto `/framework/`. O Studio não é um módulo de `getMyModules()`, por isso
 * seu menu é fixo (ver padrão do @atlas/framework).
 */
export const ADMIN_SIDEBAR_MENU: SidebarAccordionGroup[] = [
  {
    id: 'acesso',
    headerText: 'Controle de Acesso',
    isInitiallyOpen: true,
    navLinks: [
      { label: 'Usuários', routerLink: '/framework/usuarios', iconClass: 'bi bi-people' },
      { label: 'Grupos e Perfis', routerLink: '/framework/grupos', iconClass: 'bi bi-shield-check' }
    ]
  },
  {
    id: 'configs',
    headerText: 'Configurações Globais',
    isInitiallyOpen: true,
    navLinks: [
      { label: 'Parâmetros', routerLink: '/framework/parametros', iconClass: 'bi bi-sliders' },
      { label: 'Servidor SMTP', routerLink: '/framework/smtp', iconClass: 'bi bi-envelope-arrow-up' }
    ]
  }
];
