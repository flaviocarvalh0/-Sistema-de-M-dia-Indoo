import { FormItemConfig } from '@atlas/framework';

/**
 * Máscara dupla de telefone (ngx-mask): aceita FIXO (DDD + 8 dígitos) e
 * CELULAR (DDD + 9 dígitos). A máscara mais curta vem primeiro para o ngx-mask
 * escolher dinamicamente conforme o usuário digita.
 */
export const TELEFONE_MASK = '(00) 0000-0000||(00) 00000-0000';

/** Ajustes pontuais do campo de telefone reutilizável. */
export interface TelefoneFieldOverrides {
  name?: string;
  label?: string;
  col?: string;
  hint?: string;
  required?: boolean;
}

/**
 * Configuração CENTRALIZADA (DRY) do campo de Telefone/Celular para o motor de
 * formulário dinâmico — MESMO padrão do {@link DocumentFormConfig} (CPF/CNPJ).
 * Um único ponto define máscara (fixo|celular) + validação (`validacao: 'telefone'`),
 * consumido por qualquer formulário que capture telefone (Empresa, etc.).
 */
export class TelefoneFormConfig {
  /**
   * Campo padronizado de Telefone (fixo ou celular).
   *
   * @param overrides ajustes de `name` / `label` / `col` / `hint` / `required`.
   */
  static get(overrides: TelefoneFieldOverrides = {}): FormItemConfig {
    return {
      type: 'texto',
      name: overrides.name ?? 'telefone',
      label: overrides.label ?? 'Telefone',
      required: overrides.required ?? false,
      validacao: 'telefone',
      mask: TELEFONE_MASK,
      col: overrides.col ?? 'col-md-6',
      hint: overrides.hint,
    };
  }
}
