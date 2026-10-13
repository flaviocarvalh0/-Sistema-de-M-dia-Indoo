import 'package:flutter/foundation.dart';

import '../dados/api_dispositivo.dart';
import '../dados/banco_local.dart';
import '../dados/credenciais.dart';
import '../dados/modelos.dart';
import 'baixador_midia.dart';

/// Como terminou um ciclo de sincronização.
enum ResultadoCiclo {
  /// Grade nova baixada e em uso.
  gradeTrocada,

  /// Nada mudou no servidor.
  semMudanca,

  /// Sem token: a TV ainda não foi pareada.
  semCredencial,

  /// 401: o token foi apagado; continua tocando até parear de novo.
  desvinculada,

  /// 403: tela inativa ou parceiro bloqueado. Continua tocando.
  suspensa,

  /// Sem rede, 429 ou 5xx. Continua tocando.
  semConexao,

  /// Algum arquivo não baixou (ou a URL venceu): a grade antiga continua e o
  /// próximo ciclo deve vir logo.
  downloadIncompleto,
}

/// O que a tela de reprodução precisa saber, sempre lido do banco.
@immutable
class SituacaoPlayer {
  const SituacaoPlayer({
    required this.grade,
    required this.fusoHorario,
    required this.nomeTela,
    required this.ultimoResultado,
    required this.ultimaSincronizacao,
  });

  final List<ItemGrade> grade;
  final String fusoHorario;
  final String? nomeTela;
  final ResultadoCiclo? ultimoResultado;

  /// Último contato bem-sucedido com o servidor.
  final DateTime? ultimaSincronizacao;

  bool get desvinculada =>
      ultimoResultado == ResultadoCiclo.desvinculada || ultimoResultado == ResultadoCiclo.semCredencial;
}

/// Um ciclo: pergunta ao servidor, baixa o que falta, troca a grade de uma
/// vez e apaga o que saiu (regras 2 e 3 do contrato).
class Sincronizador {
  Sincronizador({
    required this.api,
    required this.banco,
    required this.credenciais,
    required this.baixador,
    required this.versaoApp,
  });

  final ApiDispositivo api;
  final BancoLocal banco;
  final Credenciais credenciais;
  final BaixadorMidia baixador;
  final String versaoApp;

  final situacao = ValueNotifier<SituacaoPlayer?>(null);

  bool _rodando = false;
  ResultadoCiclo? _ultimo;
  DateTime? _ultimaSincronizacao;

  /// Intervalo pedido pelo servidor (padrão 5 min).
  Duration intervalo = const Duration(minutes: 5);

  /// Lê o banco e publica a situação (ao abrir o app, antes de ir à rede).
  Future<void> carregar() async {
    final estado = await banco.lerEstado();
    intervalo = Duration(seconds: estado.intervaloSincronizacaoSegundos);
    situacao.value = SituacaoPlayer(
      grade: await banco.lerGrade(),
      fusoHorario: estado.fusoHorario,
      nomeTela: estado.nomeTela,
      ultimoResultado: _ultimo,
      ultimaSincronizacao: _ultimaSincronizacao,
    );
  }

  Future<ResultadoCiclo> sincronizar() async {
    if (_rodando) return _ultimo ?? ResultadoCiclo.semMudanca;
    _rodando = true;
    try {
      _ultimo = await _ciclo();
      return _ultimo!;
    } finally {
      _rodando = false;
      await carregar();
    }
  }

  Future<ResultadoCiclo> _ciclo() async {
    final token = await credenciais.token();
    if (token == null) return ResultadoCiclo.semCredencial;

    final estado = await banco.lerEstado();
    final atual = await banco.lerGrade();

    final RespostaSincronizacao resposta;
    try {
      resposta = await api.sincronizar(
        token: token,
        versaoGrade: estado.versaoGrade,
        versaoApp: versaoApp,
        itensProntos: atual.where(baixador.pronto).length,
      );
    } on CredencialRevogada {
      // Mantém grade e arquivos: a TV segue tocando até parear de novo.
      await credenciais.apagarToken();
      return ResultadoCiclo.desvinculada;
    } on AcessoSuspenso {
      return ResultadoCiclo.suspensa;
    } on FalhaApi {
      return ResultadoCiclo.semConexao;
    }

    _ultimaSincronizacao = DateTime.now();
    if (resposta.novoToken != null) await credenciais.gravarToken(resposta.novoToken!);
    await banco.gravarEstado(
      fusoHorario: resposta.fusoHorario,
      nomeTela: resposta.nomeTela,
      intervaloSincronizacaoSegundos: resposta.intervaloSincronizacaoSegundos,
    );

    if (!resposta.alterada) return ResultadoCiclo.semMudanca;

    final nova = resposta.itens ?? const <ItemGrade>[];
    try {
      // Um por vez: TV costuma ter rede e disco modestos.
      for (final item in nova) {
        await baixador.garantir(item);
      }
    } on UrlVencida {
      return ResultadoCiclo.downloadIncompleto;
    } on DownloadFalhou {
      return ResultadoCiclo.downloadIncompleto;
    }

    await banco.trocarGrade(nova, resposta.versaoGrade);
    await baixador.limpar(nova);
    return ResultadoCiclo.gradeTrocada;
  }
}
