import 'dart:math';

import '../dados/modelos.dart';

/// Um passo do loop: um anúncio da grade ou uma notícia.
class EntradaFila {
  const EntradaFila.anuncio(ItemGrade this.anuncio)
      : noticia = null,
        _duracaoNoticia = 0;

  const EntradaFila.noticia(Noticia this.noticia, int duracaoSegundos)
      : anuncio = null,
        _duracaoNoticia = duracaoSegundos;

  final ItemGrade? anuncio;
  final Noticia? noticia;
  final int _duracaoNoticia;

  int get duracaoSegundos => anuncio?.duracaoSegundos ?? _duracaoNoticia;

  @override
  String toString() => anuncio != null ? 'A${anuncio!.ordem}' : 'N${noticia!.id}';
}

/// Monta a fila de uma volta do loop, na TV, a cada volta (ADR-016):
///
/// 1. os anúncios de hoje na ordem da grade;
/// 2. com "embaralhar", sorteia a ordem a cada volta — os itens com posição
///    fixa ficam onde estão;
/// 3. uma notícia depois de cada N anúncios, em rodízio (mais recentes
///    primeiro). A contagem continua de uma volta para a outra: com 2
///    anúncios e "a cada 3", a notícia aparece no meio da volta seguinte.
///
/// Sem anúncios para hoje e com notícias, a volta é só de notícias (melhor
/// que a tela de espera). Como roda na TV, cada volta sai diferente mesmo
/// offline, e o servidor só manda grade nova quando o conteúdo muda.
class ComposicaoFila {
  ComposicaoFila({Random? sorteio}) : _sorteio = sorteio ?? Random();

  final Random _sorteio;
  int _proximaNoticia = 0;
  int _anunciosDesdeNoticia = 0;

  List<EntradaFila> montar({
    required List<ItemGrade> anuncios,
    required List<Noticia> noticias,
    required Composicao composicao,
  }) {
    final ordenados = [...anuncios]..sort((a, b) => a.ordem.compareTo(b.ordem));
    final sequencia = composicao.embaralharAnuncios ? embaralhar(ordenados, _sorteio) : ordenados;
    final aCada = composicao.noticiaACadaAnuncios;
    final comNoticias = aCada > 0 && noticias.isNotEmpty;

    if (sequencia.isEmpty) {
      return comNoticias
          ? [for (var i = 0; i < noticias.length; i++) _proxima(noticias, composicao.duracaoNoticiaSegundos)]
          : const [];
    }

    final fila = <EntradaFila>[];
    for (final anuncio in sequencia) {
      fila.add(EntradaFila.anuncio(anuncio));
      if (!comNoticias) continue;
      _anunciosDesdeNoticia++;
      if (_anunciosDesdeNoticia >= aCada) {
        fila.add(_proxima(noticias, composicao.duracaoNoticiaSegundos));
        _anunciosDesdeNoticia = 0;
      }
    }
    return fila;
  }

  EntradaFila _proxima(List<Noticia> noticias, int duracao) {
    final noticia = noticias[_proximaNoticia % noticias.length];
    _proximaNoticia = (_proximaNoticia + 1) % noticias.length;
    return EntradaFila.noticia(noticia, duracao);
  }

  /// Sorteia os itens soltos nas posições livres; os fixos ficam no lugar.
  static List<ItemGrade> embaralhar(List<ItemGrade> ordenados, Random sorteio) {
    final soltos = ordenados.where((i) => !i.posicaoFixa).toList()..shuffle(sorteio);
    var k = 0;
    return [for (final item in ordenados) item.posicaoFixa ? item : soltos[k++]];
  }
}
