import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:midia_indoor_player/dados/modelos.dart';
import 'package:midia_indoor_player/sincronizacao/atualizador_app.dart';
import 'package:midia_indoor_player/sincronizacao/baixador_midia.dart';

import 'servidor_falso.dart';

class InstaladorFalso implements InstaladorApk {
  final instalados = <String>[];
  Object? erro;

  @override
  Future<void> instalar(File apk) async {
    if (erro != null) throw erro!;
    instalados.add(await apk.exists() ? apk.path : 'inexistente');
  }

  @override
  Future<bool> instalaSemConfirmacao() async => true;
}

void main() {
  late ServidorFalso servidor;
  late Directory pasta;
  late InstaladorFalso instalador;
  var agora = DateTime(2026, 10, 16, 10);

  setUp(() async {
    servidor = await ServidorFalso.iniciar();
    pasta = await Directory.systemTemp.createTemp('app');
    instalador = InstaladorFalso();
    agora = DateTime(2026, 10, 16, 10);
    servidor.arquivos['app.apk'] = List.generate(4000, (i) => i % 200);
  });

  tearDown(() async {
    await servidor.parar();
    await pasta.delete(recursive: true);
  });

  AtualizadorApp atualizador({int codigoAtual = 5}) => AtualizadorApp(
        baixador: BaixadorMidia(pasta, esperar: (_) async {}),
        instalador: instalador,
        codigoAtual: codigoAtual,
        agora: () => agora,
      );

  AtualizacaoApp versao(int codigo) => AtualizacaoApp.fromJson({
        'codigo': codigo,
        'nome': '1.$codigo.0',
        'url': servidor.urlArquivo('app.apk'),
        'hash': ServidorFalso.sha(servidor.arquivos['app.apk']!).toUpperCase(),
        'tamanhoBytes': servidor.arquivos['app.apk']!.length,
      });

  test('versão mais nova: baixa, confere o hash e entrega ao instalador', () async {
    final a = atualizador();
    await a.verificar(versao(6));
    expect(instalador.instalados, hasLength(1));
    expect(instalador.instalados.single, endsWith('.apk'));
    expect(a.estado.value, EstadoAtualizacao.instalando);
    expect(a.versaoAlvo?.codigo, 6);
  });

  test('versão igual, menor ou ausente: não baixa e limpa a pasta', () async {
    File('${pasta.path}/velho.apk').writeAsStringSync('x');
    final a = atualizador();
    await a.verificar(versao(5));
    await a.verificar(versao(4));
    await a.verificar(null);
    expect(instalador.instalados, isEmpty);
    expect(servidor.downloads['app.apk'], isNull);
    expect(a.estado.value, EstadoAtualizacao.atualizado);
    expect(pasta.listSync(), isEmpty);
  });

  test('app sem código de versão (0) nunca se atualiza', () async {
    await atualizador(codigoAtual: 0).verificar(versao(9));
    expect(instalador.instalados, isEmpty);
  });

  test('falhou: espera 1 hora antes de tentar a mesma versão de novo', () async {
    instalador.erro = Exception('recusado');
    final a = atualizador();
    await a.verificar(versao(6));
    expect(a.estado.value, EstadoAtualizacao.falhou);

    instalador.erro = null;
    agora = agora.add(const Duration(minutes: 30));
    await a.verificar(versao(6));
    expect(instalador.instalados, isEmpty);
    expect(a.estado.value, EstadoAtualizacao.aguardando);

    agora = agora.add(const Duration(minutes: 31));
    await a.verificar(versao(6));
    expect(instalador.instalados, hasLength(1));
    // O APK ficou guardado: não baixou de novo.
    expect(servidor.downloads['app.apk'], 1);
  });

  test('versão ainda mais nova não espera a hora da anterior', () async {
    instalador.erro = Exception('recusado');
    final a = atualizador();
    await a.verificar(versao(6));
    instalador.erro = null;
    await a.verificar(versao(7));
    expect(instalador.instalados, hasLength(1));
  });

  test('hash errado: não instala', () async {
    servidor.corrompidos.add('app.apk');
    final a = atualizador();
    await a.verificar(versao(6));
    expect(instalador.instalados, isEmpty);
    expect(a.estado.value, EstadoAtualizacao.falhou);
  });

  test('descrição para o painel da TV', () async {
    final a = atualizador();
    await a.conferirModo();
    expect(a.descricao, 'em dia · instalação automática');
    instalador.erro = Exception('x');
    await a.verificar(versao(6));
    expect(a.descricao, contains('falhou ao instalar 1.6.0'));
  });
}
