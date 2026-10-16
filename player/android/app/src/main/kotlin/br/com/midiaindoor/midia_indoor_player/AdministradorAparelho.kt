package br.com.midiaindoor.midia_indoor_player

import android.app.admin.DeviceAdminReceiver

/**
 * Permite tornar o player "dono do aparelho" (device owner) pelo adb, para
 * instalar as atualizações sem confirmação na TV:
 *
 *     adb shell dpm set-device-owner br.com.midiaindoor.midia_indoor_player/.AdministradorAparelho
 *
 * Não usa nenhuma política além disso.
 */
class AdministradorAparelho : DeviceAdminReceiver()
