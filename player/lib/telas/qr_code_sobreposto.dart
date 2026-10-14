import 'package:flutter/material.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../dados/modelos.dart';

/// QR code do anúncio, por cima da mídia, num cartão branco com a legenda.
///
/// Feito para ser lido de longe: fundo branco com margem (a "zona de
/// silêncio" que o leitor precisa), correção de erro M e tamanho proporcional
/// à tela — o médio ocupa 30% da menor dimensão, o que numa TV de 50" dá
/// cerca de 18 cm, legível a uns 2 m.
class QrCodeSobreposto extends StatelessWidget {
  const QrCodeSobreposto({super.key, required this.qrCode});

  final QrCodeAnuncio qrCode;

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

    return Align(
      alignment: alinhamento(qrCode.posicao),
      child: Padding(
        padding: EdgeInsets.all(margem),
        child: Container(
          key: const ValueKey('qr-code-anuncio'),
          width: lado,
          padding: EdgeInsets.all(lado * 0.04),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(lado * 0.06),
            boxShadow: const [BoxShadow(color: Colors.black38, blurRadius: 12)],
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              QrImageView(
                data: qrCode.conteudo,
                errorCorrectionLevel: QrErrorCorrectLevel.M,
                padding: EdgeInsets.all(lado * 0.04),
                backgroundColor: Colors.white,
                eyeStyle: const QrEyeStyle(eyeShape: QrEyeShape.square, color: Colors.black),
                dataModuleStyle: const QrDataModuleStyle(dataModuleShape: QrDataModuleShape.square, color: Colors.black),
              ),
              if (legenda.isNotEmpty)
                Padding(
                  padding: EdgeInsets.only(bottom: lado * 0.02),
                  child: Text(
                    legenda,
                    textAlign: TextAlign.center,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(color: Colors.black87, fontWeight: FontWeight.w700, fontSize: lado * 0.075),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
