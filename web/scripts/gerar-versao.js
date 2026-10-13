/**
 * Carimba a versão do build antes de o Angular compilar, em três formatos.
 *
 * Gera APENAS `src/app/versao.ts`, que entra no pacote da aplicação e
 * alimenta a rota `/versoes` — atrás do login.
 *
 * Já houve aqui um `versao.html` e um `versao.json` na raiz do site, que
 * respondiam "o que está no ar?" sem exigir credencial. Eram convenientes
 * para quem publica e legíveis por QUALQUER UM que descobrisse o endereço,
 * o que não se sustenta com o sistema exposto na internet. Quem precisa
 * conferir o que subiu entra no sistema.
 *
 * Tudo vem do `CHANGELOG.md`: ele é a fonte, e estes arquivos são projeções.
 * Manter duas listas de novidades em lugares diferentes é garantir que uma
 * delas fique velha.
 *
 * Roda pelos hooks `prebuild` e `prestart`. Os três arquivos gerados são
 * IGNORADOS pelo git — são derivados, e versioná-los criaria conflito a cada
 * build.
 */
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

const raiz = path.join(__dirname, '..');
const pkg = JSON.parse(fs.readFileSync(path.join(raiz, 'package.json'), 'utf8'));

/** Executa um comando git, devolvendo vazio se não houver repositório. */
function git(comando) {
  try {
    return execSync(`git ${comando}`, { cwd: raiz, stdio: ['ignore', 'pipe', 'ignore'] })
      .toString().trim();
  } catch {
    return '';
  }
}

/**
 * Lê o `CHANGELOG.md` inteiro e devolve uma entrada por versão.
 *
 * Só exige que o cabeçalho `##` contenha algo com cara de versão. O resto do
 * formato é livre: um gerador que recusa o texto por causa de um colchete faz
 * as pessoas pararem de escrever notas.
 */
function lerHistorico() {
  const arquivo = path.join(raiz, 'CHANGELOG.md');
  if (!fs.existsSync(arquivo)) return [];

  const linhas = fs.readFileSync(arquivo, 'utf8').split(/\r?\n/);
  const entradas = [];
  const avisos = [];
  let atual = null;

  for (const linha of linhas) {
    // Antes da primeira versão há duas coisas: instruções para quem ESCREVE o
    // changelog e avisos para quem o LÊ. Só o segundo interessa na página, e o
    // que separa um do outro é a citação (`>`) — por isso só ela é recolhida.
    if (!atual && /^>\s?/.test(linha)) { avisos.push(linha.replace(/^>\s?/, '')); continue; }
    const cabecalho = /^##\s+(.*)$/.exec(linha);
    if (cabecalho) {
      const titulo = cabecalho[1].trim();
      const numero = /(\d+\.\d+\.\d+)/.exec(titulo);
      if (!numero) { atual = null; continue; }

      // A data é o que sobra depois do número, sem os separadores.
      const resto = titulo.replace(/\[?\d+\.\d+\.\d+\]?/, '').replace(/^[\s\-–—]+/, '').trim();
      atual = { versao: numero[1], periodo: resto, notas: [] };
      entradas.push(atual);
      continue;
    }
    if (atual) atual.notas.push(linha);
  }

  /**
   * Commits do período declarado no cabeçalho da versão.
   *
   * As notas escritas à mão são o resumo — dizem o que mudou e por quê. Mas o
   * resumo sempre deixa coisa de fora: nesta base, 190 commits cabiam em umas
   * 60 linhas de texto, e o resto sumia. Em vez de alguém transcrever tudo (e
   * esquecer metade), o git responde.
   *
   * O período sai do próprio cabeçalho — `2026-08-06 a 2026-08-17` ou uma data
   * só. Sem data no cabeçalho, a versão fica sem detalhamento, o que é melhor
   * que adivinhar um intervalo.
   */
  for (const e of entradas) {
    const datas = e.periodo.match(/\d{4}-\d{2}-\d{2}/g) || [];
    if (datas.length) {
      const ini = datas[0];
      const fim = datas[datas.length - 1];
      const saida = git(
        `log --no-merges --reverse --pretty=format:%s`
        + ` --after="${ini} 00:00" --before="${fim} 23:59"`,
      );
      e.commits = saida ? saida.split('\n').filter(Boolean) : [];
    } else {
      e.commits = [];
    }
  }

  // Tira as linhas vazias das pontas de cada bloco; as do meio separam seções.
  for (const e of entradas) {
    while (e.notas.length && !e.notas[0].trim()) e.notas.shift();
    while (e.notas.length && !e.notas[e.notas.length - 1].trim()) e.notas.pop();
    // O separador `---` entre versões é decoração do markdown, não conteúdo.
    e.notas = e.notas.filter(l => l.trim() !== '---');
    while (e.notas.length && !e.notas[e.notas.length - 1].trim()) e.notas.pop();
  }

  return { entradas, avisos };
}

const { entradas: historico, avisos } = lerHistorico();
const atual = historico.find(e => e.versao === pkg.version);

const info = {
  versao: pkg.version,
  // ISO em UTC: o servidor pode estar em outro fuso, e comparar "quando subiu"
  // com um horário local seria comparar coisas diferentes.
  compiladoEm: new Date().toISOString(),
  // Árvore suja no momento do build. Um pacote gerado com alterações não
  // commitadas não corresponde a nenhum commit — e é exatamente o tipo de
  // build que ninguém consegue reproduzir depois.
  limpo: git('status --porcelain') === '',
  notas: atual ? atual.notas : [],
  avisos,
  historico,
};

// ── src/app/versao.ts ──────────────────────────────────────────────────────
fs.writeFileSync(
  path.join(raiz, 'src', 'app', 'versao.ts'),
  `/**
 * GERADO AUTOMATICAMENTE por \`scripts/gerar-versao.js\`. Não edite.
 * Regenerado a cada \`npm start\` e \`npm run build\`.
 */
export const VERSAO_APP = ${JSON.stringify(info, null, 2)} as const;

/** Texto curto para a tela. */
export const VERSAO_CURTA = \`v\${VERSAO_APP.versao}\`;
`,
);

const sujo = info.limpo ? '' : '  (ATENCAO: arvore com alteracoes nao commitadas)';
const semNotas = info.notas.length ? '' : `  (sem notas no CHANGELOG para ${pkg.version})`;
console.log(
  `[versao] ${pkg.version} · ${historico.length} versao(oes) no historico${sujo}${semNotas}`,
);
