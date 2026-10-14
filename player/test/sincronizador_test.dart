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

  test('sem conexão: a mensagem diz o servidor e o motivo', () async {
    final porta = Uri.parse(servidor.url).port;
    await servidor.parar();
    final api = ApiDispositivo('http://127.0.0.1:$porta');
    await expectLater(
      api.autenticar(codigo: 'AB12CD34', dispositivoId: 'd', versaoApp: '1'),
      throwsA(isA<Indisponivel>().having((e) => e.mensagem, 'mensagem',
          allOf(contains('http://127.0.0.1:$porta'), contains('conexão recusada')))),
    );
  });

  test('id do aparelho é gerado uma vez', () async {
    final c = Credenciais(CofreMemoria());
    final id = await c.dispositivoId();
    expect(await c.dispositivoId(), id);
  });

  // ---------- Entrega 4: notícias, composição e clima ----------

  group('notícias, composição e clima', () {
    late Map<String, dynamic> Function(Map<String, String>) base;
    var noticias = <Map<String, dynamic>>[];
    String versaoNoticias() => noticias.map((n) => n['id']).join(',');
    Map<String, dynamic>? clima;
    List<Map<String, dynamic>>? cotacoes;

    Map<String, dynamic> noticiaJson(int id, {String? imagem, Duration idade = const Duration(hours: 1)}) => {
          'id': id,
          'titulo': 'Notícia $id',
          'credito': 'Fonte: Teste',
          'publicadaEm': DateTime.now().toUtc().subtract(idade).toIso8601String(),
          'url': imagem == null ? null : servidor.urlArquivo(imagem),
          'hash': imagem == null ? null : ServidorFalso.sha(servidor.arquivos[imagem]!),
          'tamanhoBytes': imagem == null ? null : servidor.arquivos[imagem]!.length,
          'tipoConteudo': imagem == null ? null : 'image/jpeg',
        };

    setUp(() {
      servidor.arquivos['foto1.jpg'] = List.filled(300, 1);
      servidor.arquivos['foto2.jpg'] = List.filled(400, 2);
      noticias = [noticiaJson(2, imagem: 'foto2.jpg'), noticiaJson(1)];
      clima = {
        'temperaturaC': 31.4, 'minimaC': 24.0, 'maximaC': 33.2, 'codigo': 2, 'descricao': 'Parcialmente nublado',
        'icone': 'sol-nuvem', 'local': 'Manaus, Amazonas', 'atualizadaEm': DateTime.now().toUtc().toIso8601String(),
        'credito': 'Clima: Open-Meteo',
        'dias': [
          {'data': '2026-10-15', 'minimaC': 24, 'maximaC': 33, 'codigo': 95, 'descricao': 'Trovoadas', 'icone': 'tempestade'},
        ],
      };
      cotacoes = [
        {'moeda': 'USD', 'nome': 'Dólar', 'compra': 5.5150, 'venda': 5.5156, 'variacaoPercentual': 1.02,
         'cotadaEm': '2026-10-14T11:04:59-03:00', 'boletim': 'Intermediário', 'credito': 'Fonte: Banco Central (PTAX)'},
      ];
      base = servidor.sincronizacao!;
      servidor.sincronizacao = (q) => {
            ...base(q),
            'composicao': {
              'noticiaACadaAnuncios': 2, 'duracaoNoticiaSegundos': 12, 'embaralharAnuncios': true,
              'exibirRelogio': false, 'exibirClima': true,
            },
            'versaoNoticias': versaoNoticias(),
            'noticias': q['versaoNoticias'] == versaoNoticias() ? null : noticias,
            'clima': clima,
            'cotacoes': cotacoes,
          };
    });

    List<String> naPasta(String sub) => Directory('${pasta.path}/$sub').existsSync()
        ? (Directory('${pasta.path}/$sub').listSync().map((e) => e.path.split('/').last).toList()..sort())
        : <String>[];

    test('guarda notícias (imagem em pasta própria), composição e clima', () async {
      expect(await sync.sincronizar(), ResultadoCiclo.gradeTrocada);

      final s = sync.situacao.value!;
      expect(s.noticias.map((n) => n.id), [2, 1]);
      expect(s.noticias.first.temImagem, isTrue);
      expect(s.composicao.noticiaACadaAnuncios, 2);
      expect(s.composicao.embaralharAnuncios, isTrue);
      expect(s.composicao.exibirRelogio, isFalse);
      expect(s.clima!.temperaturaC, 31.4);
      expect(s.clima!.icone, 'sol-nuvem');
      expect(naPasta('noticias'), ['${ServidorFalso.sha(servidor.arquivos['foto2.jpg']!)}.jpg']);
      expect(naPasta('midias'), hasLength(2), reason: 'a limpeza das mídias não apaga as imagens das notícias');

      // Segunda vez: manda a versão das notícias e não baixa nada.
      expect(await sync.sincronizar(), ResultadoCiclo.semMudanca);
      expect(servidor.ultimaQuery['versaoNoticias'], versaoNoticias());
      expect(servidor.downloads['foto2.jpg'], 1);
    });

    test('notícias novas trocam a lista e apagam a imagem que saiu', () async {
      await sync.sincronizar();
      noticias = [noticiaJson(3, imagem: 'foto1.jpg')];

      expect(await sync.sincronizar(), ResultadoCiclo.semMudanca, reason: 'a grade de anúncios não mudou');
      expect(sync.situacao.value!.noticias.map((n) => n.id), [3]);
      expect(naPasta('noticias'), ['${ServidorFalso.sha(servidor.arquivos['foto1.jpg']!)}.jpg']);
    });

    test('imagem de notícia que falha: fica de fora e o próximo ciclo pede de novo', () async {
      servidor.statusDownload['foto2.jpg'] = 403;

      expect(await sync.sincronizar(), ResultadoCiclo.downloadIncompleto);
      expect(sync.situacao.value!.grade, hasLength(2), reason: 'a grade de anúncios entra mesmo assim');
      expect(sync.situacao.value!.noticias.map((n) => n.id), [1]);

      servidor.statusDownload.clear();
      expect(await sync.sincronizar(), ResultadoCiclo.semMudanca);
      expect(servidor.ultimaQuery['versaoNoticias'], '', reason: 'versão não gravada: pede tudo de novo');
      expect(sync.situacao.value!.noticias.map((n) => n.id), [2, 1]);
    });

    test('guarda a previsão dos próximos dias e as cotações', () async {
      await sync.sincronizar();
      final s = sync.situacao.value!;
      expect(s.clima!.dias.single.icone, 'tempestade');
      expect(s.clima!.dias.single.data, DateTime(2026, 10, 15));
      expect(s.cotacoes.single.venda, 5.5156);
      expect(s.cotacoes.single.variacaoPercentual, 1.02);

      cotacoes = null;
      await sync.sincronizar();
      expect(sync.situacao.value!.cotacoes, isEmpty, reason: 'cartão desligado: some');
    });

    test('clima nulo na resposta apaga o guardado', () async {
      await sync.sincronizar();
      clima = null;
      await sync.sincronizar();
      expect(sync.situacao.value!.clima, isNull);
    });

    test('API antiga (sem os campos novos): composição padrão, sem notícias', () async {
      servidor.sincronizacao = base;
      expect(await sync.sincronizar(), ResultadoCiclo.gradeTrocada);
      final s = sync.situacao.value!;
      expect(s.composicao.noticiaACadaAnuncios, 0);
      expect(s.composicao.exibirRelogio, isTrue);
      expect(s.noticias, isEmpty);
      expect(s.clima, isNull);
    });
  });

  test('banco da versão 1 é atualizado sem perder a grade', () async {
    final arquivo = '${pasta.path}/v1.db';
    final v1 = await databaseFactoryFfiNoIsolate.openDatabase(arquivo,
        options: OpenDatabaseOptions(
          version: 1,
          onCreate: (db, _) async {
            await db.execute('CREATE TABLE estado (chave TEXT PRIMARY KEY, valor TEXT)');
            await db.execute('CREATE TABLE item_grade (ordem INTEGER PRIMARY KEY, midia_id INTEGER NOT NULL, tipo TEXT NOT NULL, '
                'tipo_conteudo TEXT NOT NULL, hash TEXT NOT NULL, tamanho_bytes INTEGER NOT NULL, duracao_segundos INTEGER NOT NULL, '
                'dias_semana INTEGER NOT NULL, inicio_exibicao TEXT, fim_exibicao TEXT)');
            await db.insert('item_grade', {
              'ordem': 0, 'midia_id': 1, 'tipo': 'imagem', 'tipo_conteudo': 'image/png', 'hash': 'abc',
              'tamanho_bytes': 1, 'duracao_segundos': 10, 'dias_semana': 127,
            });
          },
        ));
    await v1.close();

    final atualizado = await BancoLocal.abrir(arquivo, fabrica: databaseFactoryFfiNoIsolate);
    final grade = await atualizado.lerGrade();
    expect(grade.single.hash, 'abc');
    expect(grade.single.posicaoFixa, isFalse);
    expect(await atualizado.lerNoticias(), isEmpty);
    expect((await atualizado.lerEstado()).composicao.exibirRelogio, isTrue);
    await atualizado.fechar();
  });
}
