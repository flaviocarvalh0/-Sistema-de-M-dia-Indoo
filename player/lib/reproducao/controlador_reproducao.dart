import 'dart:async';
import 'dart:io';

import 'package:flutter/foundation.dart';

import '../dados/modelos.dart';
import 'composicao_fila.dart';
import 'video_reproducao.dart';

/// O loop da TV, sem tela: decide o próximo passo, toca, trata fim e falha.
///
/// Cada passo iniciado ganha uma **geração**. Fim, falha e timers só valem se
/// ainda forem da geração atual: o fim de vídeo repetido, o timer de
/// segurança de um item que já acabou ou a imagem que falhou depois de
/// trocada são ignorados. Antes disso, dois eventos do mesmo item podiam
/// iniciar dois "próximos" ao mesmo tempo e o loop pulava itens.
///
/// A fila de cada volta vem de [compor] (anúncios do dia, notícias,
/// embaralhar — ver [ComposicaoFila]) e é montada de novo a cada volta.
/// Anúncio que saiu da grade (ou perdeu o arquivo) no meio da volta é
/// pulado. A grade nova não interrompe nada: entra no fim do item atual,
/// seguindo a partir da posição dele.
class ControladorReproducao extends ChangeNotifier {
  ControladorReproducao({
    required this.itensDeHoje,
    required this.arquivoDe,
    required this.criarVideo,
    List<EntradaFila> Function(List<ItemGrade> anuncios)? compor,
  }) : compor = compor ?? ((anuncios) => [for (final a in anuncios) EntradaFila.anuncio(a)]);

  /// Anúncios que tocam agora, já filtrados pelo dia e com arquivo baixado.
  final List<ItemGrade> Function() itensDeHoje;

  /// Monta a fila de uma volta a partir dos anúncios de hoje.
  final List<EntradaFila> Function(List<ItemGrade> anuncios) compor;

  final File Function(ItemGrade item) arquivoDe;
  final VideoReproducao Function(File arquivo) criarVideo;

  /// Margem além de `duracaoSegundos` antes de considerar o vídeo travado.
  static const folgaVideo = Duration(seconds: 5);

  /// Sem nada para tocar, confere de novo neste intervalo (o dia pode virar).
  static const reavaliarEspera = Duration(seconds: 30);

  /// Pausa antes de tentar o próximo depois de uma falha.
  static const pausaAposFalha = Duration(milliseconds: 500);

  EntradaFila? _entrada;
  VideoReproducao? _video;
  Timer? _timer;
  int _geracao = 0;
  int _falhasSeguidas = 0;
  bool _encerrado = false;

  List<EntradaFila> _fila = const [];
  int _indice = -1;
  bool _filaDesatualizada = false;

  /// Passo na tela; nulo = tela de espera.
  EntradaFila? get entrada => _entrada;

  /// Anúncio na tela (nulo na espera ou durante uma notícia).
  ItemGrade? get atual => _entrada?.anuncio;

  /// Notícia na tela.
  Noticia? get noticia => _entrada?.noticia;

  /// Cartão informativo na tela (tempo ou cotações).
  TipoCartao? get cartao => _entrada?.cartao;

  /// Vídeo do item atual (só para vídeo, já inicializado).
  VideoReproducao? get video => _video;

  /// Quantos passos já começaram (para os testes e o diagnóstico).
  int get geracao => _geracao;

  void iniciar() => _tocarProximo();

  /// A grade (ou as notícias, ou a composição) mudou. Tocando, nada muda
  /// agora: a fila nova entra no fim do item atual. Na espera, começa já.
  void gradeAtualizada() {
    _filaDesatualizada = true;
    if (_entrada == null && !_encerrado) _tocarProximo();
  }

  /// A imagem do anúncio atual não abriu.
  void imagemFalhou(ItemGrade item) {
    final atual = _entrada?.anuncio;
    if (identical(item, atual) || (atual != null && item.ordem == atual.ordem)) _falhar(_geracao);
  }

  /// A imagem da notícia atual não abriu.
  void noticiaFalhou(Noticia noticia) {
    if (_entrada?.noticia?.id == noticia.id) _falhar(_geracao);
  }

  Future<void> _tocarProximo() async {
    if (_encerrado) return;
    final geracao = ++_geracao;
    _timer?.cancel();

    final proxima = _escolherProxima();

    // Nada para hoje, ou todos falharam numa volta inteira.
    if (proxima == null || (_falhasSeguidas > 0 && _falhasSeguidas >= _fila.length)) {
      _falhasSeguidas = 0;
      _fila = const [];
      _indice = -1;
      await _trocarVideo(null);
      _entrada = null;
      notifyListeners();
      _timer = Timer(reavaliarEspera, () => _seAtual(geracao, _tocarProximo));
      return;
    }

    final anuncio = proxima.anuncio;
    if (anuncio == null || anuncio.tipo == TipoMidia.imagem) {
      await _trocarVideo(null);
      if (geracao != _geracao) return;
      _entrada = proxima;
      notifyListeners();
      _timer = Timer(Duration(seconds: proxima.duracaoSegundos), () => _terminar(geracao));
      return;
    }

    final video = criarVideo(arquivoDe(anuncio));
    try {
      await video.inicializar();
    } catch (_) {
      await video.descartar();
      if (geracao == _geracao) {
        _entrada = proxima;
        _falhar(geracao);
      }
      return;
    }

    // Enquanto inicializava, algo mais novo pode ter começado (ou a tela fechou).
    if (geracao != _geracao || _encerrado) {
      await video.descartar();
      return;
    }

    video.aoTerminar = () => _terminar(geracao);
    video.aoFalhar = () => _falhar(geracao);
    await _trocarVideo(video);
    _entrada = proxima;
    notifyListeners();
    await video.tocar();
    _timer = Timer(Duration(seconds: anuncio.duracaoSegundos) + folgaVideo, () => _terminar(geracao));
  }

  /// O próximo passo da fila; no fim da volta (ou com grade nova), monta outra.
  EntradaFila? _escolherProxima() {
    final disponiveis = itensDeHoje();
    final chaves = {for (final i in disponiveis) _chave(i)};
    bool valida(EntradaFila e) => e.anuncio == null || chaves.contains(_chave(e.anuncio!));

    if (_filaDesatualizada || _fila.isEmpty) {
      _filaDesatualizada = false;
      _fila = compor(disponiveis);
      // Continua depois do anúncio atual (a grade nova entra no fim dele).
      final atual = _entrada?.anuncio;
      final seguinte = atual == null ? -1 : _fila.indexWhere((e) => e.anuncio != null && e.anuncio!.ordem > atual.ordem);
      _indice = seguinte < 0 ? -1 : seguinte - 1;
    }

    for (var voltas = 0; voltas < 2; voltas++) {
      for (var i = _indice + 1; i < _fila.length; i++) {
        if (valida(_fila[i])) {
          _indice = i;
          return _fila[i];
        }
      }
      // Fim da volta: outra fila (outro sorteio, as próximas notícias).
      _fila = compor(disponiveis);
      _indice = -1;
      if (_fila.isEmpty) return null;
    }
    return null;
  }

  static String _chave(ItemGrade item) => '${item.ordem}:${item.hash}';

  void _terminar(int geracao) {
    if (geracao != _geracao || _encerrado) return;
    _falhasSeguidas = 0;
    // Já marca a próxima geração: um segundo "fim" deste item cai fora.
    _geracao++;
    scheduleMicrotask(_tocarProximo);
  }

  /// Arquivo que não abre é pulado (regra 6 do contrato).
  void _falhar(int geracao) {
    if (geracao != _geracao || _encerrado) return;
    _falhasSeguidas++;
    final nova = ++_geracao;
    _timer?.cancel();
    _timer = Timer(pausaAposFalha, () => _seAtual(nova, _tocarProximo));
  }

  void _seAtual(int geracao, void Function() acao) {
    if (geracao == _geracao && !_encerrado) acao();
  }

  Future<void> _trocarVideo(VideoReproducao? novo) async {
    final antigo = _video;
    if (identical(antigo, novo)) return;
    _video = novo;
    await antigo?.descartar();
  }

  @override
  void dispose() {
    _encerrado = true;
    _timer?.cancel();
    final video = _video;
    _video = null;
    video?.descartar();
    super.dispose();
  }
}
