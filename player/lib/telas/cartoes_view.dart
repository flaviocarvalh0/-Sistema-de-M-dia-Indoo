import 'package:flutter/material.dart';

import '../dados/modelos.dart';
import 'sobreposicao_relogio_clima.dart';

/// Fundo, faixa e crédito comuns aos cartões informativos (tempo e cotações).
class _ModeloCartao extends StatelessWidget {
  const _ModeloCartao({required this.faixa, required this.credito, required this.conteudo});

  final String faixa;
  final String credito;
  final Widget Function(double base) conteudo;

  @override
  Widget build(BuildContext context) {
    final tamanho = MediaQuery.sizeOf(context);
    // Proporcional à tela e legível a distância numa TV 1080p.
    final base = (tamanho.shortestSide / 22).clamp(14.0, 52.0);

    return DecoratedBox(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xFF0F1F2E), Color(0xFF134E4A)],
        ),
      ),
      child: SafeArea(
        child: Padding(
          padding: EdgeInsets.symmetric(horizontal: tamanho.width * 0.05, vertical: base),
          child: TweenAnimationBuilder<double>(
            tween: Tween(begin: 0, end: 1),
            duration: const Duration(milliseconds: 700),
            curve: Curves.easeOutCubic,
            builder: (context, t, filho) => Opacity(opacity: t, child: filho),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  padding: EdgeInsets.symmetric(horizontal: base * 0.5, vertical: base * 0.2),
                  decoration: BoxDecoration(color: const Color(0xFF0F766E), borderRadius: BorderRadius.circular(6)),
                  child: Text(faixa,
                      style: TextStyle(color: Colors.white, fontWeight: FontWeight.w700, letterSpacing: 2, fontSize: base * 0.45)),
                ),
                // Encolhe para caber em qualquer tela (celular em pé, TV).
                Expanded(child: Center(child: FittedBox(fit: BoxFit.scaleDown, child: conteudo(base)))),
                Align(
                  alignment: Alignment.bottomRight,
                  child: Text(credito, style: TextStyle(color: Colors.white60, fontSize: base * 0.38)),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

const _diasSemana = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];

String _graus(double valor) => '${valor.round()}°';

/// Cartão "Previsão do tempo": agora, hoje e os próximos dias.
class CartaoTempoView extends StatelessWidget {
  const CartaoTempoView({super.key, required this.clima, required this.hoje});

  final Clima clima;

  /// Hoje no fuso do estabelecimento (`DateTime.utc(ano, mês, dia)`).
  final DateTime hoje;

  @override
  Widget build(BuildContext context) {
    // Compara pelo dia do calendário (as datas vêm sem hora e sem fuso).
    int dia(DateTime d) => d.year * 10000 + d.month * 100 + d.day;
    final proximos = clima.dias.where((d) => dia(d.data) > dia(hoje)).take(3).toList();
    final doDia = clima.dias.where((d) => dia(d.data) == dia(hoje)).firstOrNull;

    return _ModeloCartao(
      faixa: 'PREVISÃO DO TEMPO',
      credito: clima.credito,
      conteudo: (base) => Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (clima.local != null)
            Text(clima.local!, style: TextStyle(color: Colors.white70, fontSize: base * 0.7)),
          SizedBox(height: base * 0.4),
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(iconeDoTempo(clima.icone), color: Colors.white, size: base * 3),
              SizedBox(width: base * 0.6),
              Text(_graus(clima.temperaturaC),
                  key: const ValueKey('agora'),
                  style: TextStyle(color: Colors.white, fontSize: base * 3, fontWeight: FontWeight.w300)),
              SizedBox(width: base * 0.8),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(clima.descricao, style: TextStyle(color: Colors.white, fontSize: base * 0.8)),
                  if ((doDia?.minimaC ?? clima.minimaC) != null && (doDia?.maximaC ?? clima.maximaC) != null)
                    Text('Mín ${_graus(doDia?.minimaC ?? clima.minimaC!)} · Máx ${_graus(doDia?.maximaC ?? clima.maximaC!)}',
                        style: TextStyle(color: Colors.white70, fontSize: base * 0.6)),
                ],
              ),
            ],
          ),
          if (proximos.isNotEmpty) ...[
            SizedBox(height: base * 1.2),
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                for (final dia in proximos)
                  Container(
                    width: base * 5,
                    margin: EdgeInsets.symmetric(horizontal: base * 0.3),
                    padding: EdgeInsets.symmetric(vertical: base * 0.4),
                    decoration: BoxDecoration(color: Colors.white10, borderRadius: BorderRadius.circular(base * 0.4)),
                    child: Column(
                      children: [
                        Text(_diasSemana[dia.data.weekday % 7],
                            style: TextStyle(color: Colors.white, fontSize: base * 0.6, fontWeight: FontWeight.w600)),
                        Icon(iconeDoTempo(dia.icone), color: Colors.white, size: base * 1.3),
                        Text('${_graus(dia.minimaC)} / ${_graus(dia.maximaC)}',
                            style: TextStyle(color: Colors.white70, fontSize: base * 0.5)),
                      ],
                    ),
                  ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

/// Cartão "Cotações": dólar e euro (venda), com a variação do dia.
class CartaoCotacoesView extends StatelessWidget {
  const CartaoCotacoesView({super.key, required this.cotacoes});

  final List<Cotacao> cotacoes;

  /// R$ 5,52 (duas casas, vírgula).
  static String reais(double valor) => 'R\$ ${valor.toStringAsFixed(2).replaceAll('.', ',')}';

  /// +1,02% / −0,35% / 0,00%.
  static String variacao(double valor) {
    final texto = valor.abs().toStringAsFixed(2).replaceAll('.', ',');
    return valor > 0 ? '+$texto%' : valor < 0 ? '−$texto%' : '$texto%';
  }

  static String _quando(DateTime d) {
    final local = d.toUtc().subtract(const Duration(hours: 3));
    String dois(int n) => n.toString().padLeft(2, '0');
    return '${dois(local.day)}/${dois(local.month)} às ${dois(local.hour)}:${dois(local.minute)}';
  }

  @override
  Widget build(BuildContext context) {
    final maisRecente = cotacoes.map((c) => c.cotadaEm).reduce((a, b) => a.isAfter(b) ? a : b);
    return _ModeloCartao(
      faixa: 'COTAÇÕES',
      credito: '${cotacoes.first.credito} · ${_quando(maisRecente)}',
      conteudo: (base) => Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          for (final c in cotacoes)
            Container(
              width: base * 9,
              margin: EdgeInsets.symmetric(horizontal: base * 0.5),
              padding: EdgeInsets.all(base * 0.7),
              decoration: BoxDecoration(color: Colors.white10, borderRadius: BorderRadius.circular(base * 0.5)),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(c.nome, style: TextStyle(color: Colors.white70, fontSize: base * 0.8)),
                  Text(reais(c.venda),
                      key: ValueKey('venda-${c.moeda}'),
                      style: TextStyle(color: Colors.white, fontSize: base * 1.8, fontWeight: FontWeight.w600)),
                  if (c.variacaoPercentual != null)
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          c.variacaoPercentual! > 0
                              ? Icons.arrow_upward
                              : c.variacaoPercentual! < 0
                                  ? Icons.arrow_downward
                                  : Icons.remove,
                          size: base * 0.7,
                          color: c.variacaoPercentual! > 0
                              ? const Color(0xFF4ADE80)
                              : c.variacaoPercentual! < 0
                                  ? const Color(0xFFF87171)
                                  : Colors.white70,
                        ),
                        Text(variacao(c.variacaoPercentual!), style: TextStyle(color: Colors.white70, fontSize: base * 0.6)),
                      ],
                    ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
