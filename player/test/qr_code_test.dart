import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:midia_indoor_player/dados/banco_local.dart';
import 'package:midia_indoor_player/dados/modelos.dart';
import 'package:midia_indoor_player/telas/qr_code_sobreposto.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

Map<String, dynamic> itemJson({Map<String, dynamic>? qrCode}) => {
      'ordem': 0,
      'midiaId': 7,
      'tipo': 'Imagem',
      'tipoConteudo': 'image/png',
      'hash': 'ABC',
      'tamanhoBytes': 10,
      'duracaoSegundos': 10,
      'diasSemana': 127,
      if (qrCode != null) 'qrCode': qrCode,
    };

void main() {
  sqfliteFfiInit();

  group('modelo', () {
    test('lê o QR code do item com os nomes da API', () {
      final item = ItemGrade.fromJson(itemJson(qrCode: {
        'conteudo': 'https://wa.me/5592999990000',
        'legenda': 'Fale no WhatsApp',
        'posicao': 'SuperiorEsquerdo',
        'tamanho': 'Grande',
      }));

      expect(item.qrCode!.conteudo, 'https://wa.me/5592999990000');
      expect(item.qrCode!.legenda, 'Fale no WhatsApp');
      expect(item.qrCode!.posicao, PosicaoQrCode.superiorEsquerdo);
      expect(item.qrCode!.tamanho, TamanhoQrCode.grande);
    });

    test('sem qrCode fica nulo; posição e tamanho desconhecidos usam o padrão', () {
      expect(ItemGrade.fromJson(itemJson()).qrCode, isNull);

      final qr = QrCodeAnuncio.fromJson({'conteudo': 'tel:+559233330000', 'posicao': 'Centro', 'tamanho': null});
      expect(qr.legenda, '');
      expect(qr.posicao, PosicaoQrCode.inferiorDireito);
      expect(qr.tamanho, TamanhoQrCode.medio);
    });
  });

  group('diretório da TV', () {
    test('vem na sincronização e vira um QR pequeno no canto inferior direito', () {
      final resposta = RespostaSincronizacao.fromJson({
        'alterada': false, 'versaoGrade': 'v', 'fusoHorario': 'America/Manaus', 'intervaloSincronizacaoSegundos': 60,
        'nomeTela': 'Balcão', 'diretorio': {'url': 'https://tv.rede.test/d/ABCDEFGH23', 'legenda': 'Veja os anunciantes'},
      });
      final qr = resposta.diretorio!.qrCode;
      expect(qr.conteudo, 'https://tv.rede.test/d/ABCDEFGH23');
      expect(qr.legenda, 'Veja os anunciantes');
      expect(qr.posicao, PosicaoQrCode.inferiorDireito);
      expect(qr.tamanho, TamanhoQrCode.pequeno);
    });

    test('ausente (desligado ou API antiga) fica nulo', () {
      final resposta = RespostaSincronizacao.fromJson({
        'alterada': false, 'versaoGrade': 'v', 'fusoHorario': 'America/Manaus', 'intervaloSincronizacaoSegundos': 60, 'nomeTela': 'Balcão',
      });
      expect(resposta.diretorio, isNull);
    });
  });

  group('banco local', () {
    late Directory pasta;
    setUp(() => pasta = Directory.systemTemp.createTempSync('qr_'));
    tearDown(() => pasta.deleteSync(recursive: true));

    test('o QR code é gravado e lido com a grade', () async {
      final banco = await BancoLocal.abrir(inMemoryDatabasePath, fabrica: databaseFactoryFfiNoIsolate);
      final comQr = ItemGrade.fromJson(itemJson(qrCode: {
        'conteudo': 'https://instagram.com/padaria',
        'legenda': 'Siga no Instagram',
        'posicao': 'SuperiorDireito',
        'tamanho': 'Pequeno',
      }));
      final semQr = ItemGrade.fromJson({...itemJson(), 'ordem': 1});

      await banco.trocarGrade([comQr, semQr], 'v1');
      final grade = await banco.lerGrade();

      expect(grade[0].qrCode!.conteudo, 'https://instagram.com/padaria');
      expect(grade[0].qrCode!.legenda, 'Siga no Instagram');
      expect(grade[0].qrCode!.posicao, PosicaoQrCode.superiorDireito);
      expect(grade[0].qrCode!.tamanho, TamanhoQrCode.pequeno);
      expect(grade[1].qrCode, isNull);
      await banco.fechar();
    });

    test('banco da versão 2 ganha as colunas do QR sem perder a grade', () async {
      final arquivo = '${pasta.path}/v2.db';
      final v2 = await databaseFactoryFfiNoIsolate.openDatabase(arquivo,
          options: OpenDatabaseOptions(
            version: 2,
            onCreate: (db, _) async {
              await db.execute('CREATE TABLE estado (chave TEXT PRIMARY KEY, valor TEXT)');
              await db.execute('CREATE TABLE item_grade (ordem INTEGER PRIMARY KEY, midia_id INTEGER NOT NULL, tipo TEXT NOT NULL, '
                  'tipo_conteudo TEXT NOT NULL, hash TEXT NOT NULL, tamanho_bytes INTEGER NOT NULL, duracao_segundos INTEGER NOT NULL, '
                  'dias_semana INTEGER NOT NULL, inicio_exibicao TEXT, fim_exibicao TEXT, posicao_fixa INTEGER NOT NULL DEFAULT 0)');
              await db.execute('CREATE TABLE noticia (id INTEGER PRIMARY KEY, posicao INTEGER NOT NULL, titulo TEXT NOT NULL, '
                  'credito TEXT NOT NULL, publicada_em TEXT NOT NULL, hash TEXT, tamanho_bytes INTEGER, tipo_conteudo TEXT)');
              await db.insert('item_grade', {
                'ordem': 0, 'midia_id': 1, 'tipo': 'imagem', 'tipo_conteudo': 'image/png', 'hash': 'abc',
                'tamanho_bytes': 1, 'duracao_segundos': 10, 'dias_semana': 127, 'posicao_fixa': 1,
              });
            },
          ));
      await v2.close();

      final atualizado = await BancoLocal.abrir(arquivo, fabrica: databaseFactoryFfiNoIsolate);
      final grade = await atualizado.lerGrade();
      expect(grade.single.hash, 'abc');
      expect(grade.single.posicaoFixa, isTrue);
      expect(grade.single.qrCode, isNull);

      await atualizado.trocarGrade([
        ItemGrade.fromJson(itemJson(qrCode: {'conteudo': 'tel:+559233330000', 'legenda': 'Ligue', 'posicao': 'InferiorDireito', 'tamanho': 'Medio'})),
      ], 'v2');
      expect((await atualizado.lerGrade()).single.qrCode!.conteudo, 'tel:+559233330000');
      await atualizado.fechar();
    });
  });

  group('tela', () {
    Future<void> mostrar(WidgetTester tester, Size tamanho, QrCodeAnuncio qr) async {
      tester.view.physicalSize = tamanho;
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(MaterialApp(
        home: Scaffold(body: Stack(fit: StackFit.expand, children: [const ColoredBox(color: Colors.blue), QrCodeSobreposto(qrCode: qr)])),
      ));
    }

    const pix = '00020126580014br.gov.bcb.pix0136123e4567-e12b-12d1-a456-4266554400005204000053039865802BR5913Fulano de Tal6008BRASILIA62070503***63041D3D';

    for (final tamanho in [const Size(1920, 1080), const Size(1280, 720), const Size(800, 360), const Size(360, 740)]) {
      for (final t in TamanhoQrCode.values) {
        testWidgets('QR ${t.name} cabe em ${tamanho.width.toInt()}x${tamanho.height.toInt()}', (tester) async {
          await mostrar(tester, tamanho, QrCodeAnuncio(conteudo: pix, legenda: 'Pague com Pix com uma legenda bem longa', tamanho: t));

          expect(tester.takeException(), isNull);
          expect(find.byType(QrImageView), findsOneWidget);
          final cartao = tester.getSize(find.byKey(const ValueKey('qr-code-anuncio')));
          expect(cartao.width, closeTo(tamanho.shortestSide * QrCodeSobreposto.fracao(t), 0.5));
          expect(cartao.height, lessThan(tamanho.height));
        });
      }
    }

    testWidgets('fica no canto escolhido e mostra a legenda', (tester) async {
      await mostrar(tester, const Size(1920, 1080),
          const QrCodeAnuncio(conteudo: 'https://exemplo.com', legenda: 'Aponte a câmera', posicao: PosicaoQrCode.superiorEsquerdo));

      expect(find.text('Aponte a câmera'), findsOneWidget);
      final canto = tester.getTopLeft(find.byKey(const ValueKey('qr-code-anuncio')));
      expect(canto.dx, closeTo(1080 * 0.03, 0.5));
      expect(canto.dy, closeTo(1080 * 0.03, 0.5));
    });

    testWidgets('inferior direito encosta no canto de baixo', (tester) async {
      await mostrar(tester, const Size(1920, 1080), const QrCodeAnuncio(conteudo: 'https://exemplo.com', legenda: ''));

      final fim = tester.getBottomRight(find.byKey(const ValueKey('qr-code-anuncio')));
      expect(fim.dx, closeTo(1920 - 1080 * 0.03, 0.5));
      expect(fim.dy, closeTo(1080 - 1080 * 0.03, 0.5));
      expect(find.byType(Text), findsNothing);
    });
  });
}
