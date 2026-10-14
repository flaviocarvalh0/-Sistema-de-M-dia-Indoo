import 'dart:async';
import 'dart:io';

import 'package:flutter/foundation.dart';

import '../dados/modelos.dart';
import 'filtro_do_dia.dart';
import 'video_reproducao.dart';

/// O loop da TV, sem tela: decide o próximo item, toca, trata fim e falha.
///
/// Cada item iniciado ganha uma **geração**. Fim, falha e timers só valem se
/// ainda forem da geração atual: o fim de vídeo repetido, o timer de
/// segurança de um item que já acabou ou a imagem que falhou depois de
/// trocada são ignorados. Antes disso, dois eventos do mesmo item podiam
/// iniciar dois "próximos" ao mesmo tempo e o loop pulava itens.
///
/// A grade nova não interrompe nada: o próximo item é sempre escolhido da
/// grade mais recente, então ela entra no fim do item atual.
class ControladorReproducao extends ChangeNotifier {
  ControladorReproducao({
    required this.itensDeHoje,
    required this.arquivoDe,
    required this.criarVideo,
  });

  /// Itens que tocam agora, já filtrados pelo dia e com arquivo baixado.
  final List<ItemGrade> Function() itensDeHoje;
  final File Function(ItemGrade item) arquivoDe;
  final VideoReproducao Function(File arquivo) criarVideo;

  /// Margem além de `duracaoSegundos` antes de considerar o vídeo travado.
  static const folgaVideo = Duration(seconds: 5);

  /// Sem nada para tocar, confere de novo neste intervalo (o dia pode virar).
  static const reavaliarEspera = Duration(seconds: 30);

  /// Pausa antes de tentar o próximo depois de uma falha.
  static const pausaAposFalha = Duration(milliseconds: 500);

  ItemGrade? _atual;
  VideoReproducao? _video;
  Timer? _timer;
  int _geracao = 0;
  int _falhasSeguidas = 0;
  bool _encerrado = false;

  /// Item na tela; nulo = tela de espera.
  ItemGrade? get atual => _atual;

  /// Vídeo do item atual (só para vídeo, já inicializado).
  VideoReproducao? get video => _video;

  /// Quantos itens já começaram (para os testes e o diagnóstico).
  int get geracao => _geracao;

  void iniciar() => _tocarProximo();

  /// A grade mudou. Tocando, nada muda agora (a nova entra no fim do item);
  /// na espera, começa já.
  void gradeAtualizada() {
    if (_atual == null && !_encerrado) _tocarProximo();
  }

  /// A imagem do item atual não abriu.
  void imagemFalhou(ItemGrade item) {
    if (identical(item, _atual) || item.ordem == _atual?.ordem) _falhar(_geracao);
  }

  Future<void> _tocarProximo() async {
    if (_encerrado) return;
    final geracao = ++_geracao;
    _timer?.cancel();

    final doDia = itensDeHoje();
    final proximo = FiltroDoDia.proximo(doDia, _atual?.ordem);

    // Nada para hoje, ou todos os arquivos falharam numa volta inteira.
    if (proximo == null || (_falhasSeguidas > 0 && _falhasSeguidas >= doDia.length)) {
      _falhasSeguidas = 0;
      await _trocarVideo(null);
      _atual = null;
      notifyListeners();
      _timer = Timer(reavaliarEspera, () => _seAtual(geracao, _tocarProximo));
      return;
    }

    if (proximo.tipo == TipoMidia.imagem) {
      await _trocarVideo(null);
      if (geracao != _geracao) return;
      _atual = proximo;
      notifyListeners();
      _timer = Timer(Duration(seconds: proximo.duracaoSegundos), () => _terminar(geracao));
      return;
    }

    final video = criarVideo(arquivoDe(proximo));
    try {
      await video.inicializar();
    } catch (_) {
      await video.descartar();
      if (geracao == _geracao) {
        _atual = proximo;
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
    _atual = proximo;
    notifyListeners();
    await video.tocar();
    _timer = Timer(Duration(seconds: proximo.duracaoSegundos) + folgaVideo, () => _terminar(geracao));
  }

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
