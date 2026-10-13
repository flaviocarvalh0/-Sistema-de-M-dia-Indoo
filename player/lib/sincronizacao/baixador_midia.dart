import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:dio/dio.dart';
import 'package:path/path.dart' as p;

import '../dados/modelos.dart';

/// A URL assinada venceu (o storage respondeu 400/403): é preciso
/// sincronizar de novo para receber URLs novas (regra 4).
class UrlVencida implements Exception {
  const UrlVencida();
}

/// O arquivo não chegou íntegro depois de todas as tentativas.
class DownloadFalhou implements Exception {
  const DownloadFalhou(this.motivo);
  final String motivo;

  @override
  String toString() => 'DownloadFalhou: $motivo';
}

/// Regra 2 do contrato: baixa para `<arquivo>.part`, confere o SHA-256 e só
/// então renomeia para o nome final. Hash errado apaga e tenta de novo.
class BaixadorMidia {
  BaixadorMidia(
    this.pasta, {
    Dio? dio,
    this.tentativas = 3,
    Future<void> Function(Duration)? esperar,
  })  : _dio = dio ??
            Dio(BaseOptions(
              connectTimeout: const Duration(seconds: 20),
              receiveTimeout: const Duration(minutes: 2),
            )),
        _esperar = esperar ?? Future.delayed;

  /// Pasta das mídias (`<documentos>/midias`).
  final Directory pasta;
  final int tentativas;
  final Dio _dio;
  final Future<void> Function(Duration) _esperar;

  File arquivoDe(ItemGrade item) => File(p.join(pasta.path, item.nomeArquivo));

  /// O arquivo final só existe depois de conferido, então existir basta.
  bool pronto(ItemGrade item) => arquivoDe(item).existsSync();

  /// Garante o arquivo de [item] na pasta. Já existindo, não baixa de novo.
  Future<File> garantir(ItemGrade item) async {
    final destino = arquivoDe(item);
    if (destino.existsSync()) return destino;
    final url = item.url;
    if (url == null) throw const DownloadFalhou('item sem url');

    await pasta.create(recursive: true);
    final parcial = File('${destino.path}.part');

    for (var tentativa = 1;; tentativa++) {
      try {
        await _dio.download(url, parcial.path, deleteOnError: true);
        final hash = await _sha256(parcial);
        if (hash == item.hash) {
          return await parcial.rename(destino.path);
        }
        await _apagar(parcial);
        if (tentativa >= tentativas) throw DownloadFalhou('hash diferente em ${item.nomeArquivo}');
      } on DioException catch (e) {
        await _apagar(parcial);
        final status = e.response?.statusCode;
        if (status == 400 || status == 403) throw const UrlVencida();
        if (tentativa >= tentativas) throw DownloadFalhou('${e.type.name} ${status ?? ''}'.trim());
      }
      await _esperar(Duration(seconds: 5 * tentativa));
    }
  }

  /// Apaga os arquivos que não estão em [manter] e as sobras `.part`.
  Future<void> limpar(Iterable<ItemGrade> manter) async {
    if (!pasta.existsSync()) return;
    final nomes = manter.map((i) => i.nomeArquivo).toSet();
    await for (final entidade in pasta.list()) {
      if (entidade is File && !nomes.contains(p.basename(entidade.path))) {
        await _apagar(entidade);
      }
    }
  }

  /// SHA-256 lendo o arquivo em blocos (vídeo de 50 MB não vai para a memória).
  static Future<String> _sha256(File arquivo) async =>
      (await sha256.bind(arquivo.openRead()).first).toString();

  static Future<void> _apagar(File arquivo) async {
    try {
      if (arquivo.existsSync()) await arquivo.delete();
    } on FileSystemException {
      // Arquivo em uso (ex.: o vídeo tocando): sai na próxima limpeza.
    }
  }
}
