import 'package:flutter/material.dart';

import '../app.dart';
import '../config/versao_app.dart';
import '../dados/api_dispositivo.dart';
import 'reproducao_page.dart';

/// Pareamento: a pessoa digita o código gerado no painel (Telas → Gerar
/// código). Letras e números de tamanho grande, para ler de longe.
class PareamentoPage extends StatefulWidget {
  const PareamentoPage({super.key, required this.servicos, this.podeVoltar = false});

  final Servicos servicos;

  /// Aberta a partir da reprodução (há grade baixada): mostra "Voltar".
  final bool podeVoltar;

  @override
  State<PareamentoPage> createState() => _PareamentoPageState();
}

class _PareamentoPageState extends State<PareamentoPage> {
  final _codigo = TextEditingController();
  late final _servidor = TextEditingController(text: widget.servicos.urlServidor);
  bool _enviando = false;
  bool _editandoServidor = false;
  String? _erro;

  @override
  void dispose() {
    _codigo.dispose();
    _servidor.dispose();
    super.dispose();
  }

  Future<void> _parear() async {
    final codigo = ApiDispositivo.normalizarCodigo(_codigo.text);
    if (codigo.length != 8) {
      setState(() => _erro = 'O código tem 8 letras e números.');
      return;
    }

    setState(() {
      _enviando = true;
      _erro = null;
    });

    final s = widget.servicos;
    try {
      if (_servidor.text.trim() != s.urlServidor) await s.definirServidor(_servidor.text);
      final pareamento = await s.api.autenticar(
        codigo: codigo,
        dispositivoId: await s.credenciais.dispositivoId(),
        versaoApp: VersaoApp.nome,
      );
      await s.credenciais.gravarToken(pareamento.token);
      await s.banco.gravarEstado(
        nomeTela: pareamento.nomeTela,
        intervaloSincronizacaoSegundos: pareamento.intervaloSincronizacaoSegundos,
      );
      if (!mounted) return;
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute<void>(builder: (_) => ReproducaoPage(servicos: s)),
        (_) => false,
      );
    } on FalhaApi catch (e) {
      setState(() => _erro = e.mensagem);
    } catch (e) {
      setState(() => _erro = 'Não foi possível parear: $e');
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final tema = Theme.of(context);
    return Scaffold(
      backgroundColor: const Color(0xFF0F1F2E),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(48),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 720),
            child: FocusTraversalGroup(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text('Mídia Indoor', style: tema.textTheme.displaySmall?.copyWith(fontWeight: FontWeight.bold)),
                  const SizedBox(height: 8),
                  Text(
                    'No painel, abra Telas, escolha esta TV e use "Gerar código". Digite o código aqui.',
                    style: tema.textTheme.titleMedium?.copyWith(color: Colors.white70),
                  ),
                  const SizedBox(height: 32),
                  TextField(
                    controller: _codigo,
                    autofocus: true,
                    enabled: !_enviando,
                    textCapitalization: TextCapitalization.characters,
                    textAlign: TextAlign.center,
                    maxLength: 9,
                    style: tema.textTheme.displayMedium?.copyWith(letterSpacing: 12, fontFamily: 'monospace'),
                    decoration: const InputDecoration(
                      labelText: 'Código de pareamento',
                      counterText: '',
                      border: OutlineInputBorder(),
                    ),
                    onSubmitted: (_) => _parear(),
                  ),
                  if (_erro != null) ...[
                    const SizedBox(height: 16),
                    Text(_erro!, style: tema.textTheme.titleMedium?.copyWith(color: tema.colorScheme.error)),
                  ],
                  const SizedBox(height: 24),
                  FilledButton.icon(
                    onPressed: _enviando ? null : _parear,
                    icon: _enviando
                        ? const SizedBox.square(dimension: 20, child: CircularProgressIndicator(strokeWidth: 2))
                        : const Icon(Icons.link),
                    label: const Padding(padding: EdgeInsets.all(12), child: Text('Parear', style: TextStyle(fontSize: 22))),
                  ),
                  if (widget.podeVoltar) ...[
                    const SizedBox(height: 12),
                    OutlinedButton(
                      onPressed: _enviando ? null : () => Navigator.of(context).pop(),
                      child: const Padding(padding: EdgeInsets.all(12), child: Text('Voltar a tocar')),
                    ),
                  ],
                  const SizedBox(height: 40),
                  if (_editandoServidor)
                    TextField(
                      controller: _servidor,
                      enabled: !_enviando,
                      keyboardType: TextInputType.url,
                      decoration: const InputDecoration(labelText: 'Endereço do servidor', border: OutlineInputBorder()),
                    )
                  else
                    TextButton(
                      onPressed: () => setState(() => _editandoServidor = true),
                      child: Text('Servidor: ${widget.servicos.urlServidor}  ·  alterar',
                          style: const TextStyle(color: Colors.white54)),
                    ),
                  const SizedBox(height: 8),
                  Text('Versão ${VersaoApp.nome}',
                      textAlign: TextAlign.center, style: const TextStyle(color: Colors.white38)),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
