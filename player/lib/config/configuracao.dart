/// Valores de build. Troque com `--dart-define`:
///
///   flutter run --dart-define=API_URL=https://10.0.2.2:7216
///   flutter build apk --dart-define=API_URL=https://api.suaempresa.com.br
class Configuracao {
  /// Endereço da API. Pode ser trocado na tela de pareamento (fica gravado).
  /// `10.0.2.2` é o `localhost` do PC visto do emulador.
  static const urlApiPadrao = String.fromEnvironment('API_URL', defaultValue: 'https://10.0.2.2:7216');

  /// Versão mostrada no painel. Acompanhe o `version` do pubspec.yaml.
  static const versaoApp = String.fromEnvironment('VERSAO_APP', defaultValue: '1.0.0');
}
