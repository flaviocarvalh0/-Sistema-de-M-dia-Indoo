import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:midia_indoor_player/dados/api_dispositivo.dart';
import 'package:midia_indoor_player/dados/banco_local.dart';
import 'package:midia_indoor_player/dados/credenciais.dart';
import 'package:midia_indoor_player/dados/modelos.dart';
import 'package:midia_indoor_player/sincronizacao/baixador_midia.dart';
import 'package:midia_indoor_player/sincronizacao/registrador_exibicoes.dart';
import 'package:midia_indoor_player/sincronizacao/sincronizador.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

import 'servidor_falso.dart';
import 'sincronizador_test.dart' show CofreMemoria;

void main() {
  sqfliteFfiInit();

  late ServidorFalso servidor;
  late Directory pasta;
  late BancoLocal banco;
  late CofreMemoria cofre;
  late DateTime agora;
  late RegistradorExibicoes registrador;
  late Sincronizador sync;

  setUp(() async {
    servidor = await ServidorFalso.iniciar();
    pasta = await Directory.systemTemp.createTemp('exibicoes');
    banco = await BancoLocal.abrir(inMemoryDatabasePath, fabrica: databaseFactoryFfiNoIsolate);
    cofre = CofreMemoria()..dados['token'] = 'token-1';
    agora = DateTime.utc(2026, 10, 12, 10);
    servidor.sincronizacao = (q) => {
          'alterada': false,
          'versaoGrade': 'v',
          'geradoEm': '2026-10-12T10:00:00Z',
          'hoje': '2026-10-12',
          'fusoHorario': 'America/Manaus',
          'intervaloSincronizacaoSegundos': 60,
          'nomeTela': 'Recepção',
          'novoToken': null,
          'itens': null,
        };
    late Sincronizador s;
    registrador = RegistradorExibicoes(banco: banco, semConexao: () => s.semConexao, agora: () => agora);
    s = Sincronizador(
      api: ApiDispositivo(servidor.url),
      banco: banco,
      credenciais: Credenciais(cofre),
      baixador: BaixadorMidia(Directory('${pasta.path}/midias'), esperar: (_) async {}),
      versaoApp: '1.0.0',
      registrador: registrador,
    );
    sync = s;
  });

  tearDown(() async {
    await banco.fechar();
    await servidor.parar();
    await pasta.delete(recursive: true);
  });

  Future<void> exibir(int midiaId, {bool concluida = true}) => registrador.registrar(
        midiaId: midiaId,
        inicio: agora,
        duracao: const Duration(seconds: 15),
        concluida: concluida,
      );

  test('grava, envia na sincronização com o token e apaga o que foi', () async {
    await exibir(7);
    await exibir(8, concluida: false);

    await sync.sincronizar();

    expect(servidor.lotesExibicoes, hasLength(1));
    final lote = servidor.lotesExibicoes.single;
    expect(lote.map((e) => e['midiaId']), [7, 8]);
    expect(lote[0]['duracaoMs'], 15000);
    expect(lote[1]['concluida'], isFalse);
    expect(lote[0]['inicio'], '2026-10-12T10:00:00.000Z');
    expect(lote[0]['id'], matches(RegExp(r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$')));
    expect(servidor.ultimoToken, 'Bearer token-1');
    expect(await banco.contarExibicoesPendentes(), 0);
  });

  test('sem internet: marca como sem conexão e guarda até a API voltar', () async {
    servidor.statusSincronizacao = 503;
    await sync.sincronizar();
    expect(sync.semConexao, isTrue);
    await exibir(7);
    expect(servidor.lotesExibicoes, isEmpty);
    expect(await banco.contarExibicoesPendentes(), 1);

    servidor.statusSincronizacao = 200;
    await sync.sincronizar();
    expect(servidor.lotesExibicoes.single.single['semConexao'], isTrue);
    expect(await banco.contarExibicoesPendentes(), 0);
  });

  test('envia no máximo a cada 15 min, em lotes de 500', () async {
    await exibir(1);
    await sync.sincronizar();
    expect(servidor.lotesExibicoes, hasLength(1));

    await exibir(2);
    agora = agora.add(const Duration(minutes: 5));
    await sync.sincronizar();
    expect(servidor.lotesExibicoes, hasLength(1), reason: 'ainda não deu o intervalo');

    for (var i = 0; i < 600; i++) {
      await exibir(i);
    }
    agora = agora.add(const Duration(minutes: 1));
    await sync.sincronizar();
    expect(servidor.lotesExibicoes.skip(1).map((l) => l.length), [500, 101], reason: 'acumulou: envia já, em lotes');
    expect(await banco.contarExibicoesPendentes(), 0);
  });

  test('API recusa: mantém para a próxima', () async {
    await exibir(1);
    servidor.statusExibicoes = 500;
    await sync.sincronizar();
    expect(await banco.contarExibicoesPendentes(), 1);

    servidor.statusExibicoes = 200;
    await sync.sincronizar();
    expect(servidor.lotesExibicoes.single, hasLength(1));
    expect(await banco.contarExibicoesPendentes(), 0);
  });

  test('banco guarda no máximo o limite, descartando as mais antigas', () async {
    for (var i = 0; i < 5; i++) {
      await banco.registrarExibicao(
        Exibicao(id: 'id$i', midiaId: i, inicio: agora.add(Duration(minutes: i)), duracao: Duration.zero, concluida: true, semConexao: false),
        maximoExibicoes: 3,
      );
    }
    expect((await banco.exibicoesPendentes()).map((e) => e.midiaId), [2, 3, 4]);

    await banco.limpar();
    expect(await banco.contarExibicoesPendentes(), 0, reason: 'parear com outra tela descarta as da anterior');
  });
}
