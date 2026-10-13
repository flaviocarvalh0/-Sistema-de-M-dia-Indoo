/**
 * Formatação de documentos para exibição.
 *
 * A API trafega o CNPJ CRU, com catorze caracteres e sem pontuação — é assim
 * que ele é guardado e comparado. A máscara é assunto de tela, e vive aqui
 * para não ser reescrita a cada grade que precisa mostrar um.
 */

/**
 * `12345678000190` vira `12.345.678/0001-90`.
 *
 * Valor com tamanho diferente de catorze volta **como veio**, em vez de
 * receber uma máscara errada: um documento truncado precisa parecer errado na
 * tela, não parecer válido.
 *
 * Aceita letra porque o CNPJ alfanumérico entra em vigor em 2026, e a regra de
 * posicionamento dos separadores é a mesma.
 */
export function formatarCnpj(valor: string | null | undefined): string {
  const cru = (valor ?? '').replace(/[^0-9A-Za-z]/g, '').toUpperCase();
  if (cru.length !== 14) return cru;

  return `${cru.slice(0, 2)}.${cru.slice(2, 5)}.${cru.slice(5, 8)}`
    + `/${cru.slice(8, 12)}-${cru.slice(12)}`;
}

/**
 * `11999990000` vira `(11) 99999-0000`; `1133334444`, `(11) 3333-4444`.
 *
 * Fora de 10 ou 11 dígitos volta como veio, pelo mesmo motivo do CNPJ.
 */
export function formatarTelefone(valor: string | null | undefined): string {
  const digitos = (valor ?? '').replace(/\D/g, '');
  if (digitos.length !== 10 && digitos.length !== 11) return valor ?? '';

  const meio = digitos.length - 4;
  return `(${digitos.slice(0, 2)}) ${digitos.slice(2, meio)}-${digitos.slice(meio)}`;
}
