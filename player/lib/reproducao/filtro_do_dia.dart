import 'package:timezone/data/latest.dart' as tzdata;
import 'package:timezone/timezone.dart' as tz;

import '../dados/modelos.dart';

/// Regra 5 do contrato: o que toca HOJE, calculado no aparelho (sem internet
/// a TV precisa saber sozinha que o dia virou).
class FiltroDoDia {
  FiltroDoDia._();

  static bool _iniciado = false;

  /// Carrega a base de fusos. Chame uma vez ao abrir o app.
  static void iniciarFusos() {
    if (_iniciado) return;
    tzdata.initializeTimeZones();
    _iniciado = true;
  }

  /// A data de hoje no fuso do parceiro, como `DateTime.utc(ano, mês, dia)`.
  static DateTime hoje(String fusoHorario, DateTime agora) {
    iniciarFusos();
    tz.Location local;
    try {
      local = tz.getLocation(fusoHorario);
    } on tz.LocationNotFoundException {
      local = tz.getLocation('America/Sao_Paulo');
    }
    final noFuso = tz.TZDateTime.from(agora.toUtc(), local);
    return DateTime.utc(noFuso.year, noFuso.month, noFuso.day);
  }

  /// Bit do dia da semana: domingo = 1, segunda = 2, … sábado = 64.
  static int bitDoDia(DateTime dia) => 1 << (dia.weekday % 7);

  static bool tocaNoDia(ItemGrade item, DateTime dia) =>
      (item.diasSemana & bitDoDia(dia)) != 0 &&
      (item.inicioExibicao == null || !item.inicioExibicao!.isAfter(dia)) &&
      (item.fimExibicao == null || !item.fimExibicao!.isBefore(dia));

  /// Itens de [grade] que tocam em [dia], na ordem do loop.
  static List<ItemGrade> itensDoDia(List<ItemGrade> grade, DateTime dia) =>
      grade.where((i) => tocaNoDia(i, dia)).toList()..sort((a, b) => a.ordem.compareTo(b.ordem));

  /// Próximo item do loop depois de [ordemAtual] (o primeiro, se acabou a
  /// volta ou se ainda não tocou nada).
  static ItemGrade? proximo(List<ItemGrade> doDia, int? ordemAtual) {
    if (doDia.isEmpty) return null;
    if (ordemAtual == null) return doDia.first;
    return doDia.firstWhere((i) => i.ordem > ordemAtual, orElse: () => doDia.first);
  }
}
