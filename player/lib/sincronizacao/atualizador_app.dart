import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

import '../dados/modelos.dart';
import 'baixador_midia.dart';

/// Como a TV instala um APK. No aparelho é o canal nativo ([InstaladorAndroid]).
abstract interface class InstaladorApk {
  /// Entrega o APK ao instalador do Android. Com o app como "dono do
  /// aparelho" (device owner) instala sem perguntar; senão o Android mostra
  /// a confirmação na TV. Lança se não conseguiu nem começar.
  Future<void> instalar(File apk);

  /// O app é o dono do aparelho (instala sem confirmação).
  Future<bool> instalaSemConfirmacao();
}

/// O que a última verificação fez (aparece no painel de informações da TV).
enum EstadoAtualizacao { atualizado, baixando, instalando, falhou, aguardando }

/// Atualização automática do app (ADR-021): quando a sincronização traz uma
/// versão mais nova, baixa o APK (SHA-256 conferido, como as mídias) e
/// entrega ao instalador. Depois de instalar, o Android reinicia o app
/// (MY_PACKAGE_REPLACED). Falhou (download, confirmação recusada)? Tenta de
/// novo só depois de [esperaEntreTentativas], para não insistir a cada minuto.
class AtualizadorApp {
  AtualizadorApp({
    required this.baixador,
    required this.instalador,
    required this.codigoAtual,
    DateTime Function()? agora,
  }) : _agora = agora ?? DateTime.now;

  /// Pasta própria (`<documentos>/app`), fora das mídias.
  final BaixadorMidia baixador;
  final InstaladorApk instalador;
  final int codigoAtual;
  final DateTime Function() _agora;

  static const esperaEntreTentativas = Duration(hours: 1);

  final estado = ValueNotifier<EstadoAtualizacao>(EstadoAtualizacao.atualizado);
  AtualizacaoApp? versaoAlvo;

  /// O app é device owner (conferido uma vez em [conferirModo]).
  bool semConfirmacao = false;
  int? _tentadoCodigo;
  DateTime? _tentadoEm;

  Future<void> conferirModo() async {
    try {
      semConfirmacao = await instalador.instalaSemConfirmacao();
    } catch (_) {
      semConfirmacao = false;
    }
  }

  /// Linha do painel de informações da TV.
  String get descricao {
    final modo = semConfirmacao ? 'automática' : 'pede confirmação na TV';
    final alvo = versaoAlvo?.nome;
    return switch (estado.value) {
      EstadoAtualizacao.atualizado => 'em dia · instalação $modo',
      EstadoAtualizacao.baixando => 'baixando $alvo',
      EstadoAtualizacao.instalando => semConfirmacao ? 'instalando $alvo' : 'instalando $alvo · confirme na TV',
      EstadoAtualizacao.falhou => 'falhou ao instalar $alvo · tenta de novo em 1 h',
      EstadoAtualizacao.aguardando => '$alvo disponível · nova tentativa em breve',
    };
  }

  Future<void> verificar(AtualizacaoApp? atualizacao) async {
    if (atualizacao == null || codigoAtual <= 0 || atualizacao.codigo <= codigoAtual) {
      versaoAlvo = null;
      estado.value = EstadoAtualizacao.atualizado;
      await baixador.limpar(const []);
      return;
    }

    versaoAlvo = atualizacao;
    final agora = _agora();
    if (_tentadoCodigo == atualizacao.codigo && _tentadoEm != null && agora.difference(_tentadoEm!) < esperaEntreTentativas) {
      if (estado.value != EstadoAtualizacao.instalando) estado.value = EstadoAtualizacao.aguardando;
      return;
    }
    _tentadoCodigo = atualizacao.codigo;
    _tentadoEm = agora;

    try {
      estado.value = EstadoAtualizacao.baixando;
      await baixador.limpar([atualizacao]);
      final apk = await baixador.garantir(atualizacao);
      estado.value = EstadoAtualizacao.instalando;
      await instalador.instalar(apk);
    } catch (_) {
      // URL vencida, hash errado, confirmação recusada: fica para a próxima janela.
      estado.value = EstadoAtualizacao.falhou;
    }
  }
}

/// O canal com o código nativo (MainActivity.kt).
class InstaladorAndroid implements InstaladorApk {
  static const _canal = MethodChannel('br.com.midiaindoor/atualizador');

  @override
  Future<void> instalar(File apk) => _canal.invokeMethod<void>('instalar', {'caminho': apk.path});

  @override
  Future<bool> instalaSemConfirmacao() async {
    try {
      return await _canal.invokeMethod<bool>('donoDoAparelho') ?? false;
    } on MissingPluginException {
      return false;
    }
  }
}
