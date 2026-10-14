import 'package:flutter/material.dart';

import '../config/configuracao.dart';
import '../sincronizacao/sincronizador.dart';

/// Informações da TV (botão OK do controle): conexão, grade, notícias, clima,
/// diretório, servidor e versão, com "Parear de novo".
///
/// Cabe em qualquer tela: as linhas rolam e os botões ficam sempre visíveis
/// (num celular deitado a altura útil é pouco mais de 300 pontos).
class PainelInfo extends StatelessWidget {
  const PainelInfo({
    super.key,
    required this.situacao,
    required this.servidor,
    required this.itensHoje,
    required this.noticias,
    required this.aoParear,
    required this.aoFechar,
  });

  final SituacaoPlayer? situacao;
  final String servidor;
  final int itensHoje;
  final int noticias;
  final VoidCallback aoParear;
  final VoidCallback aoFechar;

  static String _conexao(ResultadoCiclo? r) => switch (r) {
        ResultadoCiclo.gradeTrocada || ResultadoCiclo.semMudanca => 'Conectada',
        ResultadoCiclo.downloadIncompleto => 'Conectada · baixando conteúdo',
        ResultadoCiclo.suspensa => 'Tela inativa no painel',
        ResultadoCiclo.semConexao => 'Sem conexão · tocando o que já baixou',
        ResultadoCiclo.desvinculada || ResultadoCiclo.semCredencial => 'Não pareada',
        null => 'Conectando…',
      };

  static String _clima(SituacaoPlayer? s) {
    final c = s?.clima;
    if (s == null || !s.composicao.exibirClima) return 'desligado na playlist';
    if (c == null || !c.valido(DateTime.now())) return 'sem leitura recente';
    return '${c.temperaturaC.round()}° · ${c.descricao}${c.local == null ? '' : ' · ${c.local}'}';
  }

  static String _hora(DateTime? d) =>
      d == null ? '—' : '${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}';

  @override
  Widget build(BuildContext context) {
    final s = situacao;
    final linhas = <(String, String)>[
      ('Tela', s?.nomeTela ?? '—'),
      ('Conexão', _conexao(s?.ultimoResultado)),
      ('Última sincronização', _hora(s?.ultimaSincronizacao)),
      ('Itens na grade', '${s?.grade.length ?? 0} ($itensHoje para hoje)'),
      ('Informativos', s == null || s.composicao.noticiaACadaAnuncios == 0
          ? 'desligados na playlist'
          : '1 a cada ${s.composicao.noticiaACadaAnuncios} anúncio(s): $noticias notícia(s)'
              '${s.composicao.cartaoTempo ? ' + tempo' : ''}${s.composicao.cartaoCotacoes ? ' + cotações (${s.cotacoes.length})' : ''}'),
      ('Clima', _clima(s)),
      ('Diretório', s?.diretorio?.url ?? 'desligado na tela'),
      ('Servidor', servidor),
      ('Versão do app', Configuracao.versaoApp),
    ];

    final tamanho = MediaQuery.sizeOf(context);
    final compacto = tamanho.height < 500;
    final margem = compacto ? 16.0 : 32.0;

    return ColoredBox(
      color: Colors.black54,
      child: SafeArea(
        child: Center(
          child: Padding(
            padding: EdgeInsets.all(compacto ? 8 : 24),
            child: Card(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 640),
                child: Padding(
                  padding: EdgeInsets.all(margem),
                  child: FocusTraversalGroup(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Text(
                          'Mídia Indoor',
                          style: compacto
                              ? Theme.of(context).textTheme.titleLarge
                              : Theme.of(context).textTheme.headlineMedium,
                        ),
                        SizedBox(height: compacto ? 8 : 16),
                        // As linhas rolam; título e botões ficam fixos.
                        Flexible(
                          child: SingleChildScrollView(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                for (final (rotulo, valor) in linhas)
                                  Padding(
                                    padding: EdgeInsets.symmetric(vertical: compacto ? 2 : 4),
                                    child: Row(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        SizedBox(
                                          width: compacto ? 160 : 220,
                                          child: Text(rotulo, style: const TextStyle(color: Colors.white60)),
                                        ),
                                        Expanded(child: Text(valor)),
                                      ],
                                    ),
                                  ),
                              ],
                            ),
                          ),
                        ),
                        SizedBox(height: compacto ? 12 : 24),
                        Wrap(
                          alignment: WrapAlignment.end,
                          spacing: 12,
                          runSpacing: 8,
                          children: [
                            OutlinedButton(onPressed: aoParear, child: const Text('Parear de novo')),
                            FilledButton(autofocus: true, onPressed: aoFechar, child: const Text('Fechar')),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
