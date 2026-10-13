import { bootstrapApplication } from '@angular/platform-browser';
import { criarAppConfig, API_URL_PADRAO } from './app/app.config';
import { App } from './app/app';
import { VERSAO_APP } from './app/versao';

/**
 * Endereço da API, lido de `assets/config.json` ANTES de a aplicação subir.
 *
 * Tem de ser antes porque `provideAtlasFramework({ apiUrl })` recebe o valor na
 * montagem dos providers — depois do bootstrap já é tarde.
 *
 * `cache: 'no-store'` mais o carimbo na query: sem isso o IIS serve o
 * config.json da publicação anterior (o web.config cacheia estático por um
 * ano), e trocar o endereço no servidor não teria efeito nenhum até alguém
 * limpar o cache do navegador.
 */
async function lerApiUrl(): Promise<string> {
  try {
    const resposta = await fetch(`assets/config.json?t=${Date.now()}`, { cache: 'no-store' });
    if (!resposta.ok) throw new Error(`HTTP ${resposta.status}`);

    const config = await resposta.json();
    const url = typeof config?.apiUrl === 'string' ? config.apiUrl.trim() : '';
    if (!url) throw new Error('campo "apiUrl" ausente ou vazio');

    return url;
  } catch (erro) {
    // Não derruba a aplicação: cai no valor compilado e DEIXA O RASTRO. Sem o
    // aviso, um config.json ausente no servidor viraria "a API está no ar mas
    // ninguém loga", e o motivo estaria escondido.
    console.warn(
      `[config] Não foi possível ler assets/config.json (${erro}). `
      + `Usando o endereço padrão: ${API_URL_PADRAO}`
    );
    return API_URL_PADRAO;
  }
}

/**
 * Tira a tela de abertura (definida no index.html) depois que o Angular
 * assume. O esmaecimento fica por conta da transição CSS; o nó sai do documento
 * quando ele termina.
 */
function encerrarAbertura(): void {
  const abertura = document.getElementById('abertura');
  if (!abertura) return;
  abertura.classList.add('saindo');
  setTimeout(() => abertura.remove(), 400);
}

/**
 * Bootstrap falhou. A abertura NÃO sai: ela troca para o estado de erro.
 * Removê-la deixaria o usuário diante de um retângulo vazio, sem saber
 * se ainda está carregando ou se quebrou.
 */
function falharAbertura(erro: unknown): void {
  console.error(erro);
  document.getElementById('abertura')?.classList.add('falhou');
}

// No console desde o primeiro instante. Quando alguém relata um problema, a
// primeira pergunta é "qual versão você está usando?", e a resposta precisa
// estar ali sem o usuário ter de navegar para lugar nenhum.
console.info(
  `%c Mídia Indoor %c ${VERSAO_APP.versao} %c ${VERSAO_APP.compiladoEm} `,
  'background:#0F1F2E;color:#fff;border-radius:3px 0 0 3px',
  'background:#0F766E;color:#fff',
  'background:#eee;color:#333;border-radius:0 3px 3px 0',
);

lerApiUrl()
  .then(apiUrl => bootstrapApplication(App, criarAppConfig(apiUrl)))
  .then(encerrarAbertura)
  .catch(falharAbertura);
