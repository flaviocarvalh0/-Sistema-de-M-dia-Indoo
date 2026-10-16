/**
 * Versões do app das TVs (backend: Atlas.Application/Business/Rede/AppTv, ADR-021).
 * Enums chegam como NOME (JsonStringEnumConverter).
 */
export type DistribuicaoVersao = 'Suspensa' | 'Teste' | 'Todas';

export interface VersaoAppDto {
  id: number;
  /** versionCode do APK (o "+N" do pubspec): a TV só atualiza para um código maior. */
  codigo: number;
  /** versionName (ex.: 1.2.0). */
  nome: string;
  notas?: string | null;
  tamanhoBytes: number;
  distribuicao: DistribuicaoVersao;
  /** TVs pareadas que informaram este código na última sincronização. */
  telasNestaVersao: number;
  created: string;
  version: number;
}

export interface VersoesAppDto {
  versoes: VersaoAppDto[];
  telasPareadas: number;
  /** TVs com app antigo (sem versionCode na sincronização): precisam de uma instalação manual. */
  telasSemAtualizacaoAutomatica: number;
}

export interface EnvioApk {
  chave: string;
  urlEnvio: string;
  expiraEm: string;
  tipoConteudo: string;
}

export interface NovaVersaoApp {
  codigo: number;
  nome: string;
  notas?: string | null;
  chave: string;
  hash: string;
}

export const DISTRIBUICAO_ROTULO: Record<DistribuicaoVersao, string> = {
  Suspensa: 'Suspensa',
  Teste: 'Só TVs de teste',
  Todas: 'Todas as TVs',
};
