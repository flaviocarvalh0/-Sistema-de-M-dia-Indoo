import 'dart:convert';

import 'package:sqflite/sqflite.dart';

import 'modelos.dart';

/// Grade em uso, notícias, composição, clima e estado da última
/// sincronização, no SQLite do aparelho.
///
/// Guarda só a grade que está TOCANDO: a nova fica em memória enquanto os
/// arquivos baixam e entra aqui de uma vez, numa transação ([trocarGrade]).
/// As notícias seguem a mesma regra ([trocarNoticias]).
class BancoLocal {
  BancoLocal._(this._db);

  final Database _db;

  static Future<BancoLocal> abrir(String caminho, {DatabaseFactory? fabrica}) async {
    final db = await (fabrica ?? databaseFactory).openDatabase(
      caminho,
      options: OpenDatabaseOptions(
        version: 4,
        onCreate: (db, _) async {
          await db.execute('CREATE TABLE estado (chave TEXT PRIMARY KEY, valor TEXT)');
          await db.execute('''
            CREATE TABLE item_grade (
              ordem INTEGER PRIMARY KEY,
              midia_id INTEGER NOT NULL,
              tipo TEXT NOT NULL,
              tipo_conteudo TEXT NOT NULL,
              hash TEXT NOT NULL,
              tamanho_bytes INTEGER NOT NULL,
              duracao_segundos INTEGER NOT NULL,
              dias_semana INTEGER NOT NULL,
              inicio_exibicao TEXT,
              fim_exibicao TEXT,
              posicao_fixa INTEGER NOT NULL DEFAULT 0
            )''');
          await _criarNoticias(db);
          await _criarQrCode(db);
          await _criarExibicoes(db);
        },
        // v2 (Entrega 4): posição fixa, notícias, composição e clima.
        onUpgrade: (db, de, para) async {
          if (de < 2) {
            await db.execute('ALTER TABLE item_grade ADD COLUMN posicao_fixa INTEGER NOT NULL DEFAULT 0');
            await _criarNoticias(db);
          }
          // v3: QR code por cima da mídia.
          if (de < 3) await _criarQrCode(db);
          // v4: exibições registradas para a prova de veiculação (ADR-023).
          if (de < 4) await _criarExibicoes(db);
        },
      ),
    );
    return BancoLocal._(db);
  }

  static Future<void> _criarNoticias(DatabaseExecutor db) => db.execute('''
        CREATE TABLE noticia (
          id INTEGER PRIMARY KEY,
          posicao INTEGER NOT NULL,
          titulo TEXT NOT NULL,
          credito TEXT NOT NULL,
          publicada_em TEXT NOT NULL,
          hash TEXT,
          tamanho_bytes INTEGER,
          tipo_conteudo TEXT
        )''');

  static Future<void> _criarQrCode(DatabaseExecutor db) async {
    for (final coluna in ['qr_conteudo TEXT', 'qr_legenda TEXT', 'qr_posicao TEXT', 'qr_tamanho TEXT']) {
      await db.execute('ALTER TABLE item_grade ADD COLUMN $coluna');
    }
  }

  static Future<void> _criarExibicoes(DatabaseExecutor db) => db.execute('''
        CREATE TABLE exibicao (
          id TEXT PRIMARY KEY,
          midia_id INTEGER NOT NULL,
          inicio TEXT NOT NULL,
          duracao_ms INTEGER NOT NULL,
          concluida INTEGER NOT NULL,
          sem_conexao INTEGER NOT NULL
        )''');

  Future<void> fechar() => _db.close();

  /// Guarda uma exibição até ser enviada. Com mais de [maximoExibicoes]
  /// pendentes (TV semanas sem internet), apaga as mais antigas: a API não
  /// aceita exibições com mais de 35 dias.
  Future<void> registrarExibicao(Exibicao e, {int maximoExibicoes = 20000}) => _db.transaction((txn) async {
        await txn.insert('exibicao', e.paraLinha(), conflictAlgorithm: ConflictAlgorithm.ignore);
        final total = Sqflite.firstIntValue(await txn.rawQuery('SELECT COUNT(*) FROM exibicao')) ?? 0;
        if (total > maximoExibicoes) {
          await txn.rawDelete(
            'DELETE FROM exibicao WHERE id IN (SELECT id FROM exibicao ORDER BY inicio LIMIT ?)',
            [total - maximoExibicoes],
          );
        }
      });

  /// As mais antigas primeiro.
  Future<List<Exibicao>> exibicoesPendentes({int limite = 500}) async =>
      (await _db.query('exibicao', orderBy: 'inicio', limit: limite)).map(Exibicao.daLinha).toList();

  Future<int> contarExibicoesPendentes() async =>
      Sqflite.firstIntValue(await _db.rawQuery('SELECT COUNT(*) FROM exibicao')) ?? 0;

  Future<void> apagarExibicoes(Iterable<String> ids) => _db.transaction((txn) async {
        for (final id in ids) {
          await txn.delete('exibicao', where: 'id = ?', whereArgs: [id]);
        }
      });

  Future<EstadoLocal> lerEstado() async {
    final linhas = await _db.query('estado');
    final mapa = {for (final l in linhas) l['chave'] as String: l['valor'] as String?};
    return EstadoLocal(
      versaoGrade: mapa['versaoGrade'],
      fusoHorario: mapa['fusoHorario'] ?? 'America/Sao_Paulo',
      nomeTela: mapa['nomeTela'],
      intervaloSincronizacaoSegundos: int.tryParse(mapa['intervalo'] ?? '') ?? 60,
      versaoNoticias: mapa['versaoNoticias'],
      composicao: _json(mapa['composicao'], Composicao.fromJson) ?? const Composicao(),
      clima: _json(mapa['clima'], Clima.fromJson),
      cotacoes: _lista(mapa['cotacoes'], Cotacao.fromJson),
      diretorio: _json(mapa['diretorio'], DiretorioTv.fromJson),
    );
  }

  /// [clima] nulo apaga o guardado (a playlist desligou ou não há leitura recente).
  Future<void> gravarEstado({
    String? fusoHorario,
    String? nomeTela,
    int? intervaloSincronizacaoSegundos,
    Composicao? composicao,
    bool gravarClima = false,
    Clima? clima,
    List<Cotacao>? cotacoes,
    DiretorioTv? diretorio,
  }) =>
      _db.transaction((txn) async {
        if (fusoHorario != null) await _gravar(txn, 'fusoHorario', fusoHorario);
        if (nomeTela != null) await _gravar(txn, 'nomeTela', nomeTela);
        if (intervaloSincronizacaoSegundos != null) {
          await _gravar(txn, 'intervalo', '$intervaloSincronizacaoSegundos');
        }
        if (composicao != null) await _gravar(txn, 'composicao', jsonEncode(composicao.toJson()));
        // Cotações: a resposta sempre traz a lista quando o cartão está ligado; nula, apaga.
        if (gravarClima) {
          if (cotacoes == null) {
            await txn.delete('estado', where: 'chave = ?', whereArgs: ['cotacoes']);
          } else {
            await _gravar(txn, 'cotacoes', jsonEncode([for (final c in cotacoes) c.toJson()]));
          }
        }
        // Diretório: vem em toda resposta 200 quando ligado; nulo, apaga (desligado na tela).
        if (gravarClima) {
          if (diretorio == null) {
            await txn.delete('estado', where: 'chave = ?', whereArgs: ['diretorio']);
          } else {
            await _gravar(txn, 'diretorio', jsonEncode(diretorio.toJson()));
          }
        }
        if (gravarClima) {
          if (clima == null) {
            await txn.delete('estado', where: 'chave = ?', whereArgs: ['clima']);
          } else {
            await _gravar(txn, 'clima', jsonEncode(clima.toJson()));
          }
        }
      });

  /// Notícias na ordem recebida (mais recentes primeiro).
  Future<List<Noticia>> lerNoticias() async {
    final linhas = await _db.query('noticia', orderBy: 'posicao');
    return linhas
        .map((l) => Noticia(
              id: l['id'] as int,
              titulo: l['titulo'] as String,
              credito: l['credito'] as String,
              publicadaEm: DateTime.parse(l['publicada_em'] as String),
              hashImagem: l['hash'] as String?,
              tamanhoBytes: l['tamanho_bytes'] as int?,
              tipoConteudo: l['tipo_conteudo'] as String?,
            ))
        .toList();
  }

  /// Troca as notícias e a versão delas de uma vez.
  Future<void> trocarNoticias(List<Noticia> noticias, String versao) => _db.transaction((txn) async {
        await txn.delete('noticia');
        final lote = txn.batch();
        for (var i = 0; i < noticias.length; i++) {
          final n = noticias[i];
          lote.insert('noticia', {
            'id': n.id,
            'posicao': i,
            'titulo': n.titulo,
            'credito': n.credito,
            'publicada_em': n.publicadaEm.toUtc().toIso8601String(),
            'hash': n.hashImagem,
            'tamanho_bytes': n.tamanhoBytes,
            'tipo_conteudo': n.tipoConteudo,
          });
        }
        await lote.commit(noResult: true);
        await _gravar(txn, 'versaoNoticias', versao);
      });

  /// Esquece a versão das notícias: a próxima sincronização manda todas, com URLs novas.
  Future<void> esquecerVersaoNoticias() => _db.delete('estado', where: 'chave = ?', whereArgs: ['versaoNoticias']);

  static List<T> _lista<T>(String? texto, T Function(Map<String, dynamic>) ler) {
    if (texto == null) return const [];
    try {
      return (jsonDecode(texto) as List<dynamic>).map((e) => ler(e as Map<String, dynamic>)).toList();
    } on Object {
      return const [];
    }
  }

  static T? _json<T>(String? texto, T Function(Map<String, dynamic>) ler) {
    if (texto == null) return null;
    try {
      return ler(jsonDecode(texto) as Map<String, dynamic>);
    } on Object {
      return null;
    }
  }

  Future<List<ItemGrade>> lerGrade() async {
    final linhas = await _db.query('item_grade', orderBy: 'ordem');
    return linhas.map(_paraItem).toList();
  }

  /// Troca a grade inteira e a versão de uma vez: ou entra tudo, ou nada.
  Future<void> trocarGrade(List<ItemGrade> itens, String versaoGrade) => _db.transaction((txn) async {
        await txn.delete('item_grade');
        final lote = txn.batch();
        for (final i in itens) {
          lote.insert('item_grade', _paraLinha(i));
        }
        await lote.commit(noResult: true);
        await _gravar(txn, 'versaoGrade', versaoGrade);
      });

  /// Esquece a versão: a próxima sincronização recebe a grade completa, com
  /// URLs novas (usado quando um download falha).
  Future<void> esquecerVersao() => _db.delete('estado', where: 'chave = ?', whereArgs: ['versaoGrade']);

  /// Apaga grade e estado (usado ao parear com outra tela).
  Future<void> limpar() => _db.transaction((txn) async {
        await txn.delete('item_grade');
        await txn.delete('noticia');
        await txn.delete('estado');
        // As da tela anterior não podem ir com o token da nova.
        await txn.delete('exibicao');
      });

  static Future<void> _gravar(DatabaseExecutor db, String chave, String valor) =>
      db.insert('estado', {'chave': chave, 'valor': valor}, conflictAlgorithm: ConflictAlgorithm.replace);

  static Map<String, Object?> _paraLinha(ItemGrade i) => {
        'ordem': i.ordem,
        'midia_id': i.midiaId,
        'tipo': i.tipo.name,
        'tipo_conteudo': i.tipoConteudo,
        'hash': i.hash,
        'tamanho_bytes': i.tamanhoBytes,
        'duracao_segundos': i.duracaoSegundos,
        'dias_semana': i.diasSemana,
        'posicao_fixa': i.posicaoFixa ? 1 : 0,
        'qr_conteudo': i.qrCode?.conteudo,
        'qr_legenda': i.qrCode?.legenda,
        'qr_posicao': i.qrCode?.posicao.name,
        'qr_tamanho': i.qrCode?.tamanho.name,
        'inicio_exibicao': _data(i.inicioExibicao),
        'fim_exibicao': _data(i.fimExibicao),
      };

  static ItemGrade _paraItem(Map<String, Object?> l) => ItemGrade(
        ordem: l['ordem'] as int,
        midiaId: l['midia_id'] as int,
        tipo: TipoMidia.values.byName(l['tipo'] as String),
        tipoConteudo: l['tipo_conteudo'] as String,
        hash: l['hash'] as String,
        tamanhoBytes: l['tamanho_bytes'] as int,
        duracaoSegundos: l['duracao_segundos'] as int,
        diasSemana: l['dias_semana'] as int,
        posicaoFixa: (l['posicao_fixa'] as int? ?? 0) == 1,
        qrCode: l['qr_conteudo'] is String
            ? QrCodeAnuncio(
                conteudo: l['qr_conteudo'] as String,
                legenda: l['qr_legenda'] as String? ?? '',
                posicao: PosicaoQrCode.values.asNameMap()[l['qr_posicao']] ?? PosicaoQrCode.inferiorDireito,
                tamanho: TamanhoQrCode.values.asNameMap()[l['qr_tamanho']] ?? TamanhoQrCode.medio,
              )
            : null,
        inicioExibicao: _lerData(l['inicio_exibicao']),
        fimExibicao: _lerData(l['fim_exibicao']),
      );

  static String? _data(DateTime? d) => d?.toIso8601String().substring(0, 10);

  static DateTime? _lerData(Object? v) {
    if (v is! String) return null;
    final p = v.split('-').map(int.parse).toList();
    return DateTime.utc(p[0], p[1], p[2]);
  }
}

class EstadoLocal {
  const EstadoLocal({
    required this.versaoGrade,
    required this.fusoHorario,
    required this.nomeTela,
    required this.intervaloSincronizacaoSegundos,
    this.versaoNoticias,
    this.composicao = const Composicao(),
    this.clima,
    this.cotacoes = const [],
    this.diretorio,
  });

  final String? versaoGrade;
  final String fusoHorario;
  final String? nomeTela;
  final int intervaloSincronizacaoSegundos;
  final String? versaoNoticias;
  final Composicao composicao;
  final Clima? clima;
  final List<Cotacao> cotacoes;
  final DiretorioTv? diretorio;
}
