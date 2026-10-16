import 'package:package_info_plus/package_info_plus.dart';

import 'configuracao.dart';

/// A versão do app instalado, lida do próprio APK (o `version` do
/// pubspec.yaml): nome (`1.3.0`) e código (o número depois do `+`). O código
/// vai na sincronização para o servidor decidir se há atualização (ADR-021).
class VersaoApp {
  VersaoApp._();

  static String nome = Configuracao.versaoApp;

  /// 0 = desconhecido (testes): a TV não pede atualização.
  static int codigo = 0;

  static Future<void> carregar() async {
    try {
      final info = await PackageInfo.fromPlatform();
      nome = info.version.isEmpty ? nome : info.version;
      codigo = int.tryParse(info.buildNumber) ?? 0;
    } catch (_) {
      // Sem plataforma (testes): fica com os padrões.
    }
  }
}
