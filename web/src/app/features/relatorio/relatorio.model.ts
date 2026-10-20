/** Relatório de veiculação (backend: Atlas.Application/Business/Veiculacao, ADR-023). */
export interface RelatorioVeiculacaoDto {
  clienteId: number;
  clienteNome: string;
  /** Período, inclusive (yyyy-MM-dd), no fuso do cliente. */
  de: string;
  ate: string;
  geradoEm: string;
  exibicoes: number;
  concluidas: number;
  interrompidas: number;
  /** Exibidas com a TV sem falar com a API (tocou do que já tinha baixado). */
  semConexao: number;
  segundosNaTela: number;
  leiturasQr: number;
  leitoresUnicosQr: number;
  /** Falso por enquanto: o registro não sabe se a TV estava ligada. */
  medeTelaLigada: boolean;
  porTela: { telaId: number; nome: string; parceiro?: string | null; exibicoes: number; segundosNaTela: number }[];
  porMidia: { midiaId: number; titulo: string; exibicoes: number; leiturasQr: number; leitoresUnicosQr: number; qrRastreado: boolean }[];
  porDia: { data: string; exibicoes: number; leiturasQr: number }[];
  /** 24 posições (0 a 23 h), na hora local de cada TV. */
  porHora: number[];
}
