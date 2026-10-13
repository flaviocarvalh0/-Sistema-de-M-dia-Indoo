import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:uuid/uuid.dart';

/// Onde o app guarda o que identifica a TV. Interface para os testes.
abstract class Cofre {
  Future<String?> ler(String chave);
  Future<void> gravar(String chave, String valor);
  Future<void> apagar(String chave);
}

class CofreSeguro implements Cofre {
  static const _armazenamento = FlutterSecureStorage();

  @override
  Future<String?> ler(String chave) => _armazenamento.read(key: chave);

  @override
  Future<void> gravar(String chave, String valor) => _armazenamento.write(key: chave, value: valor);

  @override
  Future<void> apagar(String chave) => _armazenamento.delete(key: chave);
}

/// Token, id do aparelho e endereço do servidor.
class Credenciais {
  Credenciais(this._cofre);

  final Cofre _cofre;

  static const _token = 'token';
  static const _dispositivoId = 'dispositivoId';
  static const _urlServidor = 'urlServidor';

  Future<String?> token() => _cofre.ler(_token);
  Future<void> gravarToken(String token) => _cofre.gravar(_token, token);
  Future<void> apagarToken() => _cofre.apagar(_token);

  /// Gerado uma vez na instalação e nunca muda.
  Future<String> dispositivoId() async {
    final atual = await _cofre.ler(_dispositivoId);
    if (atual != null) return atual;
    final novo = const Uuid().v4();
    await _cofre.gravar(_dispositivoId, novo);
    return novo;
  }

  Future<String?> urlServidor() => _cofre.ler(_urlServidor);
  Future<void> gravarUrlServidor(String url) => _cofre.gravar(_urlServidor, url);
}
