import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';

/// API das TVs e storage falsos num HttpServer local, para testar o app
/// pelo protocolo de verdade (dio, JSON, cabeçalhos, códigos de status).
class ServidorFalso {
  ServidorFalso._(this._http);

  final HttpServer _http;

  /// Arquivos do "storage": nome → conteúdo.
  final arquivos = <String, List<int>>{};

  /// Nomes cujo download responde este status (ex.: 403 de URL vencida).
  final statusDownload = <String, int>{};

  /// Nomes que chegam corrompidos (hash não bate).
  final corrompidos = <String>{};

  /// Contagem de downloads por nome.
  final downloads = <String, int>{};

  /// Resposta da sincronização: status e corpo (função da query recebida).
  int statusSincronizacao = 200;
  Map<String, dynamic> Function(Map<String, String> query)? sincronizacao;

  /// Última query e cabeçalho Authorization recebidos.
  Map<String, String> ultimaQuery = {};
  String? ultimoToken;

  /// Pareamento: códigos aceitos → token.
  final codigos = <String, String>{};
  Map<String, dynamic>? ultimoPareamento;

  String get url => 'http://127.0.0.1:${_http.port}';
  String urlArquivo(String nome) => '$url/storage/$nome?token=abc';

  static Future<ServidorFalso> iniciar() async {
    final servidor = ServidorFalso._(await HttpServer.bind(InternetAddress.loopbackIPv4, 0));
    servidor._http.listen(servidor._atender);
    return servidor;
  }

  Future<void> parar() => _http.close(force: true);

  static String sha(List<int> bytes) => sha256.convert(bytes).toString();

  Map<String, dynamic> itemJson(int ordem, String nome, {String tipo = 'Imagem', int dias = 127}) => {
        'ordem': ordem,
        'midiaId': ordem + 100,
        'tipo': tipo,
        'tipoConteudo': tipo == 'Video' ? 'video/mp4' : 'image/png',
        'url': urlArquivo(nome),
        'hash': sha(arquivos[nome]!),
        'tamanhoBytes': arquivos[nome]!.length,
        'duracaoSegundos': 10,
        'diasSemana': dias,
        'inicioExibicao': null,
        'fimExibicao': null,
        'exibirHoje': true,
      };

  Future<void> _atender(HttpRequest req) async {
    final r = req.response;
    final caminho = req.uri.path;

    if (caminho.startsWith('/storage/')) {
      final nome = caminho.substring('/storage/'.length);
      downloads[nome] = (downloads[nome] ?? 0) + 1;
      final status = statusDownload[nome];
      if (status != null || !arquivos.containsKey(nome)) {
        r.statusCode = status ?? 404;
      } else {
        final bytes = corrompidos.contains(nome) ? [...arquivos[nome]!, 0] : arquivos[nome]!;
        r.headers.contentType = ContentType.binary;
        r.add(bytes);
      }
    } else if (caminho == '/api/v1/dispositivo/autenticar' && req.method == 'POST') {
      final corpo = jsonDecode(await utf8.decodeStream(req)) as Map<String, dynamic>;
      ultimoPareamento = corpo;
      final token = codigos[corpo['codigoPareamento']];
      if (token == null) {
        r.statusCode = 400;
      } else {
        _json(r, {
          'token': token,
          'expiraEm': '2026-11-12T18:21:47-03:00',
          'telaId': 12,
          'nomeTela': 'Recepção',
          'nomeParceiro': 'Academia Centro',
          'intervaloSincronizacaoSegundos': 300,
        });
      }
    } else if (caminho == '/api/v1/dispositivo/sincronizacao') {
      ultimaQuery = req.uri.queryParameters;
      ultimoToken = req.headers.value('authorization');
      r.statusCode = statusSincronizacao;
      if (statusSincronizacao == 200) _json(r, sincronizacao!(ultimaQuery));
    } else {
      r.statusCode = 404;
    }
    await r.close();
  }

  static void _json(HttpResponse r, Object corpo) {
    r.headers.contentType = ContentType.json;
    r.write(jsonEncode(corpo));
  }
}
