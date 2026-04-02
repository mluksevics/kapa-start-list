package com.orienteering.startref.data.si

import com.hoho.android.usbserial.driver.Cp21xxSerialDriver
import com.hoho.android.usbserial.driver.UsbSerialProber

/**
 * Custom USB serial prober that extends the default probe table with
 * additional Silicon Labs CP21xx PIDs used by SportIdent stations.
 *
 * Known SI station chips:
 *   VID 0x10C4 (Silicon Labs), PID 0x800A — found on BS11/BSM8 stations
 *   VID 0x10C4 (Silicon Labs), PID 0xEA60 — standard CP2102 (in default prober)
 */
object SiProber {
    fun get(): UsbSerialProber {
        val table = UsbSerialProber.getDefaultProbeTable()
        table.addProduct(0x10C4, 0x800A, Cp21xxSerialDriver::class.java)
        return UsbSerialProber(table)
    }
}
