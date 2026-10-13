import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { VERSAO_APP } from '../../versao';

/**
 * Notas de versão, DENTRO da aplicação e atrás do login.
 *
 * Existia como página estática na raiz do site (`/versao.html`), o que a
 * deixava legível por qualquer um que descobrisse o endereço — inaceitável se
 * o sistema for exposto na internet. Aqui ela herda o `authGuard` da área
 * autenticada.
 *
 * O conteúdo vem do `CHANGELOG.md`, carimbado no build pelo
 * `scripts/gerar-versao.js`. Nenhuma chamada de rede: a informação já veio
 * junto com o pacote.
 */
@Component({
  selector: 'app-versoes-page',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="versoes">
      <div class="cabecalho">
        <div>
          <h2>Notas de versão</h2>
          <p class="sub">O que mudou em cada entrega da Mídia Indoor.</p>
        </div>
        <div class="atual">
          <span class="rotulo">em uso</span>
          <strong>{{ versao }}</strong>
          <span class="data">{{ compiladoEm | date:'dd/MM/yyyy, HH:mm' }}</span>
        </div>
      </div>

      <article *ngFor="let v of historico" class="card" [class.destaque]="v.versao === versao">
        <div class="titulo">
          <span class="numero">{{ v.versao }}</span>
          <span class="periodo" *ngIf="v.periodo">{{ v.periodo }}</span>
          <span class="selo" *ngIf="v.versao === versao">ATUAL</span>
        </div>

        <ng-container *ngFor="let bloco of blocos(v.notas)">
          <h3 *ngIf="bloco.titulo">{{ bloco.titulo }}</h3>
          <p *ngIf="!bloco.titulo && bloco.itens.length === 0 && bloco.texto">{{ bloco.texto }}</p>
          <ul *ngIf="bloco.itens.length">
            <li *ngFor="let i of bloco.itens">{{ i }}</li>
          </ul>
        </ng-container>
      </article>
    </div>
  `,
  styles: [`
    .versoes { max-width: 860px; }

    .cabecalho {
      display: flex; align-items: flex-start; justify-content: space-between;
      gap: 16px; flex-wrap: wrap; margin-bottom: 20px;
    }
    h2 { margin: 0; font-size: 1.3rem; color: var(--color-gray-800, #343a40); }
    .sub { margin: 2px 0 0; font-size: .88rem; color: var(--color-gray-600, #6c757d); }

    .atual {
      display: flex; flex-direction: column; align-items: flex-end;
      background: var(--color-gray-50, #f8f9fa);
      border: 1px solid var(--color-gray-200, #dee2e6);
      border-radius: 8px; padding: 8px 14px; line-height: 1.3;
    }
    .atual .rotulo {
      font-size: .64rem; text-transform: uppercase; letter-spacing: 1px;
      color: var(--color-gray-500, #6c757d);
    }
    .atual strong { font-size: 1.1rem; color: var(--color-navy-600, #1d6b2c); }
    .atual .data { font-size: .72rem; color: var(--color-gray-500, #6c757d); }

    .card {
      background: #fff;
      border: 1px solid var(--color-gray-200, #dee2e6);
      border-radius: 10px; padding: 18px 20px; margin-bottom: 12px;
    }
    /* A versão em uso puxa o olho: numa lista de nove, é a única que o usuário
       precisa achar sem procurar. */
    .card.destaque {
      border-color: var(--color-navy-600, #1d6b2c);
      box-shadow: 0 2px 12px rgba(29, 107, 44, .1);
    }

    .titulo { display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap; margin-bottom: 4px; }
    .numero { font-size: 1.08rem; font-weight: 700; color: var(--color-navy-600, #1d6b2c); }
    .periodo { font-size: .78rem; color: var(--color-gray-500, #6c757d); }
    .selo {
      background: var(--color-navy-600, #1d6b2c); color: #fff;
      font-size: .62rem; font-weight: 700; letter-spacing: .6px;
      padding: 2px 8px; border-radius: 999px;
    }

    h3 {
      margin: 14px 0 5px; font-size: .7rem; text-transform: uppercase;
      letter-spacing: 1px; color: var(--color-gray-500, #6c757d);
    }
    ul { margin: 0; padding-left: 20px; }
    li { margin-bottom: 4px; font-size: .9rem; color: var(--color-gray-700, #495057); }
    p { margin: 0 0 6px; font-size: .9rem; color: var(--color-gray-700, #495057); }
  `],
})
export class VersoesPageComponent {
  readonly versao = VERSAO_APP.versao;
  readonly compiladoEm = VERSAO_APP.compiladoEm;
  readonly historico = VERSAO_APP.historico;

  /**
   * Converte as linhas de markdown do CHANGELOG em blocos renderizáveis.
   *
   * Só o mínimo: subtítulo (`###`), item de lista (`-`) e parágrafo. Item que
   * continua na linha seguinte é juntado — no arquivo as linhas são quebradas
   * em 80 colunas, e renderizar cada pedaço como um item separado
   * transformaria uma frase em três marcadores.
   *
   * A lista de commits NÃO entra aqui: é detalhe técnico de quem publica, e
   * esta tela é de quem usa.
   */
  blocos(notas: readonly string[]): Array<{ titulo: string; texto: string; itens: string[] }> {
    const saida: Array<{ titulo: string; texto: string; itens: string[] }> = [];
    let atual = { titulo: '', texto: '', itens: [] as string[] };
    let item: string | null = null;

    const fecharItem = () => { if (item !== null) { atual.itens.push(this.limpar(item)); item = null; } };
    const fecharBloco = () => {
      fecharItem();
      if (atual.titulo || atual.texto || atual.itens.length) saida.push(atual);
      atual = { titulo: '', texto: '', itens: [] };
    };

    for (const linha of notas) {
      const sub = /^###\s+(.*)$/.exec(linha);
      if (sub) { fecharBloco(); atual.titulo = sub[1]; continue; }

      const bullet = /^[-*]\s+(.*)$/.exec(linha);
      if (bullet) { fecharItem(); item = bullet[1]; continue; }

      if (!linha.trim()) { fecharItem(); continue; }
      if (item !== null) { item += ' ' + linha.trim(); continue; }

      // Parágrafo solto (a introdução da versão).
      atual.texto = atual.texto ? `${atual.texto} ${linha.trim()}` : linha.trim();
    }
    fecharBloco();
    return saida;
  }

  /** Tira a marcação de código e negrito — a tela não renderiza markdown. */
  private limpar(texto: string): string {
    return texto.replace(/`/g, '').replace(/\*\*/g, '');
  }
}
