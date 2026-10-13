import { FormItemConfig } from '@atlas/framework';

/**
 * Máscara de CNPJ com suporte ALFANUMÉRICO (vigente a partir de jul/2026):
 * 12 posições alfanuméricas (`A` = letra ou dígito) + 2 dígitos verificadores (`0`).
 * Ex.: `12.ABC.345/01DE-35`.
 */
export const CNPJ_MASK = 'AA.AAA.AAA/AAAA-00';

/** Ajustes pontuais do campo de documento reutilizável. */
export interface DocumentFieldOverrides {
  name?: string;
  label?: string;
  col?: string;
  hint?: string;
  /**
   * Campo travado, em cinza. Para entidades cujo documento é IMUTÁVEL depois
   * de criado — hoje a Empresa, cujo `UpdateEmpresaCommand` sequer tem o
   * campo: deixá-lo editável fazia a tela aceitar a digitação, responder
   * "salvo com sucesso" e não mudar nada.
   */
  disabled?: boolean;
}

/**
 * Configuração CENTRALIZADA (DRY) do campo CNPJ para o motor de formulário
 * dinâmico. Um único ponto define máscara alfanumérica + validação, consumido
 * por Empresa, Grupo Econômico (e demais entidades com CNPJ).
 *
 * O validador `cnpj` do @atlas/framework já aceita o formato alfanumérico; aqui
 * apenas garantimos a máscara compatível (o bloqueio anterior era só na máscara).
 */
export class DocumentFormConfig {
  /**
   * Campo padronizado de CNPJ (alfanumérico).
   *
   * @param required  obrigatoriedade do campo.
   * @param overrides ajustes de `name` / `label` / `col` / `hint`.
   */
  static get(required = true, overrides: DocumentFieldOverrides = {}): FormItemConfig {
    return {
      type: 'texto',
      name: overrides.name ?? 'cnpj',
      label: overrides.label ?? 'CNPJ',
      required,
      validacao: 'cnpj',
      mask: CNPJ_MASK,
      col: overrides.col ?? 'col-md-4',
      hint: overrides.hint,
      disabled: overrides.disabled ?? false,
    };
  }
}
