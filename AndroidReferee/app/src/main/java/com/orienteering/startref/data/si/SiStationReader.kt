package com.orienteering.startref.data.si

import android.content.Context
import android.hardware.usb.UsbManager
import com.hoho.android.usbserial.driver.UsbSerialProber
import com.hoho.android.usbserial.util.SerialInputOutputManager
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.receiveAsFlow
import java.util.concurrent.Executors
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Reads SI card numbers from a SportIdent BSM-7/8 station connected via USB OTG.
 *
 * Uses usb-serial-for-android (github.com/mik3y/usb-serial-for-android) for
 * serial communication at 38400 baud (SportIdent protocol).
 *
 * SI card insert events are parsed from the raw serial byte stream and emitted
 * as card numbers via [cardReadEvents].
 *
 * Lifecycle: call [start] when the Gate screen becomes active, [stop] when leaving.
 * Requires USB host permission to be granted at runtime before calling [start].
 */
@Singleton
class SiStationReader @Inject constructor(
    private val context: Context
) {
    private val _cardReadEvents = Channel<String>(Channel.BUFFERED)
    val cardReadEvents: Flow<String> = _cardReadEvents.receiveAsFlow()

    private var ioManager: SerialInputOutputManager? = null
    private val executor = Executors.newSingleThreadExecutor()

    // Accumulation buffer for parsing multi-byte SI protocol frames
    private val buffer = mutableListOf<Byte>()

    fun start() {
        val usbManager = context.getSystemService(Context.USB_SERVICE) as UsbManager
        val availableDrivers = UsbSerialProber.getDefaultProber().findAllDrivers(usbManager)
        if (availableDrivers.isEmpty()) return

        val driver = availableDrivers.first()
        val connection = usbManager.openDevice(driver.device) ?: return

        runCatching {
            val port = driver.ports.first()
            port.open(connection)
            port.setParameters(38400, 8, 1, 0) // 38400 baud, 8N1

            ioManager = SerialInputOutputManager(port, object : SerialInputOutputManager.Listener {
                override fun onNewData(data: ByteArray) {
                    parseData(data)
                }
                override fun onRunError(e: Exception) {
                    stop()
                }
            })
            executor.submit(ioManager)
        }
    }

    fun stop() {
        ioManager?.listener = null
        ioManager?.stop()
        ioManager = null
        buffer.clear()
    }

    /**
     * Parses SportIdent serial frames to extract SI card numbers.
     *
     * SportIdent BSM-7/8 Extended Protocol (EPS):
     * - STX (0xFF) + CMD + LEN + DATA... + CRC + ETX (0xFF)
     * - Card insert (punch) command: 0xD3
     * - SI card number is in bytes 6–9 of the data section (4-byte big-endian)
     *
     * This is a simplified parser that looks for the card-read frame.
     */
    private fun parseData(data: ByteArray) {
        buffer.addAll(data.toList())

        // Look for complete SI frames: STX(FF) CMD LEN [DATA] CRC ETX(FF)
        val bufferList = buffer.toMutableList()
        var i = 0
        while (i < bufferList.size - 1) {
            if (bufferList[i] == 0xFF.toByte()) {
                val cmd = bufferList.getOrNull(i + 1) ?: break
                val len = bufferList.getOrNull(i + 2)?.toInt()?.and(0xFF) ?: break
                val frameEnd = i + 3 + len + 2 // 3 header + len data + 2 (CRC) + 1 ETX
                if (bufferList.size < frameEnd) break

                if (cmd == 0xD3.toByte()) {
                    // Card read frame — extract SI card number from bytes offset 6-9 in data
                    val dataStart = i + 3
                    if (len >= 9) {
                        val cardNum = ((bufferList[dataStart + 5].toLong() and 0xFF) shl 24) or
                                      ((bufferList[dataStart + 6].toLong() and 0xFF) shl 16) or
                                      ((bufferList[dataStart + 7].toLong() and 0xFF) shl 8) or
                                      (bufferList[dataStart + 8].toLong() and 0xFF)
                        _cardReadEvents.trySend(cardNum.toString())
                    }
                }
                i = frameEnd
            } else {
                i++
            }
        }
        // Keep unprocessed bytes
        buffer.clear()
        if (i < bufferList.size) buffer.addAll(bufferList.subList(i, bufferList.size))
    }
}
