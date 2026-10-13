import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localePt from '@angular/common/locales/pt';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  provideAtlasFramework,
  authInterceptor, loadingInterceptor, errorInterceptor, cacheBustingInterceptor,
} from '@atlas/framework';

import { provideCharts, withDefaultRegisterables } from 'ng2-charts';

import { routes } from './app.routes';
import { metadadosSilenciososInterceptor } from './shared/interceptors/metadados-silenciosos.interceptor';

/**
 * Dados de formatação pt-BR. Registrar NÃO troca o locale padrão da aplicação
 * (segue en-US) — só habilita os pipes que pedem `'pt-BR'` explicitamente, como
 * `currency:'BRL':'symbol':'1.2-2':'pt-BR'`. Sem isto o pipe estoura em tempo
 * de execução (NG0701, "Missing locale data").
 */
registerLocaleData(localePt);

/**
 * Base da API do backend (Atlas.Web). Modo Cookie (ver ADR 0002 do framework).
 *
 * Usado só quando `assets/config.json` não pôde ser lido — ver `main.ts`. Em
 * desenvolvimento os dois coincidem, então o `ng serve` funciona igual com ou
 * sem o arquivo.
 */
export const API_URL_PADRAO = 'https://localhost:7216';

/**
 * Monta a configuração da aplicação para uma base de API.
 *
 * É FUNÇÃO, e não constante, porque o endereço da API passou a vir de
 * `assets/config.json` em tempo de execução: o mesmo pacote compilado serve
 * qualquer ambiente, e trocar o endereço no servidor é editar um arquivo texto
 * em vez de recompilar e republicar.
 *
 * Antes o valor era compilado junto, e mudar de máquina exigia um build novo —
 * foi o que obrigou a republicar o front inteiro só para apontar para outra
 * API.
 */
export function criarAppConfig(apiUrl: string): ApplicationConfig {
  return {
    providers: [
      provideBrowserGlobalErrorListeners(),
      provideRouter(routes, withComponentInputBinding()),

      // HttpClient + interceptors do framework (ordem: cache-busting → auth → loading → erro),
      // com o da aplicação antes do de erro.
      provideHttpClient(
        withInterceptors([
          cacheBustingInterceptor, authInterceptor, loadingInterceptor, metadadosSilenciososInterceptor, errorInterceptor,
        ])
      ),

      // Controladores do chart.js para o atlas-chart-dinamico do framework.
      provideCharts(withDefaultRegisterables()),

      // Configuração do @atlas/framework — autenticação em modo Cookie.
      provideAtlasFramework({
        apiUrl,
        authMode: 'Cookie',
      }),
    ],
  };
}
