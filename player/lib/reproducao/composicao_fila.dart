import 'dart:math';

import '../dados/modelos.dart';

/// Cartões informativos que entram no rodízio junto com as notícias.
enum TipoCartao { tempo, cotacoes }

/// Um passo do loop: um anúncio da grade, uma notícia ou um cartão.
class EntradaFila {
  const EntradaFila.anuncio(ItemGrade this.anuncio)
      : noticia = null,
        cartao = null,
        _duracaoInformativo = 0;

  const EntradaFila.noticia(Noticia this.noticia, int duracaoSegundos)
      : anuncio = null,
        cartao = null,
        _duracaoInformativo = duracaoSegundos;

  const EntradaFila.cartao(TipoCartao this.cartao, int duracaoSegundos)
      : anuncio = null,
        noticia = null,
        _duracaoInformativo = duracaoSegundos;

  final ItemGrade? anuncio;
  final Noticia? noticia;
  final TipoCartao? cartao;
  final int _duracaoInformativo;

  int get duracaoSegundos => anuncio?.duracaoSegundos ?? _duracaoInformativo;

  @override
  String toString() => anuncio != null
      ? 'A${anuncio!.ordem}'
      : noticia != null
          ? 'N${noticia!.id}'
          : cartao == TipoCartao.tempo
              ? 'T'
              : 'C';
}

/// Monta a fila de uma volta do loop, na TV, a cada volta (ADR-016):
///
/// 1. os anúncios de hoje na ordem da grade;
/// 2. com "embaralhar", sorteia a ordem a cada volta — os itens com posição
///    fixa ficam onde estão;
/// 3. um informativo depois de cada N anúncios, em rodízio: notícia, tempo,
///    notícia, cotações (os cartões que a playlist usa e que têm dados; sem
///    notícias, só os cartões). As notícias vão em rodízio próprio, mais
///    recentes primeiro. A contagem continua de uma volta para a outra: com
///    2 anúncios e "a cada 3", o informativo aparece no meio da volta
///    seguinte.
///
/// Sem anúncios para hoje e com informativos, a volta é só de informativos
/// (melhor que a tela de espera). Como roda na TV, cada volta sai diferente mesmo
/// offline, e o servidor só manda grade nova quando o conteúdo muda.
class ComposicaoFila {
  ComposicaoFila({Random? sorteio}) : _sorteio = sorteio ?? Random();

  final Random _sorteio;
  int _proximaNoticia = 0;
  int _proximoInformativo = 0;
  int _anunciosDesdeInformativo = 0;

  /// [cartoes]: os cartões ligados na playlist que têm dados agora.
  List<EntradaFila> montar({
    required List<ItemGrade> anuncios,
    required List<Noticia> noticias,
    required Composicao composicao,
    List<TipoCartao> cartoes = const [],
  }) {
    final ordenados = [...anuncios]..sort((a, b) => a.ordem.compareTo(b.ordem));
    final sequencia = composicao.embaralharAnuncios ? embaralhar(ordenados, _sorteio) : ordenados;
    final aCada = composicao.noticiaACadaAnuncios;
    final rodizio = aCada > 0 ? _rodizio(noticias.isNotEmpty, cartoes) : const <TipoCartao?>[];
    final duracao = composicao.duracaoNoticiaSegundos;

    if (sequencia.isEmpty) {
      final quantos = noticias.length + cartoes.length;
      return rodizio.isEmpty ? const [] : [for (var i = 0; i < quantos; i++) _proximo(rodizio, noticias, duracao)];
    }

    final fila = <EntradaFila>[];
    for (final anuncio in sequencia) {
      fila.add(EntradaFila.anuncio(anuncio));
      if (rodizio.isEmpty) continue;
      _anunciosDesdeInformativo++;
      if (_anunciosDesdeInformativo >= aCada) {
        fila.add(_proximo(rodizio, noticias, duracao));
        _anunciosDesdeInformativo = 0;
      }
    }
    return fila;
  }

  /// A ordem dos informativos: notícia, tempo, notícia, cotações (nulo = notícia).
  static List<TipoCartao?> _rodizio(bool comNoticias, List<TipoCartao> cartoes) {
    if (!comNoticias) return [...cartoes];
    final rodizio = <TipoCartao?>[
      for (final c in TipoCartao.values)
        if (cartoes.contains(c)) ...[null, c],
    ];
    return rodizio.isEmpty ? const [null] : rodizio;
  }

  EntradaFila _proximo(List<TipoCartao?> rodizio, List<Noticia> noticias, int duracao) {
    final tipo = rodizio[_proximoInformativo % rodizio.length];
    _proximoInformativo = (_proximoInformativo + 1) % rodizio.length;
    if (tipo != null) return EntradaFila.cartao(tipo, duracao);

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
