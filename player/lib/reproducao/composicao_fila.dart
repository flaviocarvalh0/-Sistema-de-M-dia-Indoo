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
///    fixa ficam onde estão. Com `duracaoMaximaVoltaSegundos`, em vez disso,
///    cada volta é um trecho do [CarrosselAnuncios]: os fixos em toda volta e
///    os outros puxados de uma lista circular a partir de um cursor (ADR-019);
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
  late final CarrosselAnuncios _carrossel = CarrosselAnuncios(sorteio: _sorteio);
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
    final sequencia = composicao.carrossel
        ? _carrossel.proximaVolta(ordenados,
            limiteSegundos: composicao.duracaoMaximaVoltaSegundos, embaralhar: composicao.embaralharAnuncios)
        : composicao.embaralharAnuncios
            ? embaralhar(ordenados, _sorteio)
            : ordenados;
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

/// Carrossel com cursor (ADR-019): quando a grade não cabe numa volta, cada
/// volta mostra o próximo trecho de uma lista circular (a "piscina") e todo
/// anúncio passa uma vez antes de qualquer um repetir — a mesma frequência
/// para todos, calculada na TV, sem internet.
///
/// - Os itens com posição fixa entram em **toda** volta, na posição deles.
/// - Os outros formam a piscina (embaralhada, ou na ordem da grade). A volta
///   puxa a partir do cursor enquanto couber no limite (pelo menos um por
///   volta) e o cursor avança.
/// - No fim da piscina: embaralha de novo e volta ao começo, sem repetir na
///   mesma volta um anúncio que já entrou nela e sem emendar o mesmo anúncio
///   duas vezes seguidas.
/// - Grade nova: quem saiu sai da piscina (o cursor acompanha); quem entrou
///   entra no trecho que ainda não passou neste ciclo.
///
/// O estado (piscina e cursor) vive em memória. Se o app reinicia, começa um
/// ciclo novo, sorteado — ninguém é favorecido.
class CarrosselAnuncios {
  CarrosselAnuncios({Random? sorteio}) : _sorteio = sorteio ?? Random();

  final Random _sorteio;
  List<String> _piscina = [];
  int _cursor = 0;
  String? _ultimo;
  bool _embaralhada = true;

  /// A piscina como está agora (chaves `ordem:hash`), para testes e diagnóstico.
  List<String> get piscina => List.unmodifiable(_piscina);
  int get cursor => _cursor;

  static String chave(ItemGrade item) => '${item.ordem}:${item.hash}';

  /// Os anúncios da próxima volta, já com os fixos nas posições deles.
  List<ItemGrade> proximaVolta(List<ItemGrade> ordenados, {required int limiteSegundos, required bool embaralhar}) {
    final fixos = [for (var i = 0; i < ordenados.length; i++) if (ordenados[i].posicaoFixa) (i, ordenados[i])];
    final soltos = {for (final a in ordenados) if (!a.posicaoFixa) chave(a): a};

    _reconciliar(soltos, ordenados, embaralhar);

    final orcamento = limiteSegundos - fixos.fold<int>(0, (soma, f) => soma + f.$2.duracaoSegundos);
    final volta = <ItemGrade>[];
    final naVolta = <String>{};
    var soma = 0;

    while (_piscina.isNotEmpty && naVolta.length < _piscina.length) {
      if (_cursor >= _piscina.length) _recomecar(soltos, ordenados, embaralhar, naVolta);
      final k = _piscina[_cursor];
      final item = soltos[k]!;
      // Pelo menos um por volta, mesmo que os fixos já ocupem o limite.
      if (volta.isNotEmpty && soma + item.duracaoSegundos > orcamento) break;
      volta.add(item);
      naVolta.add(k);
      soma += item.duracaoSegundos;
      _cursor++;
    }
    if (volta.isNotEmpty) _ultimo = chave(volta.last);

    // Cada fixo volta à posição que tem na grade (ou ao fim, se a volta for menor).
    for (final (posicao, item) in fixos) {
      volta.insert(min(posicao, volta.length), item);
    }
    return volta;
  }

  /// Ajusta a piscina à grade de agora, sem perder o lugar do ciclo.
  void _reconciliar(Map<String, ItemGrade> soltos, List<ItemGrade> ordenados, bool embaralhar) {
    if (_piscina.isEmpty || embaralhar != _embaralhada) {
      _piscina = _nova(soltos, ordenados, embaralhar);
      _cursor = 0;
      _embaralhada = embaralhar;
      return;
    }

    // Quem saiu da grade (ou não vale hoje) sai da piscina; o cursor acompanha.
    final antes = _piscina;
    _piscina = [];
    var cursor = _cursor;
    for (var i = 0; i < antes.length; i++) {
      if (soltos.containsKey(antes[i])) {
        _piscina.add(antes[i]);
      } else if (i < _cursor) {
        cursor--;
      }
    }
    _cursor = cursor;

    // Quem entrou vai para o trecho que ainda não passou neste ciclo.
    final presentes = _piscina.toSet();
    for (final a in ordenados) {
      final k = chave(a);
      if (a.posicaoFixa || presentes.contains(k)) continue;
      final pos = embaralhar
          ? _cursor + _sorteio.nextInt(_piscina.length - _cursor + 1)
          : max(_cursor, _piscina.where((x) => soltos[x]!.ordem < a.ordem).length);
      _piscina.insert(pos, k);
      presentes.add(k);
    }
  }

  /// Fim da piscina: outro ciclo. Quem já entrou nesta volta vai para o fim
  /// (não repete na mesma volta) e o primeiro não é o último que passou.
  void _recomecar(Map<String, ItemGrade> soltos, List<ItemGrade> ordenados, bool embaralhar, Set<String> naVolta) {
    final nova = _nova(soltos, ordenados, embaralhar);
    _piscina = [...nova.where((k) => !naVolta.contains(k)), ...nova.where(naVolta.contains)];
    _cursor = 0;
    if (_piscina.length > 1 && _piscina.first == _ultimo && naVolta.isEmpty) {
      final troca = 1 + _sorteio.nextInt(_piscina.length - 1);
      _piscina[0] = _piscina[troca];
      _piscina[troca] = _ultimo!;
    }
  }

  List<String> _nova(Map<String, ItemGrade> soltos, List<ItemGrade> ordenados, bool embaralhar) {
    final chaves = [for (final a in ordenados) if (!a.posicaoFixa) chave(a)];
    return embaralhar ? (chaves..shuffle(_sorteio)) : chaves;
  }
}
