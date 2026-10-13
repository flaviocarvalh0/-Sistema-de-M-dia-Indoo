/**
 * Utilitários de DATA para os campos `type: 'data'` (input nativo
 * `yyyy-MM-dd`).
 *
 * Existe para um bug só, mas que reaparecia em toda tela nova:
 * `new Date().toISOString()` serializa em UTC. No Brasil (UTC-3), das 21h em
 * diante o dia UTC já virou — "hoje" voltava como o dia SEGUINTE, e as travas
 * de período passavam a barrar a data corrente. Compensar o offset do fuso
 * antes de serializar preserva o dia do relógio do usuário.
 */

/**
 * Valor em BRL ABREVIADO para leitura gerencial: "R$ 4,5 mi", "R$ 1,2 mil".
 * Some com os centavos de propósito — nos painéis de visão geral a ordem de
 * grandeza é o que importa, e o valor exato está sempre no grid abaixo.
 *
 * Vive aqui (e não no framework) porque a regra de corte é de apresentação do
 * app; o componente de faixas recebe o texto já pronto.
 */
export function moedaAbreviadaBRL(valor: number): string {
  const v = valor ?? 0;
  const abs = Math.abs(v);
  const sinal = v < 0 ? '-' : '';
  const num = (n: number, casas: number) =>
    n.toLocaleString('pt-BR', { minimumFractionDigits: casas, maximumFractionDigits: casas });

  if (abs >= 1_000_000_000) return `${sinal}R$ ${num(abs / 1_000_000_000, 1)} bi`;
  if (abs >= 1_000_000)     return `${sinal}R$ ${num(abs / 1_000_000, 1)} mi`;
  if (abs >= 1_000)         return `${sinal}R$ ${num(abs / 1_000, 1)} mil`;
  // Abaixo de mil não há o que abreviar — mostra inteiro, ainda sem centavos.
  return `${sinal}R$ ${num(abs, 0)}`;
}

/** Data LOCAL em `yyyy-MM-dd` (default: agora). */
export function dataLocalISO(base: Date = new Date()): string {
  return new Date(base.getTime() - base.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
}

/** Ontem, em `yyyy-MM-dd` local. */
export function ontemLocalISO(): string {
  return dataLocalISO(new Date(Date.now() - 24 * 60 * 60 * 1000));
}

/** `dias` atrás a partir de hoje, em `yyyy-MM-dd` local. */
export function diasAtrasLocalISO(dias: number): string {
  const d = new Date();
  d.setDate(d.getDate() - dias);
  return dataLocalISO(d);
}

/** Diferença em dias inteiros entre duas datas `yyyy-MM-dd`. */
export function diferencaEmDias(inicioISO: string, fimISO: string): number {
  const ms = new Date(`${fimISO}T00:00:00`).getTime() - new Date(`${inicioISO}T00:00:00`).getTime();
  return Math.round(ms / (24 * 60 * 60 * 1000));
}

/**
 * Primeiro dia do mês corrente em `yyyy-MM-dd`. Montado a partir dos getters
 * locais (`getFullYear`/`getMonth`), então já é imune ao desvio de fuso.
 */
export function primeiroDiaDoMesISO(): string {
  const hoje = new Date();
  return `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}-01`;
}

/**
 * ÚLTIMO dia do mês corrente em `yyyy-MM-dd`. `new Date(ano, mes + 1, 0)` cai
 * no último dia do mês anterior ao informado — ou seja, no fim do mês atual —
 * e já trata 28/29/30/31 sozinho.
 *
 * Existe para janelas que precisam do mês INTEIRO (ex.: carteira de cobranças,
 * cujos vencimentos são majoritariamente futuros), diferente das telas que
 * olham para trás e terminam em "hoje".
 */
export function ultimoDiaDoMesISO(): string {
  const hoje = new Date();
  return dataLocalISO(new Date(hoje.getFullYear(), hoje.getMonth() + 1, 0));
}
