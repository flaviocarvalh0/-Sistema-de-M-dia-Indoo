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

/// Um item da grade, como vem da API e como fica gravado no aparelho.
class ItemGrade {
  const ItemGrade({
    required this.ordem,
    required this.midiaId,
    required this.tipo,
    required this.tipoConteudo,
    required this.hash,
    required this.tamanhoBytes,
    required this.duracaoSegundos,
    required this.diasSemana,
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
        inicioExibicao: _data(json['inicioExibicao']),
        fimExibicao: _data(json['fimExibicao']),
      );

  final int ordem;
  final int midiaId;
  final TipoMidia tipo;
  final String tipoConteudo;

  /// Endereço temporário (24 h) só para baixar. Não é gravado.
  final String? url;

  /// SHA-256 do arquivo, em hexadecimal minúsculo.
  final String hash;
  final int tamanhoBytes;
  final int duracaoSegundos;

  /// Bits dos dias: domingo = 1, segunda = 2, … sábado = 64.
  final int diasSemana;

  /// Datas (sem hora) do período de exibição, em UTC à meia-noite.
  final DateTime? inicioExibicao;
  final DateTime? fimExibicao;

  /// Nome do arquivo local: o próprio hash, então a mesma mídia em duas
  /// grades é um arquivo só e não é baixada de novo.
  String get nomeArquivo => '$hash${tipoConteudo == 'video/mp4' ? '.mp4' : tipoConteudo == 'image/png' ? '.png' : '.jpg'}';

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
      );

  final bool alterada;
  final String versaoGrade;
  final String fusoHorario;
  final int intervaloSincronizacaoSegundos;
  final String nomeTela;
  final String? novoToken;

  /// Só vem quando `alterada` é verdadeiro.
  final List<ItemGrade>? itens;
}
