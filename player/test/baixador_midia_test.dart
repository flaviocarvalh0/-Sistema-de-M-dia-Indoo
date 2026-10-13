import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:midia_indoor_player/dados/modelos.dart';
import 'package:midia_indoor_player/sincronizacao/baixador_midia.dart';

import 'servidor_falso.dart';

void main() {
  late ServidorFalso servidor;
  late Directory pasta;
  late BaixadorMidia baixador;
  final esperas = <Duration>[];

  setUp(() async {
    servidor = await ServidorFalso.iniciar();
    pasta = await Directory.systemTemp.createTemp('midias');
    esperas.clear();
    baixador = BaixadorMidia(pasta, esperar: (d) async => esperas.add(d));
    servidor.arquivos['a.png'] = List.generate(300000, (i) => i % 251);
  });

  tearDown(() async {
    await servidor.parar();
    await pasta.delete(recursive: true);
  });

  ItemGrade item(String nome) => ItemGrade.fromJson(servidor.itemJson(0, nome));

  test('baixa, confere o hash e grava com o nome do hash', () async {
    final arquivo = await baixador.garantir(item('a.png'));
    expect(arquivo.path, endsWith('${ServidorFalso.sha(servidor.arquivos['a.png']!)}.png'));
    expect(await arquivo.readAsBytes(), servidor.arquivos['a.png']);
    expect(baixador.pronto(item('a.png')), isTrue);
    expect(pasta.listSync().where((e) => e.path.endsWith('.part')), isEmpty);
  });

  test('arquivo já baixado não é baixado de novo', () async {
    await baixador.garantir(item('a.png'));
    await baixador.garantir(item('a.png'));
    expect(servidor.downloads['a.png'], 1);
  });

  test('hash errado apaga e tenta 3 vezes, com espera crescente', () async {
    servidor.corrompidos.add('a.png');
    await expectLater(baixador.garantir(item('a.png')), throwsA(isA<DownloadFalhou>()));
    expect(servidor.downloads['a.png'], 3);
    expect(esperas, [const Duration(seconds: 5), const Duration(seconds: 10)]);
    expect(pasta.listSync(), isEmpty);
  });

  test('hash certo na segunda tentativa', () async {
    servidor.corrompidos.add('a.png');
    // A rede "se recupera" durante a espera entre as tentativas.
    final b = BaixadorMidia(pasta, esperar: (_) async => servidor.corrompidos.clear());
    await b.garantir(item('a.png'));
    expect(servidor.downloads['a.png'], 2);
    expect(b.pronto(item('a.png')), isTrue);
  });

  test('URL vencida (403 do storage) não insiste', () async {
    servidor.statusDownload['a.png'] = 403;
    await expectLater(baixador.garantir(item('a.png')), throwsA(isA<UrlVencida>()));
    expect(servidor.downloads['a.png'], 1);
  });

  test('limpar apaga o que saiu da grade e sobras .part', () async {
    servidor.arquivos['b.png'] = [1, 2, 3];
    await baixador.garantir(item('a.png'));
    await baixador.garantir(item('b.png'));
    File('${pasta.path}/lixo.part').writeAsStringSync('x');
    await baixador.limpar([item('b.png')]);
    expect(baixador.pronto(item('a.png')), isFalse);
    expect(baixador.pronto(item('b.png')), isTrue);
    expect(pasta.listSync(), hasLength(1));
  });
}
