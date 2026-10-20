import 'dart:math';

import '../dados/api_dispositivo.dart';
import '../dados/banco_local.dart';
import '../dados/modelos.dart';

/// Prova de veiculação (ADR-023): grava no banco local cada anúncio que saiu
/// da tela e envia em lotes quando a TV está falando com a API.
///
/// Sem internet nada se perde: as exibições ficam guardadas (até
/// [BancoLocal.registrarExibicao] descartar as mais antigas) e sobem no
/// primeiro envio depois que a conexão volta. Reenviar não duplica: cada
/// exibição tem um UUID próprio.
class RegistradorExibicoes {
  RegistradorExibicoes({
    required this.banco,
    required this.semConexao,
    DateTime Function()? agora,
    Random? aleatorio,
    this.intervaloEnvio = const Duration(minutes: 15),
    this.envioImediatoCom = 300,
  })  : _agora = agora ?? DateTime.now,
        _aleatorio = aleatorio ?? Random.secure();

  final BancoLocal banco;

  /// A TV está sem falar com a API agora (a última sincronização falhou).
  final bool Function() semConexao;

  /// Envia no máximo a cada [intervaloEnvio]...
  final Duration intervaloEnvio;

  /// ...a menos que já haja [envioImediatoCom] pendentes.
  final int envioImediatoCom;

  final DateTime Function() _agora;
  final Random _aleatorio;
  DateTime? _ultimoEnvio;

  static const tamanhoLote = 500;

  /// Chamado pelo loop quando um anúncio sai da tela. Nunca lança: falhar em
  /// gravar a prova não pode parar a TV.
  Future<void> registrar({
    required int midiaId,
    required DateTime inicio,
    required Duration duracao,
    required bool concluida,
  }) async {
    try {
      await banco.registrarExibicao(Exibicao(
        id: _uuid(),
        midiaId: midiaId,
        inicio: inicio.toUtc(),
        duracao: duracao,
        concluida: concluida,
        semConexao: semConexao(),
      ));
    } catch (_) {
      // Banco indisponível: perde esta exibição, a TV segue tocando.
    }
  }

  /// Depois de uma sincronização que respondeu: envia o que estiver pendente,
  /// em lotes, se já passou o intervalo (ou se acumulou muito). Para no
  /// primeiro erro; o que não foi tem nova chance no próximo ciclo.
  Future<int> enviarSePreciso(ApiDispositivo api, String token) async {
    final agora = _agora();
    final ultimo = _ultimoEnvio;
    if (ultimo != null && agora.difference(ultimo) < intervaloEnvio
        && await banco.contarExibicoesPendentes() < envioImediatoCom) {
      return 0;
    }

    var enviadas = 0;
    try {
      while (true) {
        final lote = await banco.exibicoesPendentes(limite: tamanhoLote);
        if (lote.isEmpty) break;
        await api.enviarExibicoes(token: token, exibicoes: lote);
        await banco.apagarExibicoes(lote.map((e) => e.id));
        enviadas += lote.length;
        if (lote.length < tamanhoLote) break;
      }
      _ultimoEnvio = agora;
    } on FalhaApi {
      // Fica para o próximo ciclo; o que já foi enviado já saiu do banco.
    }
    return enviadas;
  }

  /// UUID v4.
  String _uuid() {
    final b = List<int>.generate(16, (_) => _aleatorio.nextInt(256));
    b[6] = (b[6] & 0x0f) | 0x40;
    b[8] = (b[8] & 0x3f) | 0x80;
    final h = b.map((x) => x.toRadixString(16).padLeft(2, '0')).join();
    return '${h.substring(0, 8)}-${h.substring(8, 12)}-${h.substring(12, 16)}-${h.substring(16, 20)}-${h.substring(20)}';
  }
}
