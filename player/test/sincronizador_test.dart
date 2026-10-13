import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:midia_indoor_player/dados/api_dispositivo.dart';
import 'package:midia_indoor_player/dados/banco_local.dart';
import 'package:midia_indoor_player/dados/credenciais.dart';
import 'package:midia_indoor_player/sincronizacao/baixador_midia.dart';
import 'package:midia_indoor_player/sincronizacao/sincronizador.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

import 'servidor_falso.dart';

class CofreMemoria implements Cofre {
  final dados = <String, String>{};

  @override
  Future<String?> ler(String chave) async => dados[chave];

  @override
  Future<void> gravar(String chave, String valor) async => dados[chave] = valor;

  @override
  Future<void> apagar(String chave) async => dados.remove(chave);
}

void main() {
  sqfliteFfiInit();

  late ServidorFalso servidor;
  late Directory pasta;
  late BancoLocal banco;
  late CofreMemoria cofre;
  late Sincronizador sync;

  /// Grade "do servidor": nomes de arquivo na ordem.
  var grade = <String>[];
  String versao() => grade.join('|');

  setUp(() async {
    servidor = await ServidorFalso.iniciar();
    pasta = await Directory.systemTemp.createTemp('player');
    banco = await BancoLocal.abrir(inMemoryDatabasePath, fabrica: databaseFactoryFfiNoIsolate);
    cofre = CofreMemoria()..dados['token'] = 'token-1';
    sync = Sincronizador(
      api: ApiDispositivo(servidor.url),
      banco: banco,
      credenciais: Credenciais(cofre),
      baixador: BaixadorMidia(Directory('${pasta.path}/midias'), esperar: (_) async {}),
      versaoApp: '1.0.0',
    );

    servidor.arquivos
      ..['video.mp4'] = List.filled(5000, 7)
      ..['aviso.png'] = List.filled(800, 9)
      ..['promo.png'] = List.filled(900, 3);
    grade = ['video.mp4', 'aviso.png'];
    servidor.sincronizacao = (q) {
      final mudou = q['versaoGrade'] != versao();
      return {
        'alterada': mudou,
        'versaoGrade': versao(),
        'geradoEm': '2026-10-13T21:21:47Z',
        'hoje': '2026-10-13',
        'fusoHorario': 'America/Manaus',
        'intervaloSincronizacaoSegundos': 120,
        'nomeTela': 'Recepção',
        'novoToken': null,
        'itens': mudou
            ? [
                for (var i = 0; i < grade.length; i++)
                  servidor.itemJson(i, grade[i], tipo: grade[i].endsWith('.mp4') ? 'Video' : 'Imagem'),
              ]
            : null,
      };
    };
  });

  tearDown(() async {
    await banco.fechar();
    await servidor.parar();
    await pasta.delete(recursive: true);
  });

  List<String> arquivosNaPasta() =>
      Directory('${pasta.path}/midias').listSync().map((e) => e.path.split('/').last).toList()..sort();

  test('primeira sincronização baixa tudo e troca a grade', () async {
    expect(await sync.sincronizar(), ResultadoCiclo.gradeTrocada);

    final s = sync.situacao.value!;
    expect(s.grade.map((i) => i.ordem), [0, 1]);
    expect(s.fusoHorario, 'America/Manaus');
    expect(s.nomeTela, 'Recepção');
    expect(sync.intervalo, const Duration(seconds: 120));
    expect(arquivosNaPasta(), hasLength(2));
    expect(servidor.ultimoToken, 'Bearer token-1');
    expect(servidor.ultimaQuery['versaoApp'], '1.0.0');
    expect(servidor.ultimaQuery.containsKey('versaoGrade'), isFalse);
  });

  test('sem mudança: manda a versão e os itens prontos e não baixa nada', () async {
    await sync.sincronizar();
    expect(await sync.sincronizar(), ResultadoCiclo.semMudanca);
    expect(servidor.ultimaQuery['versaoGrade'], versao());
    expect(servidor.ultimaQuery['itensProntos'], '2');
    expect(servidor.downloads.values, everyElement(1));
  });

  test('grade nova: reaproveita o que já tem e apaga o que saiu', () async {
    await sync.sincronizar();
    grade = ['promo.png', 'video.mp4'];
    expect(await sync.sincronizar(), ResultadoCiclo.gradeTrocada);

    expect(servidor.downloads['video.mp4'], 1);
    expect(servidor.downloads['promo.png'], 1);
    final hashes = sync.situacao.value!.grade.map((i) => i.hash).toSet();
    expect(arquivosNaPasta().map((n) => n.split('.').first).toSet(), hashes);
  });

  test('download que falha mantém a grade antiga tocando', () async {
    await sync.sincronizar();
    final antiga = sync.situacao.value!.grade.map((i) => i.hash).toList();

    grade = ['promo.png'];
    servidor.corrompidos.add('promo.png');
    expect(await sync.sincronizar(), ResultadoCiclo.downloadIncompleto);
    expect(sync.situacao.value!.grade.map((i) => i.hash).toList(), antiga);
    expect(arquivosNaPasta(), hasLength(2));

    // Próximo ciclo com o arquivo bom: troca.
    servidor.corrompidos.clear();
    expect(await sync.sincronizar(), ResultadoCiclo.gradeTrocada);
    expect(sync.situacao.value!.grade, hasLength(1));
    expect(arquivosNaPasta(), hasLength(1));
  });

  test('URL vencida: grade antiga continua e o próximo ciclo pede de novo', () async {
    await sync.sincronizar();
    grade = ['promo.png'];
    servidor.statusDownload['promo.png'] = 400;
    expect(await sync.sincronizar(), ResultadoCiclo.downloadIncompleto);
    expect(sync.situacao.value!.grade, hasLength(2));
    servidor.statusDownload.clear();
    expect(await sync.sincronizar(), ResultadoCiclo.gradeTrocada);
  });

  test('401: apaga o token mas mantém grade e arquivos', () async {
    await sync.sincronizar();
    servidor.statusSincronizacao = 401;
    expect(await sync.sincronizar(), ResultadoCiclo.desvinculada);
    expect(cofre.dados.containsKey('token'), isFalse);
    expect(sync.situacao.value!.grade, hasLength(2));
    expect(sync.situacao.value!.desvinculada, isTrue);
    expect(arquivosNaPasta(), hasLength(2));
    expect(await sync.sincronizar(), ResultadoCiclo.semCredencial);
  });

  test('403 e servidor fora do ar: continua tocando', () async {
    await sync.sincronizar();
    servidor.statusSincronizacao = 403;
    expect(await sync.sincronizar(), ResultadoCiclo.suspensa);
    servidor.statusSincronizacao = 503;
    expect(await sync.sincronizar(), ResultadoCiclo.semConexao);
    await servidor.parar();
    expect(await sync.sincronizar(), ResultadoCiclo.semConexao);
    expect(sync.situacao.value!.grade, hasLength(2));
    expect(cofre.dados['token'], 'token-1');
  });

  test('novoToken substitui o token guardado', () async {
    final original = servidor.sincronizacao!;
    servidor.sincronizacao = (q) => {...original(q), 'novoToken': 'token-2'};
    await sync.sincronizar();
    expect(cofre.dados['token'], 'token-2');
  });

  test('grade vazia apaga os arquivos', () async {
    await sync.sincronizar();
    grade = [];
    expect(await sync.sincronizar(), ResultadoCiclo.gradeTrocada);
    expect(sync.situacao.value!.grade, isEmpty);
    expect(arquivosNaPasta(), isEmpty);
  });

  test('pareamento normaliza o código e devolve o token', () async {
    servidor.codigos['AB12CD34'] = 'token-novo';
    final api = ApiDispositivo(servidor.url);
    final p = await api.autenticar(codigo: ' ab12-cd34 ', dispositivoId: 'disp-1', versaoApp: '1.0.0');
    expect(p.token, 'token-novo');
    expect(p.nomeTela, 'Recepção');
    expect(servidor.ultimoPareamento, {'codigoPareamento': 'AB12CD34', 'dispositivoId': 'disp-1', 'versaoApp': '1.0.0'});
    await expectLater(
      api.autenticar(codigo: 'ZZZZ9999', dispositivoId: 'disp-1', versaoApp: '1.0.0'),
      throwsA(isA<CodigoRecusado>()),
    );
  });

  test('id do aparelho é gerado uma vez', () async {
    final c = Credenciais(CofreMemoria());
    final id = await c.dispositivoId();
    expect(await c.dispositivoId(), id);
  });
}
