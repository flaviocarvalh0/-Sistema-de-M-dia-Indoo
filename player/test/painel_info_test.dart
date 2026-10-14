import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:midia_indoor_player/dados/modelos.dart';
import 'package:midia_indoor_player/sincronizacao/sincronizador.dart';
import 'package:midia_indoor_player/telas/painel_info.dart';

void main() {
  final situacao = SituacaoPlayer(
    grade: const [],
    fusoHorario: 'America/Manaus',
    nomeTela: 'Recepção com um nome bem comprido para ocupar espaço',
    ultimoResultado: ResultadoCiclo.semMudanca,
    ultimaSincronizacao: DateTime(2026, 10, 14, 11, 7),
    composicao: const Composicao(noticiaACadaAnuncios: 2),
  );

  for (final (nome, tamanho) in [
    ('celular deitado', const Size(740, 360)),
    ('celular em pé', const Size(360, 740)),
    ('TV 1080p', const Size(1920, 1080)),
  ]) {
    testWidgets('cabe na tela e os botões funcionam: $nome', (tester) async {
      tester.view.physicalSize = tamanho;
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      var pareou = false;
      var fechou = false;

      await tester.pumpWidget(MaterialApp(
        theme: ThemeData(brightness: Brightness.dark, useMaterial3: true),
        home: Scaffold(
          body: PainelInfo(
            situacao: situacao,
            servidor: 'https://192.168.1.11:7216',
            itensHoje: 3,
            noticias: 12,
            aoParear: () => pareou = true,
            aoFechar: () => fechou = true,
          ),
        ),
      ));

      expect(tester.takeException(), isNull, reason: 'sem "bottom overflowed"');
      await tester.tap(find.text('Parear de novo'));
      await tester.tap(find.text('Fechar'));
      expect(pareou, isTrue);
      expect(fechou, isTrue);
    });
  }
}
