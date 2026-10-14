import 'dart:math';

import 'package:flutter_test/flutter_test.dart';
import 'package:midia_indoor_player/dados/modelos.dart';
import 'package:midia_indoor_player/reproducao/composicao_fila.dart';

ItemGrade anuncio(int ordem, {bool fixa = false}) => ItemGrade(
      ordem: ordem,
      midiaId: ordem,
      tipo: TipoMidia.imagem,
      tipoConteudo: 'image/png',
      hash: 'h$ordem',
      tamanhoBytes: 1,
      duracaoSegundos: 10,
      diasSemana: 127,
      posicaoFixa: fixa,
    );

Noticia noticia(int id) => Noticia(id: id, titulo: 'N$id', credito: 'Fonte', publicadaEm: DateTime.utc(2026, 10, 14));

String fila(List<EntradaFila> f) => f.join(' ');

void main() {
  final anuncios = [anuncio(2), anuncio(0), anuncio(1), anuncio(3)];

  test('sem composição: os anúncios na ordem da grade', () {
    final f = ComposicaoFila().montar(anuncios: anuncios, noticias: [noticia(1)], composicao: const Composicao());
    expect(fila(f), 'A0 A1 A2 A3');
  });

  test('uma notícia a cada 2 anúncios, em rodízio', () {
    final c = ComposicaoFila();
    const comp = Composicao(noticiaACadaAnuncios: 2, duracaoNoticiaSegundos: 12);
    final primeira = c.montar(anuncios: anuncios, noticias: [noticia(1), noticia(2), noticia(3)], composicao: comp);
    expect(fila(primeira), 'A0 A1 N1 A2 A3 N2');
    expect(primeira[2].duracaoSegundos, 12);

    final segunda = c.montar(anuncios: anuncios, noticias: [noticia(1), noticia(2), noticia(3)], composicao: comp);
    expect(fila(segunda), 'A0 A1 N3 A2 A3 N1', reason: 'continua de onde parou');
  });

  test('a contagem passa de uma volta para a outra', () {
    final c = ComposicaoFila();
    const comp = Composicao(noticiaACadaAnuncios: 3);
    final duas = [anuncio(0), anuncio(1)];
    expect(fila(c.montar(anuncios: duas, noticias: [noticia(1)], composicao: comp)), 'A0 A1');
    expect(fila(c.montar(anuncios: duas, noticias: [noticia(1)], composicao: comp)), 'A0 N1 A1');
  });

  test('embaralhar: fixos no lugar, os outros mudam de ordem entre voltas', () {
    final c = ComposicaoFila(sorteio: Random(7));
    final grade = [anuncio(0, fixa: true), anuncio(1), anuncio(2), anuncio(3), anuncio(4), anuncio(5, fixa: true)];
    final voltas = <String>{};
    for (var i = 0; i < 10; i++) {
      final f = c.montar(anuncios: grade, noticias: const [], composicao: const Composicao(embaralharAnuncios: true));
      expect(f.first.anuncio!.ordem, 0);
      expect(f.last.anuncio!.ordem, 5);
      expect(f.map((e) => e.anuncio!.ordem).toSet(), {0, 1, 2, 3, 4, 5}, reason: 'ninguém some nem repete');
      voltas.add(fila(f));
    }
    expect(voltas.length, greaterThan(1), reason: 'a ordem muda entre voltas');
  });

  test('sem anúncios hoje: a volta é só de notícias', () {
    final f = ComposicaoFila().montar(
        anuncios: const [], noticias: [noticia(1), noticia(2)], composicao: const Composicao(noticiaACadaAnuncios: 3));
    expect(fila(f), 'N1 N2');
  });

  test('sem notícias: só anúncios, mesmo com a composição ligada', () {
    final f = ComposicaoFila().montar(anuncios: anuncios, noticias: const [], composicao: const Composicao(noticiaACadaAnuncios: 1));
    expect(fila(f), 'A0 A1 A2 A3');
    expect(ComposicaoFila().montar(anuncios: const [], noticias: const [], composicao: const Composicao()), isEmpty);
  });
}
