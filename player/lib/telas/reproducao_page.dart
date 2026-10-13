import 'dart:async';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:video_player/video_player.dart';

import '../app.dart';
import '../config/configuracao.dart';
import '../dados/modelos.dart';
import '../reproducao/filtro_do_dia.dart';
import '../sincronizacao/sincronizador.dart';
import 'pareamento_page.dart';

/// O loop da TV (regras 5 a 8 do contrato): só arquivo local, filtro do dia
/// recalculado a cada item, sincronização em segundo plano.
///
/// Botão OK (ou Menu/Info) do controle mostra as informações da TV e o
/// atalho para parear de novo.
class ReproducaoPage extends StatefulWidget {
  const ReproducaoPage({super.key, required this.servicos});

  final Servicos servicos;

  @override
  State<ReproducaoPage> createState() => _ReproducaoPageState();
}

class _ReproducaoPageState extends State<ReproducaoPage> {
  /// Margem além de `duracaoSegundos` antes de considerar o vídeo travado.
  static const _folgaVideo = Duration(seconds: 5);

  /// Sem nada para tocar, confere de novo neste intervalo (o dia pode virar).
  static const _reavaliarEspera = Duration(seconds: 30);

  /// Depois de download incompleto, tenta logo (URL nova, rede de volta).
  static const _tentarDeNovo = Duration(minutes: 1);

  Sincronizador get _sync => widget.servicos.sincronizador;

  ItemGrade? _atual;
  VideoPlayerController? _video;
  Timer? _timerItem;
  Timer? _timerSync;
  Timer? _timerInfo;
  int _falhasSeguidas = 0;
  bool _mostrarInfo = false;
  bool _saindo = false;

  @override
  void initState() {
    super.initState();
    _sync.situacao.addListener(_aoMudarSituacao);
    _sync.carregar().then((_) {
      _tocarProximo();
      _sincronizar();
    });
  }

  @override
  void dispose() {
    _saindo = true;
    _sync.situacao.removeListener(_aoMudarSituacao);
    _timerItem?.cancel();
    _timerSync?.cancel();
    _timerInfo?.cancel();
    _video?.dispose();
    super.dispose();
  }

  void _aoMudarSituacao() {
    if (!mounted) return;
    setState(() {});
    // Estava na espera e chegou grade: começa sem esperar o próximo ciclo.
    if (_atual == null) _tocarProximo();
  }

  Future<void> _sincronizar() async {
    _timerSync?.cancel();
    final resultado = await _sync.sincronizar();
    if (_saindo) return;
    final espera = resultado == ResultadoCiclo.downloadIncompleto ? _tentarDeNovo : _sync.intervalo;
    _timerSync = Timer(espera, _sincronizar);
  }

  // ── Loop ────────────────────────────────────────────────────────────────

  List<ItemGrade> _itensDeHoje() {
    final s = _sync.situacao.value;
    if (s == null) return const [];
    final hoje = FiltroDoDia.hoje(s.fusoHorario, DateTime.now());
    return FiltroDoDia.itensDoDia(s.grade, hoje).where(widget.servicos.baixador.pronto).toList();
  }

  Future<void> _tocarProximo() async {
    if (_saindo) return;
    _timerItem?.cancel();

    final doDia = _itensDeHoje();
    final proximo = FiltroDoDia.proximo(doDia, _atual?.ordem);

    // Nada para hoje, ou todos os arquivos falharam numa volta inteira.
    if (proximo == null || _falhasSeguidas >= doDia.length) {
      _falhasSeguidas = 0;
      await _trocarVideo(null);
      setState(() => _atual = null);
      _timerItem = Timer(_reavaliarEspera, _tocarProximo);
      return;
    }

    final arquivo = widget.servicos.baixador.arquivoDe(proximo);
    if (proximo.tipo == TipoMidia.video) {
      await _tocarVideo(proximo, arquivo);
    } else {
      await _trocarVideo(null);
      setState(() => _atual = proximo);
      _timerItem = Timer(Duration(seconds: proximo.duracaoSegundos), _aoTerminarItem);
    }
  }

  Future<void> _tocarVideo(ItemGrade item, File arquivo) async {
    final controlador = VideoPlayerController.file(arquivo);
    try {
      await controlador.initialize();
    } catch (_) {
      await controlador.dispose();
      _aoFalhar(item);
      return;
    }
    if (_saindo) {
      await controlador.dispose();
      return;
    }

    await _trocarVideo(controlador);
    setState(() => _atual = item);
    controlador.addListener(() {
      final v = controlador.value;
      if (v.hasError) {
        _aoFalhar(item);
      } else if (v.isInitialized && !v.isPlaying && v.position >= v.duration && v.duration > Duration.zero) {
        _aoTerminarItem();
      }
    });
    await controlador.play();
    _timerItem = Timer(Duration(seconds: item.duracaoSegundos) + _folgaVideo, _aoTerminarItem);
  }

  Future<void> _trocarVideo(VideoPlayerController? novo) async {
    final antigo = _video;
    _video = novo;
    await antigo?.dispose();
  }

  void _aoTerminarItem() {
    if (_saindo) return;
    _falhasSeguidas = 0;
    _timerItem?.cancel();
    _timerItem = null;
    // Adia para fora do listener do vídeo.
    scheduleMicrotask(_tocarProximo);
  }

  /// Arquivo que não abre é pulado (regra 6).
  void _aoFalhar(ItemGrade item) {
    if (_saindo) return;
    _falhasSeguidas++;
    _atual = item;
    _timerItem?.cancel();
    _timerItem = Timer(const Duration(milliseconds: 500), _tocarProximo);
  }

  // ── Controle remoto ─────────────────────────────────────────────────────

  static final _teclasInfo = {
    LogicalKeyboardKey.select,
    LogicalKeyboardKey.enter,
    LogicalKeyboardKey.numpadEnter,
    LogicalKeyboardKey.contextMenu,
    LogicalKeyboardKey.info,
    LogicalKeyboardKey.gameButtonA,
  };

  KeyEventResult _aoTeclar(FocusNode _, KeyEvent evento) {
    if (evento is! KeyDownEvent) return KeyEventResult.ignored;
    if (!_teclasInfo.contains(evento.logicalKey) || _mostrarInfo) return KeyEventResult.ignored;
    _abrirInfo();
    return KeyEventResult.handled;
  }

  void _abrirInfo() {
    setState(() => _mostrarInfo = true);
    _timerInfo?.cancel();
    _timerInfo = Timer(const Duration(seconds: 20), _fecharInfo);
  }

  void _fecharInfo() {
    _timerInfo?.cancel();
    if (mounted) setState(() => _mostrarInfo = false);
  }

  Future<void> _parearDeNovo() async {
    _fecharInfo();
    await Navigator.of(context).push(
      MaterialPageRoute<void>(builder: (_) => PareamentoPage(servicos: widget.servicos, podeVoltar: true)),
    );
  }

  // ── Tela ────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    final situacao = _sync.situacao.value;
    return Scaffold(
      backgroundColor: Colors.black,
      body: Focus(
        autofocus: true,
        onKeyEvent: _aoTeclar,
        child: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: _abrirInfo,
          child: Stack(
            fit: StackFit.expand,
            children: [
              AnimatedSwitcher(
                duration: const Duration(milliseconds: 400),
                child: KeyedSubtree(key: ValueKey(_atual?.ordem ?? -1), child: _conteudo(situacao)),
              ),
              if (situacao?.desvinculada ?? false) const _AvisoDesvinculada(),
              if (_mostrarInfo)
                _PainelInfo(
                  situacao: situacao,
                  servidor: widget.servicos.urlServidor,
                  itensHoje: _itensDeHoje().length,
                  aoParear: _parearDeNovo,
                  aoFechar: _fecharInfo,
                ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _conteudo(SituacaoPlayer? situacao) {
    final item = _atual;
    if (item == null) return _Espera(nomeTela: situacao?.nomeTela);
    final video = _video;
    if (item.tipo == TipoMidia.video && video != null && video.value.isInitialized) {
      return Center(child: AspectRatio(aspectRatio: video.value.aspectRatio, child: VideoPlayer(video)));
    }
    if (item.tipo == TipoMidia.imagem) {
      return Image.file(
        widget.servicos.baixador.arquivoDe(item),
        fit: BoxFit.contain,
        errorBuilder: (_, __, ___) {
          WidgetsBinding.instance.addPostFrameCallback((_) => _aoFalhar(item));
          return const SizedBox.shrink();
        },
      );
    }
    return const SizedBox.shrink();
  }
}

/// Regra 7: grade vazia ou nada para hoje.
class _Espera extends StatelessWidget {
  const _Espera({this.nomeTela});

  final String? nomeTela;

  @override
  Widget build(BuildContext context) {
    final tema = Theme.of(context).textTheme;
    return ColoredBox(
      color: const Color(0xFF0F1F2E),
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.tv, size: 96, color: Color(0xFF2DB3A8)),
            const SizedBox(height: 24),
            Text(nomeTela ?? 'Mídia Indoor', style: tema.displaySmall?.copyWith(color: Colors.white)),
            const SizedBox(height: 8),
            Text('Aguardando conteúdo', style: tema.titleLarge?.copyWith(color: Colors.white54)),
          ],
        ),
      ),
    );
  }
}

class _AvisoDesvinculada extends StatelessWidget {
  const _AvisoDesvinculada();

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.bottomRight,
      child: Container(
        margin: const EdgeInsets.all(24),
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
        decoration: BoxDecoration(color: Colors.black.withValues(alpha: 0.75), borderRadius: BorderRadius.circular(12)),
        child: const Text(
          'TV não pareada · pressione OK para parear',
          style: TextStyle(color: Colors.white, fontSize: 18),
        ),
      ),
    );
  }
}

class _PainelInfo extends StatelessWidget {
  const _PainelInfo({
    required this.situacao,
    required this.servidor,
    required this.itensHoje,
    required this.aoParear,
    required this.aoFechar,
  });

  final SituacaoPlayer? situacao;
  final String servidor;
  final int itensHoje;
  final VoidCallback aoParear;
  final VoidCallback aoFechar;

  static String _conexao(ResultadoCiclo? r) => switch (r) {
        ResultadoCiclo.gradeTrocada || ResultadoCiclo.semMudanca => 'Conectada',
        ResultadoCiclo.downloadIncompleto => 'Conectada · baixando conteúdo',
        ResultadoCiclo.suspensa => 'Tela inativa ou parceiro bloqueado no painel',
        ResultadoCiclo.semConexao => 'Sem conexão · tocando o que já baixou',
        ResultadoCiclo.desvinculada || ResultadoCiclo.semCredencial => 'Não pareada',
        null => 'Conectando…',
      };

  static String _hora(DateTime? d) =>
      d == null ? '—' : '${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}';

  @override
  Widget build(BuildContext context) {
    final s = situacao;
    final linhas = <(String, String)>[
      ('Tela', s?.nomeTela ?? '—'),
      ('Conexão', _conexao(s?.ultimoResultado)),
      ('Última sincronização', _hora(s?.ultimaSincronizacao)),
      ('Itens na grade', '${s?.grade.length ?? 0} ($itensHoje para hoje)'),
      ('Servidor', servidor),
      ('Versão do app', Configuracao.versaoApp),
    ];
    return ColoredBox(
      color: Colors.black54,
      child: Center(
        child: Card(
          child: Padding(
            padding: const EdgeInsets.all(32),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 640),
              child: FocusTraversalGroup(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text('Mídia Indoor', style: Theme.of(context).textTheme.headlineMedium),
                    const SizedBox(height: 16),
                    for (final (rotulo, valor) in linhas)
                      Padding(
                        padding: const EdgeInsets.symmetric(vertical: 4),
                        child: Row(children: [
                          SizedBox(width: 220, child: Text(rotulo, style: const TextStyle(color: Colors.white60))),
                          Expanded(child: Text(valor)),
                        ]),
                      ),
                    const SizedBox(height: 24),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.end,
                      children: [
                        OutlinedButton(onPressed: aoParear, child: const Text('Parear de novo')),
                        const SizedBox(width: 12),
                        FilledButton(autofocus: true, onPressed: aoFechar, child: const Text('Fechar')),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
