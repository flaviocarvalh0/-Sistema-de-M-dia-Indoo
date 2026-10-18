import { CampoOption } from '@atlas/framework';

/**
 * Dias da semana em bits, como a API manda e recebe (`diasSemana`):
 * domingo = 1, segunda = 2, … sábado = 64; 127 = todos.
 */
export const DIAS_SEMANA: ReadonlyArray<{ bit: number; curto: string; nome: string }> = [
  { bit: 2, curto: 'Seg', nome: 'Segunda' },
  { bit: 4, curto: 'Ter', nome: 'Terça' },
  { bit: 8, curto: 'Qua', nome: 'Quarta' },
  { bit: 16, curto: 'Qui', nome: 'Quinta' },
  { bit: 32, curto: 'Sex', nome: 'Sexta' },
  { bit: 64, curto: 'Sáb', nome: 'Sábado' },
  { bit: 1, curto: 'Dom', nome: 'Domingo' },
];

export const TODOS_OS_DIAS = 127;
export const DIAS_UTEIS = 62;
export const FIM_DE_SEMANA = 65;

/** "Todos os dias", "Seg a sex", "Fim de semana" ou "Seg, Qua, Sex". */
export function descreverDias(dias: number): string {
  if (dias === TODOS_OS_DIAS) return 'Todos os dias';
  if (dias === DIAS_UTEIS) return 'Seg a sex';
  if (dias === FIM_DE_SEMANA) return 'Fim de semana';
  const marcados = DIAS_SEMANA.filter(d => (dias & d.bit) !== 0).map(d => d.curto);
  return marcados.length ? marcados.join(', ') : 'Nenhum dia';
}

/** "1,5 MB", "820 KB". */
export function formatarBytes(bytes: number | null | undefined): string {
  const b = bytes ?? 0;
  if (b >= 1024 * 1024 * 1024) return `${(b / (1024 * 1024 * 1024)).toLocaleString('pt-BR', { maximumFractionDigits: 1 })} GB`;
  if (b >= 1024 * 1024) return `${(b / (1024 * 1024)).toLocaleString('pt-BR', { maximumFractionDigits: 1 })} MB`;
  return `${Math.max(1, Math.round(b / 1024)).toLocaleString('pt-BR')} KB`;
}

/** "15 s", "1 min 30 s". */
export function formatarDuracao(segundos: number | null | undefined): string {
  const s = Math.max(0, Math.round(segundos ?? 0));
  if (s < 60) return `${s} s`;
  const min = Math.floor(s / 60);
  const resto = s % 60;
  return resto ? `${min} min ${resto} s` : `${min} min`;
}

/** Opções de select a partir de um mapa valor → rótulo. */
export function opcoes<T extends string>(rotulos: Record<T, string>): CampoOption<T>[] {
  return (Object.keys(rotulos) as T[]).map(value => ({ value, label: rotulos[value] }));
}

/** Fusos do Brasil (IANA), para o cadastro do cliente. */
export const FUSOS_BRASIL: Record<string, string> = {
  'America/Sao_Paulo': 'Brasília (SP, RJ, MG, Sul, GO, DF, BA, NE)',
  'America/Manaus': 'Amazonas (Manaus)',
  'America/Cuiaba': 'Mato Grosso (Cuiabá)',
  'America/Campo_Grande': 'Mato Grosso do Sul (Campo Grande)',
  'America/Porto_Velho': 'Rondônia (Porto Velho)',
  'America/Boa_Vista': 'Roraima (Boa Vista)',
  'America/Rio_Branco': 'Acre (Rio Branco)',
  'America/Belem': 'Pará (Belém)',
  'America/Fortaleza': 'Ceará (Fortaleza)',
  'America/Recife': 'Pernambuco (Recife)',
  'America/Noronha': 'Fernando de Noronha',
};

/** `yyyy-MM-dd` (DateOnly da API) → `dd/MM/yyyy`, sem passar por fuso. */
export function formatarData(iso: string | null | undefined): string {
  if (!iso) return '';
  const [ano, mes, dia] = iso.slice(0, 10).split('-');
  return `${dia}/${mes}/${ano}`;
}
