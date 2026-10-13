import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:wakelock_plus/wakelock_plus.dart';

import 'app.dart';
import 'reproducao/filtro_do_dia.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // TV: tela cheia, deitada e sempre acesa.
  await SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
  await SystemChrome.setPreferredOrientations([DeviceOrientation.landscapeLeft, DeviceOrientation.landscapeRight]);
  await WakelockPlus.enable();
  FiltroDoDia.iniciarFusos();

  final servicos = await Servicos.iniciar();
  runApp(MidiaIndoorApp(servicos: servicos));
}
