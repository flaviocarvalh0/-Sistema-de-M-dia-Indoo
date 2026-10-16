import 'dart:io';

import 'package:flutter/material.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

import 'config/configuracao.dart';
import 'config/versao_app.dart';
import 'dados/api_dispositivo.dart';
import 'dados/banco_local.dart';
import 'dados/credenciais.dart';
import 'sincronizacao/atualizador_app.dart';
import 'sincronizacao/baixador_midia.dart';
import 'sincronizacao/sincronizador.dart';
import 'telas/pareamento_page.dart';
import 'telas/reproducao_page.dart';

/// Monta as dependências uma vez, na abertura do app.
class Servicos {
  Servicos._(this.credenciais, this.banco, this.baixador, this.urlServidor, this.atualizador);

  final Credenciais credenciais;
  final BancoLocal banco;
  final BaixadorMidia baixador;

  /// Atualização automática do app (ADR-021).
  final AtualizadorApp atualizador;
  String urlServidor;
  late Sincronizador sincronizador = _novoSincronizador();

  static Future<Servicos> iniciar() async {
    final documentos = await getApplicationDocumentsDirectory();
    final credenciais = Credenciais(CofreSeguro());
    final banco = await BancoLocal.abrir(p.join(documentos.path, 'midia_indoor.db'));
    final baixador = BaixadorMidia(Directory(p.join(documentos.path, 'midias')));
    final url = await credenciais.urlServidor() ?? Configuracao.urlApiPadrao;
    await VersaoApp.carregar();
    final atualizador = AtualizadorApp(
      baixador: BaixadorMidia(Directory(p.join(documentos.path, 'app'))),
      instalador: InstaladorAndroid(),
      codigoAtual: VersaoApp.codigo,
    );
    await atualizador.conferirModo();
    return Servicos._(credenciais, banco, baixador, url, atualizador);
  }

  ApiDispositivo get api => ApiDispositivo(urlServidor);

  /// Troca o endereço do servidor (tela de pareamento).
  Future<void> definirServidor(String url) async {
    urlServidor = url.trim();
    await credenciais.gravarUrlServidor(urlServidor);
    sincronizador = _novoSincronizador();
  }

  Sincronizador _novoSincronizador() => Sincronizador(
        api: api,
        banco: banco,
        credenciais: credenciais,
        baixador: baixador,
        versaoApp: VersaoApp.nome,
        versaoCodigoApp: VersaoApp.codigo,
        atualizador: atualizador,
      );
}

class MidiaIndoorApp extends StatelessWidget {
  const MidiaIndoorApp({super.key, required this.servicos});

  final Servicos servicos;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Mídia Indoor',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF0F766E), brightness: Brightness.dark),
        scaffoldBackgroundColor: Colors.black,
        useMaterial3: true,
      ),
      home: _Inicio(servicos: servicos),
    );
  }
}

/// Decide a primeira tela: com token ou com grade baixada, toca; senão, pareia.
class _Inicio extends StatefulWidget {
  const _Inicio({required this.servicos});

  final Servicos servicos;

  @override
  State<_Inicio> createState() => _InicioState();
}

class _InicioState extends State<_Inicio> {
  late final Future<bool> _temAlgoParaTocar = _verificar();

  Future<bool> _verificar() async {
    final token = await widget.servicos.credenciais.token();
    final grade = await widget.servicos.banco.lerGrade();
    return token != null || grade.isNotEmpty;
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<bool>(
      future: _temAlgoParaTocar,
      builder: (context, snap) {
        if (!snap.hasData) return const ColoredBox(color: Colors.black);
        return snap.data! ? ReproducaoPage(servicos: widget.servicos) : PareamentoPage(servicos: widget.servicos);
      },
    );
  }
}
