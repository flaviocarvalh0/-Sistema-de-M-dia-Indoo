import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../app.dart';
import '../dados/modelos.dart';
import '../reproducao/composicao_fila.dart';
import '../reproducao/controlador_reproducao.dart';
import '../reproducao/filtro_do_dia.dart';
import '../reproducao/video_reproducao.dart';
import '../sincronizacao/sincronizador.dart';
import 'cartoes_view.dart';
import 'noticia_view.dart';
import 'painel_info.dart';
import 'pareamento_page.dart';
import 'sobreposicao_relogio_clima.dart';

/// O loop da TV (regras 5 a 9 do contrato): só arquivo local, filtro do dia
/// recalculado a cada volta, fila composta na TV (notícias intercaladas,
/// ordem sorteada), relógio e clima por cima, sincronização em segundo plano.
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
  /// Depois de download incompleto, tenta logo (URL nova, rede de volta).
  static const _tentarDeNovo = Duration(minutes: 1);

  Sincronizador get _sync => widget.servicos.sincronizador;

  final _composicao = ComposicaoFila();

  late final ControladorReproducao _loop = ControladorReproducao(
    itensDeHoje: _itensDeHoje,
    compor: _compor,
    arquivoDe: widget.servicos.baixador.arquivoDe,
    criarVideo: VideoDoAparelho.new,
  );

  Timer? _timerSync;
  Timer? _timerInfo;
  bool _mostrarInfo = false;
  bool _saindo = false;

  @override
  void initState() {
    super.initState();
    _loop.addListener(_redesenhar);
    _sync.situacao.addListener(_aoMudarSituacao);
    _sync.carregar().then((_) {
      if (_saindo) return;
      _loop.iniciar();
      _sincronizar();
    });
  }

  @override
  void dispose() {
    _saindo = true;
    _sync.situacao.removeListener(_aoMudarSituacao);
    _loop.removeListener(_redesenhar);
    _loop.dispose();
    _timerSync?.cancel();
    _timerInfo?.cancel();
    super.dispose();
  }

  void _redesenhar() {
    if (mounted) setState(() {});
  }

  /// O sincronizador publicou outra situação (grade nova, resultado do ciclo).
  void _aoMudarSituacao() {
    if (!mounted) return;
    setState(() {});
    _loop.gradeAtualizada();
  }

  Future<void> _sincronizar() async {
    _timerSync?.cancel();
    final resultado = await _sync.sincronizar();
    if (_saindo) return;
    final espera = resultado == ResultadoCiclo.downloadIncompleto ? _tentarDeNovo : _sync.intervalo;
    _timerSync = Timer(espera, _sincronizar);
  }

  /// A fila de uma volta: anúncios de hoje com as notícias ainda válidas.
  List<EntradaFila> _compor(List<ItemGrade> anuncios) {
    final s = _sync.situacao.value;
    if (s == null) return [for (final a in anuncios) EntradaFila.anuncio(a)];
    return _composicao.montar(
      anuncios: anuncios,
      noticias: _noticiasValidas(s),
      composicao: s.composicao,
      cartoes: [
        if (s.composicao.cartaoTempo && _climaValido(s) != null) TipoCartao.tempo,
        if (s.composicao.cartaoCotacoes && _cotacoesValidas(s).isNotEmpty) TipoCartao.cotacoes,
      ],
    );
  }

  Clima? _climaValido(SituacaoPlayer s) {
    final c = s.clima;
    return c != null && c.valido(DateTime.now()) ? c : null;
  }

  List<Cotacao> _cotacoesValidas(SituacaoPlayer s) {
    final agora = DateTime.now();
    return s.cotacoes.where((c) => c.valida(agora)).toList();
  }

  List<Noticia> _noticiasValidas(SituacaoPlayer s) {
    final agora = DateTime.now();
    return s.noticias.where((n) => n.valida(agora)).toList();
  }

  List<ItemGrade> _itensDeHoje() {
    final s = _sync.situacao.value;
    if (s == null) return const [];
    final hoje = FiltroDoDia.hoje(s.fusoHorario, DateTime.now());
    return FiltroDoDia.itensDoDia(s.grade, hoje).where(widget.servicos.baixador.pronto).toList();
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
                child: KeyedSubtree(key: ValueKey(_loop.geracao), child: _conteudo(situacao)),
              ),
              if (situacao != null)
                SobreposicaoRelogioClima(
                  fusoHorario: situacao.fusoHorario,
                  exibirRelogio: situacao.composicao.exibirRelogio,
                  clima: situacao.composicao.exibirClima ? situacao.clima : null,
                ),
              if (situacao?.desvinculada ?? false) const _AvisoDesvinculada(),
              if (_mostrarInfo)
                PainelInfo(
                  situacao: situacao,
                  servidor: widget.servicos.urlServidor,
                  itensHoje: _itensDeHoje().length,
                  noticias: situacao == null ? 0 : _noticiasValidas(situacao).length,
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
    final noticia = _loop.noticia;
    if (noticia != null) {
      return NoticiaView(
        noticia: noticia,
        imagem: noticia.temImagem ? _sync.baixadorNoticias.arquivoDe(noticia) : null,
        aoFalharImagem: () => _loop.noticiaFalhou(noticia),
      );
    }
    final cartao = _loop.cartao;
    if (cartao != null && situacao != null) {
      final clima = _climaValido(situacao);
      final cotacoes = _cotacoesValidas(situacao);
      // O dado pode ter vencido entre montar a fila e chegar a vez do cartão.
      if (cartao == TipoCartao.tempo && clima != null) {
        return CartaoTempoView(clima: clima, hoje: FiltroDoDia.hoje(situacao.fusoHorario, DateTime.now()));
      }
      if (cartao == TipoCartao.cotacoes && cotacoes.isNotEmpty) return CartaoCotacoesView(cotacoes: cotacoes);
      return _Espera(nomeTela: situacao.nomeTela);
    }
    final item = _loop.atual;
    if (item == null) return _Espera(nomeTela: situacao?.nomeTela);
    final video = _loop.video;
    if (item.tipo == TipoMidia.video && video != null) return video.visual();
    if (item.tipo == TipoMidia.imagem) {
      return Image.file(
        widget.servicos.baixador.arquivoDe(item),
        fit: BoxFit.contain,
        errorBuilder: (_, __, ___) {
          WidgetsBinding.instance.addPostFrameCallback((_) => _loop.imagemFalhou(item));
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
