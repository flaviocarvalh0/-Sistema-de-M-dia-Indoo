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
    this.qrCode,
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
        qrCode: json['qrCode'] is Map<String, dynamic> ? QrCodeAnuncio.fromJson(json['qrCode'] as Map<String, dynamic>) : null,
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

  /// QR code por cima da mídia; nulo = sem QR code.
  final QrCodeAnuncio? qrCode;

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

enum PosicaoQrCode { inferiorDireito, inferiorEsquerdo, superiorDireito, superiorEsquerdo }

enum TamanhoQrCode { pequeno, medio, grande }

/// QR code do anúncio: o conteúdo (link, tel: ou Pix copia e cola), a
/// legenda e onde aparece. A TV gera a imagem do QR na hora, offline.
class QrCodeAnuncio {
  const QrCodeAnuncio({
    required this.conteudo,
    required this.legenda,
    this.posicao = PosicaoQrCode.inferiorDireito,
    this.tamanho = TamanhoQrCode.medio,
  });

  factory QrCodeAnuncio.fromJson(Map<String, dynamic> json) => QrCodeAnuncio(
        conteudo: json['conteudo'] as String,
        legenda: json['legenda'] as String? ?? '',
        posicao: _enum(PosicaoQrCode.values, json['posicao']) ?? PosicaoQrCode.inferiorDireito,
        tamanho: _enum(TamanhoQrCode.values, json['tamanho']) ?? TamanhoQrCode.medio,
      );

  final String conteudo;
  final String legenda;
  final PosicaoQrCode posicao;
  final TamanhoQrCode tamanho;

  /// A API manda o nome com inicial maiúscula (`InferiorDireito`).
  static T? _enum<T extends Enum>(List<T> valores, Object? nome) {
    if (nome is! String || nome.isEmpty) return null;
    final procurado = nome[0].toLowerCase() + nome.substring(1);
    for (final v in valores) {
      if (v.name == procurado) return v;
    }
    return null;
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
    this.cartaoTempo = false,
    this.cartaoCotacoes = false,
    this.duracaoMaximaVoltaSegundos = 0,
  });

  factory Composicao.fromJson(Map<String, dynamic> json) => Composicao(
        noticiaACadaAnuncios: (json['noticiaACadaAnuncios'] as num?)?.toInt() ?? 0,
        duracaoNoticiaSegundos: (json['duracaoNoticiaSegundos'] as num?)?.toInt() ?? 10,
        embaralharAnuncios: json['embaralharAnuncios'] as bool? ?? false,
        exibirRelogio: json['exibirRelogio'] as bool? ?? true,
        exibirClima: json['exibirClima'] as bool? ?? true,
        cartaoTempo: json['cartaoTempo'] as bool? ?? false,
        cartaoCotacoes: json['cartaoCotacoes'] as bool? ?? false,
        duracaoMaximaVoltaSegundos: (json['duracaoMaximaVoltaSegundos'] as num?)?.toInt() ?? 0,
      );

  /// Um informativo (notícia ou cartão) depois de cada N anúncios; 0 = nenhum.
  final int noticiaACadaAnuncios;
  final int duracaoNoticiaSegundos;
  final bool embaralharAnuncios;
  final bool exibirRelogio;
  final bool exibirClima;

  /// Cartões no rodízio dos informativos, junto com as notícias.
  final bool cartaoTempo;
  final bool cartaoCotacoes;

  /// Segundos de anúncios por volta; 0 = todos em toda volta. Com limite, a
  /// fila vira um carrossel com cursor (ver [ComposicaoFila]).
  final int duracaoMaximaVoltaSegundos;

  bool get carrossel => duracaoMaximaVoltaSegundos > 0;

  Map<String, dynamic> toJson() => {
        'noticiaACadaAnuncios': noticiaACadaAnuncios,
        'duracaoNoticiaSegundos': duracaoNoticiaSegundos,
        'embaralharAnuncios': embaralharAnuncios,
        'exibirRelogio': exibirRelogio,
        'exibirClima': exibirClima,
        'cartaoTempo': cartaoTempo,
        'cartaoCotacoes': cartaoCotacoes,
        'duracaoMaximaVoltaSegundos': duracaoMaximaVoltaSegundos,
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
    this.dias = const [],
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
        dias: (json['dias'] as List<dynamic>? ?? const [])
            .map((e) => DiaPrevisao.fromJson(e as Map<String, dynamic>))
            .toList(),
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

  /// Hoje e os próximos dias (cartão "Previsão do tempo").
  final List<DiaPrevisao> dias;

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
        'dias': [for (final d in dias) d.toJson()],
      };
}

/// Um dia da previsão do tempo.
class DiaPrevisao {
  const DiaPrevisao({
    required this.data,
    required this.minimaC,
    required this.maximaC,
    required this.descricao,
    required this.icone,
  });

  factory DiaPrevisao.fromJson(Map<String, dynamic> json) => DiaPrevisao(
        data: DateTime.parse(json['data'] as String),
        minimaC: (json['minimaC'] as num).toDouble(),
        maximaC: (json['maximaC'] as num).toDouble(),
        descricao: json['descricao'] as String,
        icone: json['icone'] as String,
      );

  /// Data (sem hora) no fuso do estabelecimento.
  final DateTime data;
  final double minimaC;
  final double maximaC;
  final String descricao;
  final String icone;

  Map<String, dynamic> toJson() => {
        'data': '${data.year.toString().padLeft(4, '0')}-${data.month.toString().padLeft(2, '0')}-${data.day.toString().padLeft(2, '0')}',
        'minimaC': minimaC,
        'maximaC': maximaC,
        'descricao': descricao,
        'icone': icone,
      };
}

/// Cotação oficial (PTAX do Banco Central) para o cartão "Cotações".
class Cotacao {
  const Cotacao({
    required this.moeda,
    required this.nome,
    required this.compra,
    required this.venda,
    required this.cotadaEm,
    required this.credito,
    this.variacaoPercentual,
    this.boletim,
  });

  factory Cotacao.fromJson(Map<String, dynamic> json) => Cotacao(
        moeda: json['moeda'] as String,
        nome: json['nome'] as String,
        compra: (json['compra'] as num).toDouble(),
        venda: (json['venda'] as num).toDouble(),
        variacaoPercentual: (json['variacaoPercentual'] as num?)?.toDouble(),
        cotadaEm: DateTime.parse(json['cotadaEm'] as String),
        boletim: json['boletim'] as String?,
        credito: json['credito'] as String? ?? 'Fonte: Banco Central (PTAX)',
      );

  /// Cotação mais velha que isto sai da tela (cobre fim de semana e feriado).
  static const validade = Duration(days: 4);

  final String moeda;
  final String nome;
  final double compra;
  final double venda;
  final double? variacaoPercentual;
  final DateTime cotadaEm;
  final String? boletim;
  final String credito;

  bool valida(DateTime agora) => agora.toUtc().difference(cotadaEm.toUtc()) < validade;

  Map<String, dynamic> toJson() => {
        'moeda': moeda,
        'nome': nome,
        'compra': compra,
        'venda': venda,
        'variacaoPercentual': variacaoPercentual,
        'cotadaEm': cotadaEm.toIso8601String(),
        'boletim': boletim,
        'credito': credito,
      };
}

/// Versão do app mais nova liberada para esta TV (ADR-021): o APK é baixado
/// como as mídias (SHA-256 conferido) e instalado.
class AtualizacaoApp implements ArquivoRemoto {
  const AtualizacaoApp({
    required this.codigo,
    required this.nome,
    required this.hash,
    required this.tamanhoBytes,
    this.url,
  });

  factory AtualizacaoApp.fromJson(Map<String, dynamic> json) => AtualizacaoApp(
        codigo: (json['codigo'] as num).toInt(),
        nome: json['nome'] as String,
        url: json['url'] as String?,
        hash: (json['hash'] as String).toLowerCase(),
        tamanhoBytes: (json['tamanhoBytes'] as num).toInt(),
      );

  final int codigo;
  final String nome;
  final int tamanhoBytes;

  @override
  final String? url;

  @override
  final String hash;

  @override
  String get nomeArquivo => '$hash.apk';
}

/// QR code fixo do diretório de anunciantes da TV (ADR-018): a página
/// pública com os contatos dos anunciantes que aceitaram aparecer.
class DiretorioTv {
  const DiretorioTv({required this.url, this.legenda = 'Contatos dos anunciantes'});

  factory DiretorioTv.fromJson(Map<String, dynamic> json) => DiretorioTv(
        url: json['url'] as String,
        legenda: json['legenda'] as String? ?? 'Contatos dos anunciantes',
      );

  final String url;
  final String legenda;

  /// No canto inferior direito, pequeno: fica fora do relógio (em cima) e
  /// some quando o anúncio tem QR code próprio.
  QrCodeAnuncio get qrCode => QrCodeAnuncio(conteudo: url, legenda: legenda, tamanho: TamanhoQrCode.pequeno);

  Map<String, dynamic> toJson() => {'url': url, 'legenda': legenda};
}

/// Resposta de `GET /sincronizacao`.
/// Um anúncio que esteve na tela (prova de veiculação, ADR-023). Gravado no
/// banco local e enviado em lote; o [id] (UUID) torna o reenvio idempotente.
class Exibicao {
  const Exibicao({
    required this.id,
    required this.midiaId,
    required this.inicio,
    required this.duracao,
    required this.concluida,
    required this.semConexao,
  });

  factory Exibicao.daLinha(Map<String, Object?> l) => Exibicao(
        id: l['id'] as String,
        midiaId: l['midia_id'] as int,
        inicio: DateTime.parse(l['inicio'] as String),
        duracao: Duration(milliseconds: l['duracao_ms'] as int),
        concluida: l['concluida'] == 1,
        semConexao: l['sem_conexao'] == 1,
      );

  final String id;
  final int midiaId;

  /// Quando apareceu (UTC).
  final DateTime inicio;
  final Duration duracao;

  /// Foi até o fim (falso = interrompida por erro).
  final bool concluida;

  /// A TV não estava conseguindo falar com a API quando exibiu.
  final bool semConexao;

  Map<String, Object?> paraLinha() => {
        'id': id,
        'midia_id': midiaId,
        'inicio': inicio.toUtc().toIso8601String(),
        'duracao_ms': duracao.inMilliseconds,
        'concluida': concluida ? 1 : 0,
        'sem_conexao': semConexao ? 1 : 0,
      };

  Map<String, Object?> toJson() => {
        'id': id,
        'midiaId': midiaId,
        'inicio': inicio.toUtc().toIso8601String(),
        'duracaoMs': duracao.inMilliseconds,
        'concluida': concluida,
        'semConexao': semConexao,
      };
}

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
    this.cotacoes,
    this.diretorio,
    this.atualizacao,
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
        cotacoes: (json['cotacoes'] as List<dynamic>?)
            ?.map((e) => Cotacao.fromJson(e as Map<String, dynamic>))
            .toList(),
        diretorio: json['diretorio'] is Map<String, dynamic>
            ? DiretorioTv.fromJson(json['diretorio'] as Map<String, dynamic>)
            : null,
        atualizacao: json['atualizacao'] is Map<String, dynamic>
            ? AtualizacaoApp.fromJson(json['atualizacao'] as Map<String, dynamic>)
            : null,
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

  /// Nulo: a playlist não usa o cartão de cotações.
  final List<Cotacao>? cotacoes;

  /// Nulo: diretório desligado na tela (ou API sem endereço público).
  final DiretorioTv? diretorio;

  /// Nulo: o app já está na versão mais nova liberada para esta TV.
  final AtualizacaoApp? atualizacao;
}
