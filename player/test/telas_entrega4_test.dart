import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:midia_indoor_player/dados/modelos.dart';
import 'package:midia_indoor_player/telas/noticia_view.dart';
import 'package:midia_indoor_player/telas/sobreposicao_relogio_clima.dart';

Widget tela(Widget filho) => MaterialApp(home: Scaffold(body: SizedBox(width: 1280, height: 720, child: filho)));

void main() {
  // 14/10/2026 15:07 UTC = 11:07 em Manaus, quarta-feira.
  final agora = DateTime.utc(2026, 10, 14, 15, 7);

  Clima clima({Duration idade = const Duration(minutes: 20)}) => Clima(
        temperaturaC: 31.4,
        codigo: 2,
        descricao: 'Parcialmente nublado',
        icone: 'sol-nuvem',
        atualizadaEm: agora.subtract(idade),
        credito: 'Clima: Open-Meteo',
      );

  testWidgets('relógio na hora do estabelecimento e clima com o crédito', (tester) async {
    await tester.pumpWidget(tela(SobreposicaoRelogioClima(
      fusoHorario: 'America/Manaus',
      exibirRelogio: true,
      clima: clima(),
      agora: () => agora,
    )));

    expect(find.text('11:07'), findsOneWidget);
    expect(find.text('qua, 14 out'), findsOneWidget);
    expect(find.text('31°'), findsOneWidget);
    expect(find.text('Clima: Open-Meteo'), findsOneWidget);
    expect(find.byIcon(Icons.wb_cloudy), findsOneWidget);
  });

  testWidgets('clima velho (mais de 6 h) some; sem relógio e sem clima, nada aparece', (tester) async {
    await tester.pumpWidget(tela(SobreposicaoRelogioClima(
      fusoHorario: 'America/Manaus',
      exibirRelogio: true,
      clima: clima(idade: const Duration(hours: 7)),
      agora: () => agora,
    )));
    expect(find.text('31°'), findsNothing);
    expect(find.text('11:07'), findsOneWidget);

    await tester.pumpWidget(tela(SobreposicaoRelogioClima(
      fusoHorario: 'America/Manaus',
      exibirRelogio: false,
      clima: null,
      agora: () => agora,
    )));
    expect(find.byType(Text), findsNothing);
  });

  testWidgets('notícia sem foto mostra a faixa, o título e o crédito', (tester) async {
    await tester.pumpWidget(tela(NoticiaView(
      noticia: Noticia(id: 1, titulo: 'Chuva forte atinge o litoral', credito: 'Fonte: Agência Brasil', publicadaEm: agora),
    )));
    await tester.pump(const Duration(seconds: 1));

    expect(find.text('NOTÍCIAS'), findsOneWidget);
    expect(find.text('Chuva forte atinge o litoral'), findsOneWidget);
    expect(find.text('Fonte: Agência Brasil'), findsOneWidget);
  });
}
