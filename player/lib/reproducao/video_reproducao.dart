import 'dart:io';

import 'package:flutter/widgets.dart';
import 'package:video_player/video_player.dart';

/// O que o loop precisa de um vídeo. Interface para os testes simularem fim,
/// erro e eventos repetidos sem o player nativo.
abstract class VideoReproducao {
  Future<void> inicializar();
  Future<void> tocar();
  Future<void> descartar();

  /// Chamado no fim do vídeo. Pode disparar mais de uma vez: quem ouve trata.
  set aoTerminar(VoidCallback? acao);

  /// Chamado em erro de reprodução depois de inicializado.
  set aoFalhar(VoidCallback? acao);

  Widget visual();
}

/// [VideoReproducao] sobre o `video_player` (ExoPlayer no Android).
class VideoDoAparelho implements VideoReproducao {
  VideoDoAparelho(File arquivo) : _controlador = VideoPlayerController.file(arquivo);

  final VideoPlayerController _controlador;
  VoidCallback? _aoTerminar;
  VoidCallback? _aoFalhar;
  bool _ouvindo = false;

  @override
  set aoTerminar(VoidCallback? acao) => _aoTerminar = acao;

  @override
  set aoFalhar(VoidCallback? acao) => _aoFalhar = acao;

  @override
  Future<void> inicializar() async {
    await _controlador.initialize();
    _controlador.addListener(_aoMudar);
    _ouvindo = true;
  }

  void _aoMudar() {
    final v = _controlador.value;
    if (v.hasError) {
      _aoFalhar?.call();
    } else if (v.isInitialized && v.isCompleted) {
      _aoTerminar?.call();
    }
  }

  @override
  Future<void> tocar() => _controlador.play();

  @override
  Future<void> descartar() async {
    // Tira o ouvinte antes do dispose: depois daqui nenhum evento deste
    // vídeo chega ao loop.
    if (_ouvindo) _controlador.removeListener(_aoMudar);
    _ouvindo = false;
    _aoTerminar = null;
    _aoFalhar = null;
    await _controlador.dispose();
  }

  @override
  Widget visual() => Center(
        child: AspectRatio(aspectRatio: _controlador.value.aspectRatio, child: VideoPlayer(_controlador)),
      );
}
