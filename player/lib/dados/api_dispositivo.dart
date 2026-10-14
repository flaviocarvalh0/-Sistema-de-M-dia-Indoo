import 'dart:io';

import 'package:dio/dio.dart';
import 'package:dio/io.dart';
import 'package:flutter/foundation.dart';

import 'modelos.dart';

/// Falhas da API das TVs, já traduzidas para o que o app faz com cada uma
/// (tabela da seção 2 do contrato).
sealed class FalhaApi implements Exception {
  const FalhaApi(this.mensagem);
  final String mensagem;

  @override
  String toString() => mensagem;
}

/// 400 no pareamento: código inválido, expirado, já usado, de tela inativa
/// ou de parceiro bloqueado (a API não diz qual).
class CodigoRecusado extends FalhaApi {
  const CodigoRecusado() : super('Código inválido ou expirado. Gere outro no painel.');
}

/// 401 na sincronização: a TV foi desvinculada ou outra foi pareada no lugar.
class CredencialRevogada extends FalhaApi {
  const CredencialRevogada() : super('Esta TV foi desvinculada no painel.');
}

/// 403: tela inativa no painel. Continua tocando o que já tem.
class AcessoSuspenso extends FalhaApi {
  const AcessoSuspenso() : super('Tela inativa no painel.');
}

/// 429: muitas tentativas.
class MuitasTentativas extends FalhaApi {
  const MuitasTentativas() : super('Muitas tentativas. Aguarde alguns minutos.');
}

/// Sem rede, tempo esgotado ou erro do servidor: tentar no próximo ciclo.
class Indisponivel extends FalhaApi {
  const Indisponivel([super.mensagem = 'Sem conexão com o servidor.']);
}

/// Cliente de `{api}/api/v1/dispositivo`.
class ApiDispositivo {
  ApiDispositivo(String urlBase, {Dio? dio})
      : _dio = dio ??
            Dio(BaseOptions(
              connectTimeout: const Duration(seconds: 15),
              receiveTimeout: const Duration(seconds: 30),
            )) {
    _dio.options.baseUrl = '${urlBase.replaceAll(RegExp(r'/+$'), '')}/api/v1/dispositivo';
    if (aceitarCertificadoDeDesenvolvimento) {
      _dio.httpClientAdapter = IOHttpClientAdapter(
        createHttpClient: () => HttpClient()..badCertificateCallback = (_, __, ___) => true,
      );
    }
  }

  /// Em debug/profile a API local usa o certificado de desenvolvimento do
  /// .NET (emitido para `localhost`, sem validade na TV). O APK de release
  /// nunca aceita certificado inválido.
  static bool get aceitarCertificadoDeDesenvolvimento => !kReleaseMode;

  final Dio _dio;

  /// Normaliza o que a pessoa digitou na TV: `ab12-cd34` = `AB12CD34`.
  static String normalizarCodigo(String digitado) =>
      digitado.toUpperCase().replaceAll(RegExp(r'[^A-Z0-9]'), '');

  Future<Pareamento> autenticar({
    required String codigo,
    required String dispositivoId,
    required String versaoApp,
  }) async {
    final resposta = await _enviar(() => _dio.post<Map<String, dynamic>>(
          '/autenticar',
          data: {
            'codigoPareamento': normalizarCodigo(codigo),
            'dispositivoId': dispositivoId,
            'versaoApp': versaoApp,
          },
        ), pareamento: true);
    return Pareamento.fromJson(resposta.data!);
  }

  Future<RespostaSincronizacao> sincronizar({
    required String token,
    String? versaoGrade,
    String? versaoNoticias,
    String? versaoApp,
    int? itensProntos,
  }) async {
    final resposta = await _enviar(() => _dio.get<Map<String, dynamic>>(
          '/sincronizacao',
          queryParameters: {
            if (versaoGrade != null) 'versaoGrade': versaoGrade,
            if (versaoNoticias != null) 'versaoNoticias': versaoNoticias,
            if (versaoApp != null) 'versaoApp': versaoApp,
            if (itensProntos != null) 'itensProntos': itensProntos,
          },
          options: Options(headers: {'Authorization': 'Bearer $token'}),
        ));
    return RespostaSincronizacao.fromJson(resposta.data!);
  }

  Future<Response<T>> _enviar<T>(Future<Response<T>> Function() chamada, {bool pareamento = false}) async {
    try {
      return await chamada();
    } on DioException catch (e) {
      final status = e.response?.statusCode;
      throw switch (status) {
        400 when pareamento => const CodigoRecusado(),
        401 => const CredencialRevogada(),
        403 => const AcessoSuspenso(),
        429 => const MuitasTentativas(),
        null => Indisponivel('Sem conexão com ${_servidor()} (${_motivo(e)}).'),
        _ => Indisponivel('O servidor respondeu $status.'),
      };
    }
  }

  /// `https://192.168.0.10:7216`, sem o caminho da API.
  String _servidor() => _dio.options.baseUrl.replaceFirst('/api/v1/dispositivo', '');

  /// O motivo em palavras, para quem está na frente da TV saber o que checar.
  static String _motivo(DioException e) {
    final erro = e.error;
    return switch (e.type) {
      DioExceptionType.connectionTimeout => 'tempo esgotado: confira o IP, o firewall e se a TV está na mesma rede',
      DioExceptionType.receiveTimeout || DioExceptionType.sendTimeout => 'o servidor demorou demais para responder',
      DioExceptionType.badCertificate => 'certificado recusado',
      _ when erro is HandshakeException => 'falha no https: ${erro.message}',
      _ when erro is SocketException =>
        'conexão recusada: confira se a API está rodando com --urls "https://*:7216" (${erro.osError?.message ?? erro.message})',
      _ => e.message ?? e.type.name,
    };
  }
}
