package br.com.midiaindoor.midia_indoor_player

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

/**
 * Abre o player quando a TV termina de ligar (e depois de uma atualização do
 * app), para a tela voltar a tocar sem ninguém mexer no controle.
 */
class ReceptorInicializacao : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val acoes = setOf(
            Intent.ACTION_BOOT_COMPLETED,
            "android.intent.action.QUICKBOOT_POWERON",
            Intent.ACTION_MY_PACKAGE_REPLACED,
        )
        if (intent.action !in acoes) return

        val abrir = Intent(context, MainActivity::class.java).apply {
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP)
        }
        context.startActivity(abrir)
    }
}
