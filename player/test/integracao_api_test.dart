// Ponta a ponta contra a API de verdade (pareia, sincroniza, baixa e confere
// o hash). Só roda com as variáveis abaixo; sem elas, é pulado.
//
//   INDOOR_API_URL=http://localhost:5366 INDOOR_CODIGO=AB12CD34 flutter test test/integracao_api_test.dart
//
// O código sai do painel (Telas → Gerar código) de uma tela com playlist.
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:midia_indoor_player/dados/api_dispositivo.dart';
import 'package:midia_indoor_player/dados/banco_local.dart';
import 'package:midia_indoor_player/dados/credenciais.dart';
import 'package:midia_indoor_player/reproducao/filtro_do_dia.dart';
import 'package:midia_indoor_player/sincronizacao/baixador_midia.dart';
import 'package:midia_indoor_player/sincronizacao/sincronizador.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

import 'sincronizador_test.dart' show CofreMemoria;

void main() {
  final url = Platform.environment['INDOOR_API_URL'];
  final codigo = Platform.environment['INDOOR_CODIGO'];
  final pular = url == null || codigo == null ? 'defina INDOOR_API_URL e INDOOR_CODIGO' : null;

  test('pareia, sincroniza e baixa a grade da API', () async {
    sqfliteFfiInit();
    final pasta = await Directory.systemTemp.createTemp('integracao');
    final banco = await BancoLocal.abrir(inMemoryDatabasePath, fabrica: databaseFactoryFfiNoIsolate);
    final credenciais = Credenciais(CofreMemoria());
    final api = ApiDispositivo(url!);

    final pareamento = await api.autenticar(
      codigo: codigo!,
      dispositivoId: await credenciais.dispositivoId(),
      versaoApp: '1.0.0-teste',
    );
    await credenciais.gravarToken(pareamento.token);
    // ignore: avoid_print
    print('Pareada: ${pareamento.nomeTela} (${pareamento.nomeParceiro})');

    final baixador = BaixadorMidia(Directory('${pasta.path}/midias'));
    final sync = Sincronizador(
      api: api,
      banco: banco,
      credenciais: credenciais,
      baixador: baixador,
      versaoApp: '1.0.0-teste',
    );

    expect(await sync.sincronizar(), ResultadoCiclo.gradeTrocada);
    final s = sync.situacao.value!;
    expect(s.grade, isNotEmpty);
    expect(s.grade.every(baixador.pronto), isTrue);
    final hoje = FiltroDoDia.hoje(s.fusoHorario, DateTime.now());
    // ignore: avoid_print
    print('Grade: ${s.grade.length} itens, ${FiltroDoDia.itensDoDia(s.grade, hoje).length} para hoje '
        '(${s.fusoHorario}); arquivos: ${Directory('${pasta.path}/midias').listSync().length}');

    // Segunda volta: nada muda e o painel passa a ver os itens prontos.
    expect(await sync.sincronizar(), ResultadoCiclo.semMudanca);

    // Código usado não pareia de novo.
    await expectLater(
      api.autenticar(codigo: codigo, dispositivoId: 'outro', versaoApp: '1.0.0-teste'),
      throwsA(isA<CodigoRecusado>()),
    );

    await banco.fechar();
    await pasta.delete(recursive: true);
  }, skip: pular);
}
