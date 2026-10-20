import 'package:flutter/material.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../dados/modelos.dart';

/// QR code por cima da mídia: um cartão escuro, com o QR num quadro branco e
/// a legenda embaixo (e, no QR do diretório, o convite "Quer anunciar aqui?").
///
/// Feito para ser lido de longe: o QR fica em fundo branco com margem (a
/// "zona de silêncio" que o leitor precisa), correção de erro M e tamanho
/// proporcional à tela — o médio ocupa 30% da menor dimensão, o que numa TV
/// de 50" dá cerca de 18 cm, legível a uns 2 m. O cartão escuro em volta só
/// emoldura: nunca encosta nos módulos do QR.
class QrCodeSobreposto extends StatelessWidget {
  const QrCodeSobreposto({super.key, required this.qrCode});

  final QrCodeAnuncio qrCode;

  static const fundoEscuro = Color(0xFF10201F);
  static const fundoClaro = Color(0xFF173330);
  static const menta = Color(0xFFB8F36B);

  static double fracao(TamanhoQrCode tamanho) => switch (tamanho) {
        TamanhoQrCode.pequeno => 0.22,
        TamanhoQrCode.medio => 0.30,
        TamanhoQrCode.grande => 0.40,
      };

  static Alignment alinhamento(PosicaoQrCode posicao) => switch (posicao) {
        PosicaoQrCode.inferiorDireito => Alignment.bottomRight,
        PosicaoQrCode.inferiorEsquerdo => Alignment.bottomLeft,
        PosicaoQrCode.superiorDireito => Alignment.topRight,
        PosicaoQrCode.superiorEsquerdo => Alignment.topLeft,
      };

  @override
  Widget build(BuildContext context) {
    final menor = MediaQuery.sizeOf(context).shortestSide;
    final lado = menor * fracao(qrCode.tamanho);
    final margem = menor * 0.03;
    final legenda = qrCode.legenda.trim();
    final convite = qrCode.convite?.trim() ?? '';
    final temTexto = legenda.isNotEmpty || convite.isNotEmpty;

    return Align(
      alignment: alinhamento(qrCode.posicao),
      child: Padding(
        padding: EdgeInsets.all(margem),
        child: Container(
          key: const ValueKey('qr-code-anuncio'),
          width: lado,
          padding: EdgeInsets.all(lado * 0.05),
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: [fundoClaro, fundoEscuro],
            ),
            borderRadius: BorderRadius.circular(lado * 0.09),
            border: Border.all(color: const Color(0x339DEAC4), width: lado * 0.006),
            boxShadow: const [BoxShadow(color: Color(0x80000000), blurRadius: 24, offset: Offset(0, 8))],
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                padding: EdgeInsets.all(lado * 0.035),
                decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(lado * 0.06)),
                child: QrImageView(
                  data: qrCode.conteudo,
                  errorCorrectionLevel: QrErrorCorrectLevel.M,
                  padding: EdgeInsets.all(lado * 0.03),
                  backgroundColor: Colors.white,
                  eyeStyle: const QrEyeStyle(eyeShape: QrEyeShape.square, color: fundoEscuro),
                  dataModuleStyle: const QrDataModuleStyle(dataModuleShape: QrDataModuleShape.square, color: fundoEscuro),
                ),
              ),
              if (temTexto) SizedBox(height: lado * 0.045),
              if (legenda.isNotEmpty)
                Text(
                  legenda,
                  textAlign: TextAlign.center,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: lado * 0.075, height: 1.15),
                ),
              if (convite.isNotEmpty)
                Padding(
                  padding: EdgeInsets.only(top: lado * 0.025),
                  child: Text(
                    convite,
                    key: const ValueKey('qr-convite'),
                    textAlign: TextAlign.center,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(color: menta, fontWeight: FontWeight.w900, fontSize: lado * 0.08, height: 1.15),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
