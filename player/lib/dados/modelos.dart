/// Contratos da API das TVs (docs/CONTRATO_DISPOSITIVO.md).
library;

/// Resposta de `POST /autenticar`.
class Pareamento {
  const Pareamento({
    required this.token,
    required this.expiraEm,
    required this.telaId,
    required this.nomeTela,
    required this.nomeParceiro,
    required this.intervaloSincronizacaoSegundos,
  });

  factory Pareamento.fromJson(Map<String, dynamic> json) => Pareamento(
        token: json['token'] as String,
        expiraEm: DateTime.parse(json['expiraEm'] as String),
        telaId: (json['telaId'] as num).toInt(),
        nomeTela: json['nomeTela'] as String,
        nomeParceiro: json['nomeParceiro'] as String,
        intervaloSincronizacaoSegundos: (json['intervaloSincronizacaoSegundos'] as num).toInt(),
      );

  final String token;
  final DateTime expiraEm;
  final int telaId;
  final String nomeTela;
  final String nomeParceiro;
  final int intervaloSincronizacaoSegundos;
}

enum TipoMidia { video, imagem }

/// Arquivo que a TV baixa e confere pelo SHA-256 (mídia ou imagem de notícia).
abstract interface class ArquivoRemoto {
  /// Endereço temporário só para baixar (não é gravado).
  String? get url;

  /// SHA-256 em hexadecimal minúsculo.
  String get hash;

  /// Nome local: o próprio hash com a extensão do tipo.
  String get nomeArquivo;
}

String _extensao(String tipoConteudo) => switch (tipoConteudo) {
      'video/mp4' => '.mp4',
      'image/png' => '.png',
      // Só fotos de notícias (o Android exibe WebP nativamente).
      'image/webp' => '.webp',
      _ => '.jpg',
    };

/// Um item da grade, como vem da API e como fica gravado no aparelho.
class ItemGrade implements ArquivoRemoto {
  const ItemGrade({
    required this.ordem,
    required this.midiaId,
    required this.tipo,
    required this.tipoConteudo,
    required this.hash,
    required this.tamanhoBytes,
    required this.duracaoSegundos,
    required this.diasSemana,
    this.posicaoFixa = false,
    this.url,
    this.inicioExibicao,
    this.fimExibicao,
  });

  factory ItemGrade.fromJson(Map<String, dynamic> json) => ItemGrade(
        ordem: (json['ordem'] as num).toInt(),
        midiaId: (json['midiaId'] as num).toInt(),
        tipo: _tipo(json['tipo']),
        tipoConteudo: json['tipoConteudo'] as String,
        url: json['url'] as String?,
        hash: (json['hash'] as String).toLowerCase(),
        tamanhoBytes: (json['tamanhoBytes'] as num).toInt(),
        duracaoSegundos: (json['duracaoSegundos'] as num).toInt(),
        diasSemana: (json['diasSemana'] as num).toInt(),
        posicaoFixa: json['posicaoFixa'] as bool? ?? false,
        inicioExibicao: _data(json['inicioExibicao']),
        fimExibicao: _data(json['fimExibicao']),
      );

  final int ordem;
  final int midiaId;
  final TipoMidia tipo;
  final String tipoConteudo;

  /// Endereço temporário (24 h) só para baixar. Não é gravado.
  @override
  final String? url;

  /// SHA-256 do arquivo, em hexadecimal minúsculo.
  @override
  final String hash;
  final int tamanhoBytes;
  final int duracaoSegundos;

  /// Bits dos dias: domingo = 1, segunda = 2, … sábado = 64.
  final int diasSemana;

  /// Fica nesta posição quando a composição embaralha os anúncios.
  final bool posicaoFixa;

  /// Datas (sem hora) do período de exibição, em UTC à meia-noite.
  final DateTime? inicioExibicao;
  final DateTime? fimExibicao;

  /// Nome do arquivo local: o próprio hash, então a mesma mídia em duas
  /// grades é um arquivo só e não é baixada de novo.
  @override
  String get nomeArquivo => '$hash${_extensao(tipoConteudo)}';

  static TipoMidia _tipo(Object? valor) => switch (valor) {
        'Video' => TipoMidia.video,
        _ => TipoMidia.imagem,
      };

  static DateTime? _data(Object? valor) {
    if (valor is! String || valor.isEmpty) return null;
    final p = valor.split('-').map(int.parse).toList();
    return DateTime.utc(p[0], p[1], p[2]);
  }
}

/// Como a TV monta a fila a cada volta do loop (vem da playlist).
class Composicao {
  const Composicao({
    this.noticiaACadaAnuncios = 0,
    this.duracaoNoticiaSegundos = 10,
    this.embaralharAnuncios = false,
    this.exibirRelogio = true,
    this.exibirClima = true,
  });

  factory Composicao.fromJson(Map<String, dynamic> json) => Composicao(
        noticiaACadaAnuncios: (json['noticiaACadaAnuncios'] as num?)?.toInt() ?? 0,
        duracaoNoticiaSegundos: (json['duracaoNoticiaSegundos'] as num?)?.toInt() ?? 10,
        embaralharAnuncios: json['embaralharAnuncios'] as bool? ?? false,
        exibirRelogio: json['exibirRelogio'] as bool? ?? true,
        exibirClima: json['exibirClima'] as bool? ?? true,
      );

  /// Uma notícia depois de cada N anúncios; 0 = sem notícias.
  final int noticiaACadaAnuncios;
  final int duracaoNoticiaSegundos;
  final bool embaralharAnuncios;
  final bool exibirRelogio;
  final bool exibirClima;

  Map<String, dynamic> toJson() => {
        'noticiaACadaAnuncios': noticiaACadaAnuncios,
        'duracaoNoticiaSegundos': duracaoNoticiaSegundos,
        'embaralharAnuncios': embaralharAnuncios,
        'exibirRelogio': exibirRelogio,
        'exibirClima': exibirClima,
      };
}

/// Notícia para intercalar entre os anúncios (com imagem opcional).
class Noticia implements ArquivoRemoto {
  const Noticia({
    required this.id,
    required this.titulo,
    required this.credito,
    required this.publicadaEm,
    this.url,
    this.hashImagem,
    this.tamanhoBytes,
    this.tipoConteudo,
  });

  factory Noticia.fromJson(Map<String, dynamic> json) => Noticia(
        id: (json['id'] as num).toInt(),
        titulo: json['titulo'] as String,
        credito: json['credito'] as String,
        publicadaEm: DateTime.parse(json['publicadaEm'] as String).toUtc(),
        url: json['url'] as String?,
        hashImagem: (json['hash'] as String?)?.toLowerCase(),
        tamanhoBytes: (json['tamanhoBytes'] as num?)?.toInt(),
        tipoConteudo: json['tipoConteudo'] as String?,
      );

  /// Notícia mais velha que isto não vai para a tela, mesmo offline.
  static const validade = Duration(hours: 48);

  final int id;
  final String titulo;

  /// Exigido pela licença da fonte (ex.: "Fonte: Agência Brasil").
  final String credito;
  final DateTime publicadaEm;

  @override
  final String? url;
  final String? hashImagem;
  final int? tamanhoBytes;
  final String? tipoConteudo;

  bool get temImagem => hashImagem != null;

  bool valida(DateTime agora) => agora.toUtc().difference(publicadaEm) < validade;

  @override
  String get hash => hashImagem ?? '';

  @override
  String get nomeArquivo => '$hash${_extensao(tipoConteudo ?? 'image/jpeg')}';
}

/// Tempo agora na cidade do estabelecimento (Open-Meteo).
class Clima {
  const Clima({
    required this.temperaturaC,
    required this.codigo,
    required this.descricao,
    required this.icone,
    required this.atualizadaEm,
    required this.credito,
    this.minimaC,
    this.maximaC,
    this.local,
  });

  factory Clima.fromJson(Map<String, dynamic> json) => Clima(
        temperaturaC: (json['temperaturaC'] as num).toDouble(),
        minimaC: (json['minimaC'] as num?)?.toDouble(),
        maximaC: (json['maximaC'] as num?)?.toDouble(),
        codigo: (json['codigo'] as num).toInt(),
        descricao: json['descricao'] as String,
        icone: json['icone'] as String,
        local: json['local'] as String?,
        atualizadaEm: DateTime.parse(json['atualizadaEm'] as String).toUtc(),
        credito: json['credito'] as String? ?? 'Clima: Open-Meteo',
      );

  /// Leitura mais velha que isto sai da tela (a TV ficou muito tempo offline).
  static const validade = Duration(hours: 6);

  final double temperaturaC;
  final double? minimaC;
  final double? maximaC;
  final int codigo;
  final String descricao;

  /// `sol`, `sol-nuvem`, `nuvem`, `neblina`, `garoa`, `chuva`, `neve` ou `tempestade`.
  final String icone;
  final String? local;
  final DateTime atualizadaEm;
  final String credito;

  bool valido(DateTime agora) => agora.toUtc().difference(atualizadaEm) < validade;

  Map<String, dynamic> toJson() => {
        'temperaturaC': temperaturaC,
        'minimaC': minimaC,
        'maximaC': maximaC,
        'codigo': codigo,
        'descricao': descricao,
        'icone': icone,
        'local': local,
        'atualizadaEm': atualizadaEm.toIso8601String(),
        'credito': credito,
      };
}

/// Resposta de `GET /sincronizacao`.
class RespostaSincronizacao {
  const RespostaSincronizacao({
    required this.alterada,
    required this.versaoGrade,
    required this.fusoHorario,
    required this.intervaloSincronizacaoSegundos,
    required this.nomeTela,
    this.novoToken,
    this.itens,
    this.composicao = const Composicao(),
    this.versaoNoticias,
    this.noticias,
    this.clima,
  });

  factory RespostaSincronizacao.fromJson(Map<String, dynamic> json) => RespostaSincronizacao(
        alterada: json['alterada'] as bool,
        versaoGrade: json['versaoGrade'] as String,
        fusoHorario: json['fusoHorario'] as String,
        intervaloSincronizacaoSegundos: (json['intervaloSincronizacaoSegundos'] as num).toInt(),
        nomeTela: json['nomeTela'] as String,
        novoToken: json['novoToken'] as String?,
        itens: (json['itens'] as List<dynamic>?)
            ?.map((e) => ItemGrade.fromJson(e as Map<String, dynamic>))
            .toList(),
        composicao: json['composicao'] is Map<String, dynamic>
            ? Composicao.fromJson(json['composicao'] as Map<String, dynamic>)
            : const Composicao(),
        versaoNoticias: json['versaoNoticias'] as String?,
        noticias: (json['noticias'] as List<dynamic>?)
            ?.map((e) => Noticia.fromJson(e as Map<String, dynamic>))
            .toList(),
        clima: json['clima'] is Map<String, dynamic> ? Clima.fromJson(json['clima'] as Map<String, dynamic>) : null,
      );

  final bool alterada;
  final String versaoGrade;
  final String fusoHorario;
  final int intervaloSincronizacaoSegundos;
  final String nomeTela;
  final String? novoToken;

  /// Só vem quando `alterada` é verdadeiro.
  final List<ItemGrade>? itens;

  /// Vem em toda resposta (API antiga: o padrão, sem notícias).
  final Composicao composicao;

  /// Nulo em API antiga (sem notícias).
  final String? versaoNoticias;

  /// Só vem quando as notícias mudaram (vazia = sem notícias).
  final List<Noticia>? noticias;

  /// Nulo: a playlist não exibe clima ou não há leitura recente.
  final Clima? clima;
}
