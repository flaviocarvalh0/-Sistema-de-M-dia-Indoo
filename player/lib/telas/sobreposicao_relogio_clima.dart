import 'dart:async';

import 'package:flutter/material.dart';

import '../dados/modelos.dart';
import '../reproducao/filtro_do_dia.dart';

/// Ícone de cada tempo que a API manda em `icone`.
IconData iconeDoTempo(String nome) => switch (nome) {
      'sol' => Icons.wb_sunny,
      'sol-nuvem' => Icons.wb_cloudy,
      'nuvem' => Icons.cloud,
      'neblina' => Icons.foggy,
      'garoa' => Icons.grain,
      'chuva' => Icons.umbrella,
      'neve' => Icons.ac_unit,
      'tempestade' => Icons.thunderstorm,
      _ => Icons.cloud,
    };

/// Relógio (na hora do estabelecimento) e clima, no canto da tela, por cima
/// de anúncios e notícias. Cada um aparece se a playlist pedir; o clima só
/// com leitura recente (até 6 h), e com o crédito do Open-Meteo.
class SobreposicaoRelogioClima extends StatefulWidget {
  const SobreposicaoRelogioClima({
    super.key,
    required this.fusoHorario,
    required this.exibirRelogio,
    required this.clima,
    this.agora = DateTime.now,
    this.alinhamento = Alignment.topRight,
  });

  final String fusoHorario;
  final bool exibirRelogio;

  /// Nulo (ou vencido) esconde o clima.
  final Clima? clima;

  /// Relógio injetável (testes).
  final DateTime Function() agora;

  /// Canto da tela (vai para a esquerda quando o QR code do anúncio ocupa a direita).
  final Alignment alinhamento;

  @override
  State<SobreposicaoRelogioClima> createState() => _SobreposicaoRelogioClimaState();
}

class _SobreposicaoRelogioClimaState extends State<SobreposicaoRelogioClima> {
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _agendar();
  }

  /// Redesenha na virada de cada minuto.
  void _agendar() {
    final agora = widget.agora();
    _timer = Timer(Duration(seconds: 60 - agora.second, milliseconds: -agora.millisecond), () {
      if (!mounted) return;
      setState(() {});
      _agendar();
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  static const _dias = ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb'];
  static const _meses = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];

  @override
  Widget build(BuildContext context) {
    final agora = widget.agora();
    final clima = widget.clima;
    final mostrarClima = clima != null && clima.valido(agora);
    if (!widget.exibirRelogio && !mostrarClima) return const SizedBox.shrink();

    final largura = MediaQuery.sizeOf(context).width;
    final base = (largura / 48).clamp(14.0, 40.0);
    final local = FiltroDoDia.horaLocal(widget.fusoHorario, agora);
    final hora = '${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}';
    final data = '${_dias[local.weekday % 7]}, ${local.day} ${_meses[local.month - 1]}';

    return Align(
      alignment: widget.alinhamento,
      child: Padding(
        padding: EdgeInsets.all(base),
        child: Container(
          padding: EdgeInsets.symmetric(horizontal: base * 0.8, vertical: base * 0.4),
          decoration: BoxDecoration(color: Colors.black.withValues(alpha: 0.55), borderRadius: BorderRadius.circular(base)),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (mostrarClima) ...[
                Icon(iconeDoTempo(clima.icone), color: Colors.white, size: base * 1.4),
                SizedBox(width: base * 0.4),
                Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('${clima.temperaturaC.round()}°',
                        key: const ValueKey('temperatura'),
                        style: TextStyle(color: Colors.white, fontSize: base * 1.2, fontWeight: FontWeight.w600)),
                    Text(clima.credito, style: TextStyle(color: Colors.white54, fontSize: base * 0.45)),
                  ],
                ),
                if (widget.exibirRelogio)
                  Container(
                    width: 1,
                    height: base * 1.6,
                    margin: EdgeInsets.symmetric(horizontal: base * 0.7),
                    color: Colors.white30,
                  ),
              ],
              if (widget.exibirRelogio)
                Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text(hora,
                        key: const ValueKey('hora'),
                        style: TextStyle(color: Colors.white, fontSize: base * 1.2, fontWeight: FontWeight.w600)),
                    Text(data, style: TextStyle(color: Colors.white70, fontSize: base * 0.55)),
                  ],
                ),
            ],
          ),
        ),
      ),
    );
  }
}
