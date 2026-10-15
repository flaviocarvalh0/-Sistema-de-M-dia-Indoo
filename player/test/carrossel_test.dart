import 'dart:math';

import 'package:flutter_test/flutter_test.dart';
import 'package:midia_indoor_player/dados/modelos.dart';
import 'package:midia_indoor_player/reproducao/composicao_fila.dart';

ItemGrade anuncio(int ordem, {int duracao = 18, bool fixa = false}) => ItemGrade(
      ordem: ordem,
      midiaId: ordem,
      tipo: TipoMidia.imagem,
      tipoConteudo: 'image/png',
      hash: 'h$ordem',
      tamanhoBytes: 1,
      duracaoSegundos: duracao,
      diasSemana: 127,
      posicaoFixa: fixa,
    );

List<int> ordens(List<ItemGrade> volta) => [for (final a in volta) a.ordem];

void main() {
  final trinta = [for (var i = 0; i < 30; i++) anuncio(i)];

  test('30 anúncios de 18 s numa volta de 180 s: 10 por volta, todos uma vez em 3 voltas', () {
    final c = CarrosselAnuncios(sorteio: Random(1));
    final vistos = <int>[];
    for (var v = 0; v < 3; v++) {
      final volta = c.proximaVolta(trinta, limiteSegundos: 180, embaralhar: true);
      expect(volta, hasLength(10));
      vistos.addAll(ordens(volta));
    }
    expect(vistos.toSet(), hasLength(30), reason: 'ninguém repete antes de todos passarem');

    // O 4º trecho já é de um ciclo novo, sorteado de novo.
    expect(c.proximaVolta(trinta, limiteSegundos: 180, embaralhar: true), hasLength(10));
  });

  test('frequência igual no longo prazo, mesmo quando o ciclo não fecha numa volta', () {
    final c = CarrosselAnuncios(sorteio: Random(3));
    final grade = [for (var i = 0; i < 25; i++) anuncio(i, duracao: 15)];
    final contagem = <int, int>{};
    int? ultimo;
    for (var v = 0; v < 200; v++) {
      final volta = c.proximaVolta(grade, limiteSegundos: 150, embaralhar: true);
      expect(volta, hasLength(10));
      expect(ordens(volta).toSet(), hasLength(10), reason: 'sem repetir dentro da volta, nem na virada do ciclo');
      expect(volta.first.ordem, isNot(ultimo), reason: 'o mesmo anúncio não passa duas vezes seguidas');
      ultimo = volta.last.ordem;
      for (final a in volta) {
        contagem[a.ordem] = (contagem[a.ordem] ?? 0) + 1;
      }
    }
    // 2.000 exibições / 25 anúncios = 80 cada.
    expect(contagem.values.reduce(max) - contagem.values.reduce(min), lessThanOrEqualTo(1));
  });

  test('posição fixa entra em toda volta, no lugar dela, e ocupa o tempo da volta', () {
    final c = CarrosselAnuncios(sorteio: Random(5));
    final grade = [anuncio(0, fixa: true, duracao: 30), for (var i = 1; i <= 20; i++) anuncio(i)];
    for (var v = 0; v < 5; v++) {
      final volta = c.proximaVolta(grade, limiteSegundos: 120, embaralhar: true);
      expect(volta.first.ordem, 0);
      // 120 s − 30 s do fixo = 90 s = 5 anúncios de 18 s.
      expect(volta, hasLength(6));
    }
  });

  test('anúncio maior que a volta ainda passa (pelo menos um por volta)', () {
    final c = CarrosselAnuncios(sorteio: Random(1));
    final grade = [anuncio(0, duracao: 300), anuncio(1, duracao: 300)];
    final a = c.proximaVolta(grade, limiteSegundos: 60, embaralhar: false);
    final b = c.proximaVolta(grade, limiteSegundos: 60, embaralhar: false);
    expect([...ordens(a), ...ordens(b)], [0, 1]);
  });

  test('sem embaralhar: segue a ordem da grade, de onde parou', () {
    final c = CarrosselAnuncios();
    final grade = [for (var i = 0; i < 7; i++) anuncio(i, duracao: 10)];
    expect(ordens(c.proximaVolta(grade, limiteSegundos: 30, embaralhar: false)), [0, 1, 2]);
    expect(ordens(c.proximaVolta(grade, limiteSegundos: 30, embaralhar: false)), [3, 4, 5]);
    expect(ordens(c.proximaVolta(grade, limiteSegundos: 30, embaralhar: false)), [6, 0, 1]);
  });

  test('grade nova: quem saiu some, quem entrou passa ainda neste ciclo, sem perder o lugar', () {
    final c = CarrosselAnuncios(sorteio: Random(9));
    final grade = [for (var i = 0; i < 9; i++) anuncio(i, duracao: 10)];
    final primeira = ordens(c.proximaVolta(grade, limiteSegundos: 30, embaralhar: true));

    final faltam = c.piscina.skip(c.cursor).map((k) => int.parse(k.split(':').first)).toList();
    final sai = faltam.first;
    final nova = [...grade.where((a) => a.ordem != sai), anuncio(50, duracao: 10)];

    final resto = [
      ...ordens(c.proximaVolta(nova, limiteSegundos: 30, embaralhar: true)),
      ...ordens(c.proximaVolta(nova, limiteSegundos: 30, embaralhar: true)),
    ];
    expect(resto, isNot(contains(sai)));
    expect({...primeira, ...resto}, {for (var i = 0; i < 9; i++) if (i != sai) i, 50},
        reason: 'o ciclo fecha com todos os de agora, cada um uma vez');
  });

  test('ComposicaoFila: com limite, a volta é o trecho do carrossel com os informativos intercalados', () {
    final c = ComposicaoFila(sorteio: Random(2));
    const comp = Composicao(duracaoMaximaVoltaSegundos: 54, noticiaACadaAnuncios: 3);
    final noticias = [Noticia(id: 1, titulo: 'N1', credito: 'F', publicadaEm: DateTime.utc(2026, 10, 15))];
    final volta = c.montar(anuncios: trinta, noticias: noticias, composicao: comp);
    expect(volta.where((e) => e.anuncio != null), hasLength(3), reason: '54 s = 3 anúncios de 18 s');
    expect(volta.last.noticia?.id, 1, reason: 'a notícia vem depois dos 3 e não conta no limite');
  });
}
