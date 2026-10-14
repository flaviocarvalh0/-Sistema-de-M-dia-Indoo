import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:midia_indoor_player/dados/modelos.dart';
import 'package:midia_indoor_player/telas/cartoes_view.dart';

void main() {
  final hoje = DateTime.utc(2026, 10, 14);
  final clima = Clima(
    temperaturaC: 31.4,
    minimaC: 24,
    maximaC: 33,
    codigo: 80,
    descricao: 'Pancadas de chuva fracas',
    icone: 'chuva',
    local: 'Manaus, Amazonas',
    atualizadaEm: DateTime.utc(2026, 10, 14, 14),
    credito: 'Clima: Open-Meteo',
    dias: [
      DiaPrevisao(data: DateTime(2026, 10, 14), minimaC: 24.2, maximaC: 33.6, descricao: 'x', icone: 'chuva'),
      DiaPrevisao(data: DateTime(2026, 10, 15), minimaC: 23, maximaC: 32, descricao: 'x', icone: 'sol'),
      DiaPrevisao(data: DateTime(2026, 10, 16), minimaC: 22, maximaC: 31, descricao: 'x', icone: 'nuvem'),
      DiaPrevisao(data: DateTime(2026, 10, 17), minimaC: 21, maximaC: 30, descricao: 'x', icone: 'tempestade'),
    ],
  );
  final cotacoes = [
    Cotacao(moeda: 'USD', nome: 'Dólar', compra: 5.515, venda: 5.5156, variacaoPercentual: 1.02,
        cotadaEm: DateTime.parse('2026-10-14T11:04:59-03:00'), credito: 'Fonte: Banco Central (PTAX)'),
    Cotacao(moeda: 'EUR', nome: 'Euro', compra: 6.1, venda: 6.12, variacaoPercentual: -0.35,
        cotadaEm: DateTime.parse('2026-10-14T11:04:59-03:00'), credito: 'Fonte: Banco Central (PTAX)'),
  ];

  for (final (nome, tamanho) in [
    ('celular deitado', const Size(740, 360)),
    ('celular em pé', const Size(360, 740)),
    ('TV 1080p', const Size(1920, 1080)),
  ]) {
    testWidgets('cartão do tempo: agora, hoje e 3 dias ($nome)', (tester) async {
      tester.view.physicalSize = tamanho;
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(MaterialApp(home: CartaoTempoView(clima: clima, hoje: hoje)));
      await tester.pump(const Duration(seconds: 1));

      expect(tester.takeException(), isNull, reason: 'sem overflow');
      expect(find.text('31°'), findsOneWidget);
      expect(find.text('Mín 24° · Máx 34°'), findsOneWidget);
      expect(find.text('Qui'), findsOneWidget);
      expect(find.text('Sáb'), findsOneWidget);
      expect(find.text('Clima: Open-Meteo'), findsOneWidget);
    });

    testWidgets('cartão de cotações: venda e variação ($nome)', (tester) async {
      tester.view.physicalSize = tamanho;
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(MaterialApp(home: CartaoCotacoesView(cotacoes: cotacoes)));
      await tester.pump(const Duration(seconds: 1));

      expect(tester.takeException(), isNull, reason: 'sem overflow');
      expect(find.text(r'R$ 5,52'), findsOneWidget);
      expect(find.text(r'R$ 6,12'), findsOneWidget);
      expect(find.text('+1,02%'), findsOneWidget);
      expect(find.text('−0,35%'), findsOneWidget);
      expect(find.byIcon(Icons.arrow_upward), findsOneWidget);
      expect(find.text('Fonte: Banco Central (PTAX) · 14/10 às 11:04'), findsOneWidget);
    });
  }
}
