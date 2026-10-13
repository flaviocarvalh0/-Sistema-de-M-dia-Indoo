import { HttpInterceptorFn } from '@angular/common/http';
import { comErroLocal } from '@atlas/framework';

/**
 * O formulário dinâmico do framework consulta `GET /api/EntityMetadata` para
 * saber se mostra a aba de histórico. Quem não é administrador recebe 403 ali
 * (a permissão é do Atlas Studio) e o formulário segue sem problema, mas o
 * interceptor de erro global mostrava "sem permissão" a cada modal aberto.
 * Esta consulta trata o erro sozinha, então sai do tratamento global.
 */
export const metadadosSilenciososInterceptor: HttpInterceptorFn = (req, next) =>
  req.method === 'GET' && /\/api\/EntityMetadata\/?$/i.test(req.url.split('?')[0])
    ? next(req.clone({ context: comErroLocal(req.context) }))
    : next(req);
