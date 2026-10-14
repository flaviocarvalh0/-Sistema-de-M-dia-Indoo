import 'dart:async';
import 'dart:io';

import 'package:fake_async/fake_async.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:midia_indoor_player/dados/modelos.dart';
import 'package:midia_indoor_player/reproducao/controlador_reproducao.dart';
import 'package:midia_indoor_player/reproducao/video_reproducao.dart';

/// Vídeo simulado: o teste decide quando termina, falha ou acaba de abrir.
class VideoFalso implements VideoReproducao {
  VideoFalso(this.nome, {this.falhaAoAbrir = false, Completer<void>? abertura}) : _abertura = abertura;

  final String nome;
  final bool falhaAoAbrir;
  final Completer<void>? _abertura;
  VoidCallback? _aoTerminar;
  VoidCallback? _aoFalhar;
  bool tocando = false;
  bool descartado = false;

  @override
  set aoTerminar(VoidCallback? acao) => _aoTerminar = acao;

  @override
  set aoFalhar(VoidCallback? acao) => _aoFalhar = acao;

  @override
  Future<void> inicializar() async {
    if (_abertura != null) await _abertura.future;
    if (falhaAoAbrir) throw Exception('arquivo corrompido');
  }

  @override
  Future<void> tocar() async => tocando = true;

  @override
  Future<void> descartar() async {
    descartado = true;
    tocando = false;
    _aoTerminar = null;
    _aoFalhar = null;
  }

  /// O ExoPlayer avisa o fim mais de uma vez: simula isso.
  void terminar({int vezes = 1}) {
    for (var i = 0; i < vezes; i++) {
      _aoTerminar?.call();
    }
  }

  void falhar() => _aoFalhar?.call();

  @override
  Widget visual() => const SizedBox.shrink();
}

ItemGrade item(int ordem, {TipoMidia tipo = TipoMidia.imagem, int duracao = 10}) => ItemGrade(
      ordem: ordem,
      midiaId: ordem,
      tipo: tipo,
      tipoConteudo: tipo == TipoMidia.video ? 'video/mp4' : 'image/png',
      hash: 'h$ordem',
      tamanhoBytes: 1,
      duracaoSegundos: duracao,
      diasSemana: 127,
    );

void main() {
  late List<ItemGrade> grade;
  late List<VideoFalso> videos;
  late Map<String, VideoFalso Function(String)> fabricas;
  late ControladorReproducao loop;

  ControladorReproducao criar() => ControladorReproducao(
        itensDeHoje: () => grade,
        arquivoDe: (i) => File(i.hash),
        criarVideo: (arquivo) {
          final nome = arquivo.path;
          final v = (fabricas[nome] ?? (n) => VideoFalso(n))(nome);
          videos.add(v);
          return v;
        },
      );

  setUp(() {
    grade = [];
    videos = [];
    fabricas = {};
  });

  List<int?> tocados(FakeAsync async, int voltas, Duration passo) {
    final r = <int?>[];
    for (var i = 0; i < voltas; i++) {
      r.add(loop.atual?.ordem);
      async.elapse(passo);
    }
    return r;
  }

  test('imagens tocam na ordem, pela duração, e o loop dá a volta', () {
    fakeAsync((async) {
      grade = [item(0), item(1), item(2)];
      loop = criar()..iniciar();
      async.flushMicrotasks();
      expect(tocados(async, 5, const Duration(seconds: 10)), [0, 1, 2, 0, 1]);
      loop.dispose();
    });
  });

  test('fim de vídeo avisado várias vezes avança um item só', () {
    fakeAsync((async) {
      grade = [item(0, tipo: TipoMidia.video, duracao: 30), item(1), item(2)];
      loop = criar()..iniciar();
      async.flushMicrotasks();
      expect(loop.atual?.ordem, 0);

      videos.single.terminar(vezes: 3);
      async.flushMicrotasks();
      expect(loop.atual?.ordem, 1, reason: 'não pode pular para o 2');
      expect(videos.single.descartado, isTrue);

      async.elapse(const Duration(seconds: 10));
      expect(loop.atual?.ordem, 2);
      loop.dispose();
    });
  });

  test('fim repetido com vídeo depois de vídeo: um só vivo e nenhum item pulado', () {
    fakeAsync((async) {
      grade = [
        item(0, tipo: TipoMidia.video, duracao: 30),
        item(1, tipo: TipoMidia.video, duracao: 30),
        item(2, tipo: TipoMidia.video, duracao: 30),
      ];
      loop = criar()..iniciar();
      async.flushMicrotasks();

      videos.single.terminar(vezes: 3);
      async.flushMicrotasks();
      expect(loop.atual?.ordem, 1);
      expect(videos.map((v) => v.nome), ['h0', 'h1'], reason: 'o vídeo seguinte abre uma vez só (sem piscar)');

      videos.last.terminar(vezes: 2);
      async.flushMicrotasks();
      expect(loop.atual?.ordem, 2, reason: 'sem pular o 2');
      expect(videos.map((v) => v.nome), ['h0', 'h1', 'h2']);

      // Nenhum timer órfão de um vídeo anterior avança o loop antes da hora.
      async.elapse(const Duration(seconds: 34));
      expect(loop.atual?.ordem, 2);
      loop.dispose();
    });
  });

  test('timer de segurança de um vídeo que já terminou não pula item', () {
    fakeAsync((async) {
      grade = [item(0, tipo: TipoMidia.video, duracao: 20), item(1, duracao: 60), item(2)];
      loop = criar()..iniciar();
      async.flushMicrotasks();

      async.elapse(const Duration(seconds: 19));
      videos.single.terminar();
      async.flushMicrotasks();
      expect(loop.atual?.ordem, 1);

      // Passa do limite de segurança do vídeo (20 s + 5 s): nada acontece.
      async.elapse(const Duration(seconds: 10));
      expect(loop.atual?.ordem, 1);
      loop.dispose();
    });
  });

  test('vídeo travado avança pelo limite de segurança', () {
    fakeAsync((async) {
      grade = [item(0, tipo: TipoMidia.video, duracao: 20), item(1)];
      loop = criar()..iniciar();
      async.flushMicrotasks();
      async.elapse(const Duration(seconds: 24));
      expect(loop.atual?.ordem, 0);
      async.elapse(const Duration(seconds: 2));
      expect(loop.atual?.ordem, 1);
      loop.dispose();
    });
  });

  test('grade nova entra no fim do item atual, sem interromper o vídeo', () {
    fakeAsync((async) {
      grade = [item(0, tipo: TipoMidia.video, duracao: 30), item(1), item(2)];
      loop = criar()..iniciar();
      async.flushMicrotasks();

      grade = [item(0, tipo: TipoMidia.video, duracao: 30), item(5), item(6)];
      loop.gradeAtualizada();
      async.flushMicrotasks();
      expect(loop.atual?.ordem, 0);
      expect(videos.single.tocando, isTrue, reason: 'o vídeo atual segue tocando');

      videos.single.terminar();
      async.flushMicrotasks();
      expect(loop.atual?.ordem, 5, reason: 'o próximo vem da grade nova');
      loop.dispose();
    });
  });

  test('item atual que saiu da grade: segue para o seguinte na ordem', () {
    fakeAsync((async) {
      grade = [item(0), item(1), item(2), item(3)];
      loop = criar()..iniciar();
      async.flushMicrotasks();
      async.elapse(const Duration(seconds: 10));
      expect(loop.atual?.ordem, 1);

      grade = [item(0), item(3)];
      async.elapse(const Duration(seconds: 10));
      expect(loop.atual?.ordem, 3);
      loop.dispose();
    });
  });

  test('arquivo que não abre é pulado', () {
    fakeAsync((async) {
      grade = [item(0), item(1, tipo: TipoMidia.video), item(2)];
      fabricas['h1'] = (n) => VideoFalso(n, falhaAoAbrir: true);
      loop = criar()..iniciar();
      async.flushMicrotasks();
      async.elapse(const Duration(seconds: 10));
      async.elapse(ControladorReproducao.pausaAposFalha);
      expect(loop.atual?.ordem, 2);
      expect(videos.single.descartado, isTrue);
      loop.dispose();
    });
  });

  test('tudo falhando vai para a espera e tenta de novo depois', () {
    fakeAsync((async) {
      grade = [item(0, tipo: TipoMidia.video), item(1, tipo: TipoMidia.video)];
      fabricas['h0'] = (n) => VideoFalso(n, falhaAoAbrir: true);
      fabricas['h1'] = (n) => VideoFalso(n, falhaAoAbrir: true);
      loop = criar()..iniciar();
      async.flushMicrotasks();
      async.elapse(const Duration(seconds: 2));
      expect(loop.atual, isNull, reason: 'volta inteira falhou: tela de espera');
      final tentativas = videos.length;

      async.elapse(const Duration(seconds: 5));
      expect(videos.length, tentativas, reason: 'não fica tentando sem parar');

      fabricas.clear();
      async.elapse(ControladorReproducao.reavaliarEspera);
      async.flushMicrotasks();
      expect(loop.atual?.ordem, isNotNull);
      loop.dispose();
    });
  });

  test('erro durante a reprodução pula para o próximo', () {
    fakeAsync((async) {
      grade = [item(0, tipo: TipoMidia.video, duracao: 30), item(1)];
      loop = criar()..iniciar();
      async.flushMicrotasks();
      videos.single.falhar();
      videos.single.falhar();
      async.elapse(ControladorReproducao.pausaAposFalha);
      expect(loop.atual?.ordem, 1);
      loop.dispose();
    });
  });

  test('vídeo lento para abrir não vira dois itens ao mesmo tempo', () {
    fakeAsync((async) {
      final abertura = Completer<void>();
      grade = [item(0, tipo: TipoMidia.video, duracao: 30), item(1)];
      fabricas['h0'] = (n) => VideoFalso(n, abertura: abertura);
      loop = criar()..iniciar();
      async.flushMicrotasks();
      expect(loop.atual, isNull);

      // Enquanto abre, chega grade nova (estava "na espera"): começa outro ciclo.
      loop.gradeAtualizada();
      async.flushMicrotasks();
      abertura.complete();
      async.flushMicrotasks();

      final ativos = videos.where((v) => !v.descartado).toList();
      expect(ativos, hasLength(1), reason: 'só um vídeo vivo');
      expect(loop.atual?.ordem, 0);
      loop.dispose();
    });
  });

  test('grade vazia mostra a espera e começa quando a grade chega', () {
    fakeAsync((async) {
      loop = criar()..iniciar();
      async.flushMicrotasks();
      expect(loop.atual, isNull);

      grade = [item(0)];
      loop.gradeAtualizada();
      async.flushMicrotasks();
      expect(loop.atual?.ordem, 0);
      loop.dispose();
    });
  });

  test('fechar a tela descarta o vídeo e para os timers', () {
    fakeAsync((async) {
      grade = [item(0, tipo: TipoMidia.video, duracao: 30), item(1)];
      loop = criar()..iniciar();
      async.flushMicrotasks();
      loop.dispose();
      expect(videos.single.descartado, isTrue);
      async.elapse(const Duration(minutes: 5));
      expect(videos, hasLength(1));
    });
  });
}
