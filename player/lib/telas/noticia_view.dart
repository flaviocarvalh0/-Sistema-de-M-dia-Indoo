import 'dart:io';

import 'package:flutter/material.dart';

import '../dados/modelos.dart';

/// Notícia em tela cheia: a foto ao fundo (ou um fundo da marca, sem foto),
/// a faixa "NOTÍCIAS", o título entrando com animação e o crédito da fonte,
/// que a licença exige na tela.
class NoticiaView extends StatelessWidget {
  const NoticiaView({super.key, required this.noticia, this.imagem, this.aoFalharImagem});

  final Noticia noticia;

  /// Arquivo da imagem já baixado; nulo para notícia sem foto.
  final File? imagem;

  final VoidCallback? aoFalharImagem;

  static const _corMarca = Color(0xFF0F766E);

  @override
  Widget build(BuildContext context) {
    final largura = MediaQuery.sizeOf(context).width;
    // Títulos legíveis a 3 m numa TV de 1080p e proporcionais em telas menores.
    final tamanhoTitulo = (largura / 30).clamp(22.0, 64.0);
    final comFoto = imagem != null;

    return Stack(
      fit: StackFit.expand,
      children: [
        if (comFoto)
          Image.file(
            imagem!,
            fit: BoxFit.cover,
            errorBuilder: (_, __, ___) {
              WidgetsBinding.instance.addPostFrameCallback((_) => aoFalharImagem?.call());
              return const ColoredBox(color: Color(0xFF0F1F2E));
            },
          )
        else
          const DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [Color(0xFF0F1F2E), Color(0xFF134E4A)],
              ),
            ),
          ),
        // Escurece a parte de baixo para o texto ficar legível sobre qualquer foto.
        if (comFoto)
          const DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                stops: [0.35, 1],
                colors: [Colors.transparent, Color(0xE6000000)],
              ),
            ),
          ),
        Align(
          alignment: comFoto ? Alignment.bottomLeft : Alignment.centerLeft,
          child: Padding(
            padding: EdgeInsets.fromLTRB(largura * 0.05, 32, largura * 0.05, largura * 0.04),
            child: TweenAnimationBuilder<double>(
              key: ValueKey(noticia.id),
              tween: Tween(begin: 0, end: 1),
              duration: const Duration(milliseconds: 900),
              curve: Curves.easeOutCubic,
              builder: (context, t, filho) => Opacity(
                opacity: t,
                child: Transform.translate(offset: Offset(0, (1 - t) * 40), child: filho),
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                    decoration: BoxDecoration(color: _corMarca, borderRadius: BorderRadius.circular(6)),
                    child: Text(
                      'NOTÍCIAS',
                      style: TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.w700,
                        letterSpacing: 2,
                        fontSize: tamanhoTitulo * 0.4,
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),
                  Text(
                    noticia.titulo,
                    maxLines: 4,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: tamanhoTitulo,
                      height: 1.2,
                      fontWeight: FontWeight.w600,
                      shadows: const [Shadow(blurRadius: 8, color: Colors.black54)],
                    ),
                  ),
                  const SizedBox(height: 14),
                  Text(
                    noticia.credito,
                    style: TextStyle(color: Colors.white70, fontSize: tamanhoTitulo * 0.38),
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }
}
