import 'package:flutter_test/flutter_test.dart';
import 'package:midia_indoor_player/dados/modelos.dart';
import 'package:midia_indoor_player/reproducao/filtro_do_dia.dart';

ItemGrade item(int ordem, {int dias = 127, DateTime? inicio, DateTime? fim}) => ItemGrade(
      ordem: ordem,
      midiaId: ordem,
      tipo: TipoMidia.imagem,
      tipoConteudo: 'image/png',
      hash: 'h$ordem',
      tamanhoBytes: 1,
      duracaoSegundos: 10,
      diasSemana: dias,
      inicioExibicao: inicio,
      fimExibicao: fim,
    );

void main() {
  // 2026-10-13 é terça-feira.
  final terca = DateTime.utc(2026, 10, 13);

  test('bit do dia segue o contrato (domingo = 1 … sábado = 64)', () {
    expect(FiltroDoDia.bitDoDia(DateTime.utc(2026, 10, 11)), 1); // domingo
    expect(FiltroDoDia.bitDoDia(DateTime.utc(2026, 10, 12)), 2); // segunda
    expect(FiltroDoDia.bitDoDia(terca), 4);
    expect(FiltroDoDia.bitDoDia(DateTime.utc(2026, 10, 17)), 64); // sábado
  });

  test('"hoje" é a data no fuso do parceiro', () {
    final agora = DateTime.utc(2026, 10, 14, 2, 30); // 23:30 de terça em São Paulo
    expect(FiltroDoDia.hoje('America/Sao_Paulo', agora), terca);
    expect(FiltroDoDia.hoje('America/Manaus', agora), terca);
    expect(FiltroDoDia.hoje('Asia/Tokyo', agora), DateTime.utc(2026, 10, 14));
  });

  test('fuso desconhecido cai em São Paulo', () {
    expect(FiltroDoDia.hoje('Lua/Base', DateTime.utc(2026, 10, 14, 2, 30)), terca);
  });

  test('filtra por dia da semana e período, na ordem do loop', () {
    final grade = [
      item(3),
      item(0, dias: 62), // seg a sex
      item(1, dias: 65), // fim de semana
      item(2, inicio: DateTime.utc(2026, 10, 14)), // começa amanhã
      item(4, fim: DateTime.utc(2026, 10, 12)), // acabou ontem
      item(5, inicio: terca, fim: terca), // só hoje
    ];
    expect(FiltroDoDia.itensDoDia(grade, terca).map((i) => i.ordem), [0, 3, 5]);
    expect(FiltroDoDia.itensDoDia(grade, DateTime.utc(2026, 10, 17)).map((i) => i.ordem), [1, 2, 3]);
  });

  test('próximo item dá a volta no fim do loop', () {
    final doDia = [item(0), item(3), item(5)];
    expect(FiltroDoDia.proximo(doDia, null)?.ordem, 0);
    expect(FiltroDoDia.proximo(doDia, 0)?.ordem, 3);
    expect(FiltroDoDia.proximo(doDia, 3)?.ordem, 5);
    expect(FiltroDoDia.proximo(doDia, 5)?.ordem, 0);
    // O item atual saiu do dia (ex.: 4): segue para o seguinte.
    expect(FiltroDoDia.proximo(doDia, 4)?.ordem, 5);
    expect(FiltroDoDia.proximo(const [], 0), isNull);
  });

  test('item da API vira arquivo nomeado pelo hash', () {
    expect(ItemGrade.fromJson({
      'ordem': 0, 'midiaId': 1, 'tipo': 'Video', 'tipoConteudo': 'video/mp4', 'url': 'u',
      'hash': 'ABC', 'tamanhoBytes': 1, 'duracaoSegundos': 5, 'diasSemana': 127,
      'inicioExibicao': '2026-10-13', 'fimExibicao': null, 'exibirHoje': true,
    }).nomeArquivo, 'abc.mp4');
  });
}
