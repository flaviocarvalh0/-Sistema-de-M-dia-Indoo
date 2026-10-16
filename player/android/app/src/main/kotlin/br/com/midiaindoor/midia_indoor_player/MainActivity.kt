package br.com.midiaindoor.midia_indoor_player

import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.io.File
import kotlin.concurrent.thread

class MainActivity : FlutterActivity() {
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        // Atualização automática (ADR-021): o Dart baixa o APK e pede a instalação aqui.
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CANAL_ATUALIZADOR).setMethodCallHandler { call, result ->
            when (call.method) {
                "instalar" -> {
                    val caminho = call.argument<String>("caminho")
                    if (caminho == null) {
                        result.error("argumento", "caminho do APK ausente", null)
                        return@setMethodCallHandler
                    }
                    // Copiar o APK para a sessão do instalador leva alguns segundos: fora da thread da tela.
                    thread(name = "instalador-apk") {
                        try {
                            Instalador.instalar(applicationContext, File(caminho))
                            runOnUiThread { result.success(null) }
                        } catch (e: Exception) {
                            runOnUiThread { result.error("instalacao", e.message, null) }
                        }
                    }
                }
                "donoDoAparelho" -> result.success(Instalador.donoDoAparelho(applicationContext))
                else -> result.notImplemented()
            }
        }
    }

    companion object {
        private const val CANAL_ATUALIZADOR = "br.com.midiaindoor/atualizador"
    }
}
