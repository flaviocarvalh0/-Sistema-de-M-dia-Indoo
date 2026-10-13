import 'package:sqflite/sqflite.dart';

import 'modelos.dart';

/// Grade em uso e estado da última sincronização, no SQLite do aparelho.
///
/// Guarda só a grade que está TOCANDO: a nova fica em memória enquanto os
/// arquivos baixam e entra aqui de uma vez, numa transação ([trocarGrade]).
class BancoLocal {
  BancoLocal._(this._db);

  final Database _db;

  static Future<BancoLocal> abrir(String caminho, {DatabaseFactory? fabrica}) async {
    final db = await (fabrica ?? databaseFactory).openDatabase(
      caminho,
      options: OpenDatabaseOptions(
        version: 1,
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
              fim_exibicao TEXT
            )''');
        },
      ),
    );
    return BancoLocal._(db);
  }

  Future<void> fechar() => _db.close();

  Future<EstadoLocal> lerEstado() async {
    final linhas = await _db.query('estado');
    final mapa = {for (final l in linhas) l['chave'] as String: l['valor'] as String?};
    return EstadoLocal(
      versaoGrade: mapa['versaoGrade'],
      fusoHorario: mapa['fusoHorario'] ?? 'America/Sao_Paulo',
      nomeTela: mapa['nomeTela'],
      intervaloSincronizacaoSegundos: int.tryParse(mapa['intervalo'] ?? '') ?? 300,
    );
  }

  Future<void> gravarEstado({String? fusoHorario, String? nomeTela, int? intervaloSincronizacaoSegundos}) =>
      _db.transaction((txn) async {
        if (fusoHorario != null) await _gravar(txn, 'fusoHorario', fusoHorario);
        if (nomeTela != null) await _gravar(txn, 'nomeTela', nomeTela);
        if (intervaloSincronizacaoSegundos != null) {
          await _gravar(txn, 'intervalo', '$intervaloSincronizacaoSegundos');
        }
      });

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
        await txn.delete('estado');
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
  });

  final String? versaoGrade;
  final String fusoHorario;
  final String? nomeTela;
  final int intervaloSincronizacaoSegundos;
}
