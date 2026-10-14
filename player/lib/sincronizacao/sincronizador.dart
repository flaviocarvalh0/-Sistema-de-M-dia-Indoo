import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:path/path.dart' as p;

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

  /// 403: tela inativa. Continua tocando.
  suspensa,

  /// Sem rede, 429 ou 5xx. Continua tocando.
  semConexao,

  /// Algum arquivo não baixou (ou a URL venceu): a grade antiga continua e o
  /// próximo ciclo deve vir logo. Vale também para imagem de notícia.
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
    this.composicao = const Composicao(),
    this.noticias = const [],
    this.clima,
  });

  final List<ItemGrade> grade;

  /// Como montar a fila (notícias, embaralhar, relógio, clima).
  final Composicao composicao;

  /// Notícias com a imagem já baixada (ou sem imagem), mais recentes primeiro.
  final List<Noticia> noticias;

  /// Última leitura do clima que chegou (a tela confere a validade).
  final Clima? clima;
  final String fusoHorario;
  final String? nomeTela;
  final ResultadoCiclo? ultimoResultado;

  /// Último contato bem-sucedido com o servidor.
  final DateTime? ultimaSincronizacao;

  bool get desvinculada =>
      ultimoResultado == ResultadoCiclo.desvinculada || ultimoResultado == ResultadoCiclo.semCredencial;
}

/// Um ciclo: pergunta ao servidor, baixa o que falta, troca a grade de uma
/// vez e apaga o que saiu (regras 2 e 3 do contrato). As notícias têm versão
/// própria e as imagens delas ficam numa pasta separada das mídias.
class Sincronizador {
  Sincronizador({
    required this.api,
    required this.banco,
    required this.credenciais,
    required this.baixador,
    required this.versaoApp,
    BaixadorMidia? baixadorNoticias,
  }) : baixadorNoticias = baixadorNoticias ?? BaixadorMidia(Directory(p.join(baixador.pasta.parent.path, 'noticias')));

  final ApiDispositivo api;
  final BancoLocal banco;
  final Credenciais credenciais;
  final BaixadorMidia baixador;
  final String versaoApp;

  /// Imagens das notícias (`<documentos>/noticias`).
  final BaixadorMidia baixadorNoticias;

  final situacao = ValueNotifier<SituacaoPlayer?>(null);

  bool _rodando = false;
  ResultadoCiclo? _ultimo;
  DateTime? _ultimaSincronizacao;

  /// Intervalo pedido pelo servidor (padrão 60 s).
  Duration intervalo = const Duration(seconds: 60);

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
      composicao: estado.composicao,
      noticias: (await banco.lerNoticias()).where((n) => !n.temImagem || baixadorNoticias.pronto(n)).toList(),
      clima: estado.clima,
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
        versaoNoticias: estado.versaoNoticias,
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
      composicao: resposta.composicao,
      gravarClima: true,
      clima: resposta.clima,
    );

    final noticiasCompletas = await _atualizarNoticias(resposta);

    if (!resposta.alterada) {
      return noticiasCompletas ? ResultadoCiclo.semMudanca : ResultadoCiclo.downloadIncompleto;
    }

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
    return noticiasCompletas ? ResultadoCiclo.gradeTrocada : ResultadoCiclo.downloadIncompleto;
  }

  /// Baixa as imagens das notícias novas e troca a lista. Notícia cuja imagem
  /// não baixou fica de fora desta vez e a versão não é gravada: o próximo
  /// ciclo pede tudo de novo (com URLs novas). Devolve se ficou completa.
  Future<bool> _atualizarNoticias(RespostaSincronizacao resposta) async {
    final recebidas = resposta.noticias;
    final versao = resposta.versaoNoticias;
    if (recebidas == null || versao == null) return true;

    final prontas = <Noticia>[];
    var completas = true;
    for (final noticia in recebidas) {
      if (!noticia.temImagem) {
        prontas.add(noticia);
        continue;
      }
      try {
        await baixadorNoticias.garantir(noticia);
        prontas.add(noticia);
      } on UrlVencida {
        completas = false;
      } on DownloadFalhou {
        completas = false;
      }
    }

    await banco.trocarNoticias(prontas, completas ? versao : '');
    await baixadorNoticias.limpar(prontas.where((n) => n.temImagem));
    return completas;
  }
}
