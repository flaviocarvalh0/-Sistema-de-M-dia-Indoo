import { FormItemConfig, isCampoDef, isSectionConfig, isSectorConfig } from '@atlas/framework';

/** Preenche o `value` de cada campo com o dado do registro (para edição). */
export function preencherCampos(itens: FormItemConfig[], dados: Record<string, any>): FormItemConfig[] {
  return itens.map(item => {
    if (isCampoDef(item)) return { ...item, value: dados[item.name] ?? null };
    if (isSectorConfig(item)) return { ...item, fields: item.fields.map(f => ({ ...f, value: dados[f.name] ?? null })) };
    if (isSectionConfig(item)) return { ...item, children: preencherCampos(item.children, dados) };
    return item;
  });
}

/**
 * Número a partir do que um campo de formulário devolve: número, "12.5" ou
 * "1.234,56". Vazio vira 0.
 */
export function paraNumero(valor: unknown): number {
  if (typeof valor === 'number') return valor;
  const texto = String(valor ?? '').trim();
  if (!texto) return 0;
  const normalizado = texto.includes(',') ? texto.replace(/\./g, '').replace(',', '.') : texto;
  const n = Number(normalizado);
  return isNaN(n) ? 0 : n;
}
