package br.com.midiaindoor.midia_indoor_player

import android.app.PendingIntent
import android.app.admin.DevicePolicyManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageInstaller
import android.os.Build
import java.io.File

/**
 * Instala uma versão nova do próprio app pelo PackageInstaller.
 *
 * Com o app como "dono do aparelho" (device owner, ver docs/IMPLANTACAO.md)
 * o Android instala sem perguntar nada. Sem isso, o [ReceptorInstalacao]
 * abre a confirmação na TV e alguém precisa apertar "Instalar" no controle.
 * Ao terminar, o Android reinicia o app (MY_PACKAGE_REPLACED, ver
 * [ReceptorInicializacao]).
 */
object Instalador {
    fun donoDoAparelho(context: Context): Boolean {
        val politicas = context.getSystemService(Context.DEVICE_POLICY_SERVICE) as DevicePolicyManager
        return politicas.isDeviceOwnerApp(context.packageName)
    }

    fun instalar(context: Context, apk: File) {
        require(apk.isFile) { "APK não encontrado: ${apk.path}" }

        val instalador = context.packageManager.packageInstaller
        val parametros = PackageInstaller.SessionParams(PackageInstaller.SessionParams.MODE_FULL_INSTALL).apply {
            setAppPackageName(context.packageName)
            setSize(apk.length())
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                setRequireUserAction(PackageInstaller.SessionParams.USER_ACTION_NOT_REQUIRED)
            }
        }

        val idSessao = instalador.createSession(parametros)
        instalador.openSession(idSessao).use { sessao ->
            apk.inputStream().use { entrada ->
                sessao.openWrite("app.apk", 0, apk.length()).use { saida ->
                    entrada.copyTo(saida)
                    sessao.fsync(saida)
                }
            }

            // O resultado volta por broadcast; o Android preenche os extras, por isso mutável.
            val flags = PendingIntent.FLAG_UPDATE_CURRENT or
                (if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) PendingIntent.FLAG_MUTABLE else 0)
            val retorno = PendingIntent.getBroadcast(
                context,
                idSessao,
                Intent(context, ReceptorInstalacao::class.java),
                flags,
            )
            sessao.commit(retorno.intentSender)
        }
    }
}
