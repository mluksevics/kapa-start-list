package com.orienteering.startref.data.si

import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.hardware.usb.UsbDevice
import android.hardware.usb.UsbManager
import android.media.AudioManager
import android.media.ToneGenerator
import com.hoho.android.usbserial.driver.UsbSerialPort
import com.hoho.android.usbserial.util.SerialInputOutputManager
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import java.util.concurrent.Executors
import javax.inject.Inject
import javax.inject.Singleton

enum class SiConnectionState { DISCONNECTED, CONNECTED }

/**
 * Reads SI card numbers from a SportIdent BSM-7/8 station connected via USB OTG.
 *
 * Uses usb-serial-for-android (github.com/mik3y/usb-serial-for-android) for
 * serial communication at 38400 baud (SportIdent EPS protocol).
 *
 * SI card insert events are parsed from the raw serial byte stream and emitted
 * as card numbers via [cardReadEvents].
 *
 * Lifecycle: call [start] when the Gate/Startlist screen becomes active, [stop] when leaving.
 * Also called automatically on USB attach/detach via BroadcastReceiver in MainActivity.
 *
 * [siReaderDeviceKey] filters to a specific "vendorId:productId" device; empty = auto (first found).
 */
@Singleton
class SiStationReader @Inject constructor(
    private val context: Context,
    private val debugLog: SiDebugLog
) {
    private val _cardReadEvents = Channel<String>(Channel.BUFFERED)
    val cardReadEvents: Flow<String> = _cardReadEvents.receiveAsFlow()

    private val _connectionState = MutableStateFlow(SiConnectionState.DISCONNECTED)
    val connectionState: StateFlow<SiConnectionState> = _connectionState.asStateFlow()

    /** Set externally (from settings) before calling start(). "" = auto. */
    var siReaderDeviceKey: String = ""

    /** When true uses STREAM_ALARM (bypasses device volume). Updated live from settings. */
    var loudSound: Boolean = false

    private var ioManager: SerialInputOutputManager? = null
    private var port: UsbSerialPort? = null
    private val executor = Executors.newSingleThreadExecutor()

    // Accumulation buffer for parsing multi-byte SI protocol frames
    private val buffer = mutableListOf<Byte>()

    private val audioStream get() =
        if (loudSound) AudioManager.STREAM_ALARM else AudioManager.STREAM_MUSIC

    /** Positive confirmation — ascending two-tone "ta-daa". */
    fun playSuccess() {
        try {
            ToneGenerator(audioStream, ToneGenerator.MAX_VOLUME)
                .startTone(ToneGenerator.TONE_PROP_ACK, 600)
        } catch (e: Exception) {
            debugLog.log("playSuccess failed: ${e.message}")
        }
    }

    /** Error / warning — continuous high-pitched beep. */
    fun playError() {
        try {
            ToneGenerator(audioStream, ToneGenerator.MAX_VOLUME)
                .startTone(ToneGenerator.TONE_CDMA_HIGH_L, 800)
        } catch (e: Exception) {
            debugLog.log("playError failed: ${e.message}")
        }
    }

    /**
     * Sends raw ACK (0x06) so the station advances its state machine and doesn't
     * retry the autosend frame. Does NOT trigger a beep — that requires the station's
     * own audio feedback to be enabled via SiPuncher.
     */
    private fun sendAck() {
        val p = port ?: return
        executor.submit {
            runCatching { p.write(byteArrayOf(0x06), 500) }
        }
    }

    companion object {
        const val ACTION_USB_PERMISSION = "com.orienteering.startref.USB_PERMISSION"
    }

    /**
     * Attempt to connect. If USB permission hasn't been granted yet, requests it and returns.
     * Call start() again after permission is granted (MainActivity handles the broadcast).
     */
    fun start() {
        if (ioManager != null) return  // Already connected — idempotent

        val usbManager = context.getSystemService(Context.USB_SERVICE) as UsbManager
        val availableDrivers = SiProber.get().findAllDrivers(usbManager)
        if (availableDrivers.isEmpty()) {
            debugLog.log("start(): no drivers found")
            return
        }

        val driver = if (siReaderDeviceKey.isNotEmpty()) {
            availableDrivers.firstOrNull { d ->
                "${d.device.vendorId}:${d.device.productId}" == siReaderDeviceKey
            } ?: availableDrivers.first()
        } else {
            availableDrivers.first()
        }

        if (!usbManager.hasPermission(driver.device)) {
            debugLog.log("No USB permission, requesting for VID=${driver.device.vendorId} PID=${driver.device.productId}")
            requestPermission(usbManager, driver.device)
            return
        }

        val connection = usbManager.openDevice(driver.device) ?: run {
            debugLog.log("openDevice() returned null — permission issue?")
            return
        }

        runCatching {
            val p = driver.ports.first()
            p.open(connection)
            p.setParameters(38400, 8, 1, 0) // 38400 baud, 8N1
            p.dtr = true  // Required to keep CP21xx connection alive
            port = p

            ioManager = SerialInputOutputManager(p, object : SerialInputOutputManager.Listener {
                override fun onNewData(data: ByteArray) {
                    parseData(data)
                }
                override fun onRunError(e: Exception) {
                    debugLog.log("onRunError: ${e.javaClass.simpleName}: ${e.message}")
                    // Don't call stop() directly from inside the callback — it risks
                    // calling ioManager.stop() re-entrantly from its own thread.
                    // Just clear state; the run() loop exits on its own after this callback.
                    ioManager = null
                    buffer.clear()
                    _connectionState.value = SiConnectionState.DISCONNECTED
                }
            }).also {
                it.readTimeout = 2000  // Non-zero timeout avoids infinite blocking reads
            }
            executor.submit(ioManager)
            _connectionState.value = SiConnectionState.CONNECTED
            debugLog.log("Connected: ${driver.device.deviceName} VID=${driver.device.vendorId} PID=${driver.device.productId}")
        }.onFailure {
            debugLog.log("Failed to open port: ${it.javaClass.simpleName}: ${it.message}")
        }
    }

    private fun requestPermission(usbManager: UsbManager, device: UsbDevice) {
        val intent = PendingIntent.getBroadcast(
            context, 0,
            Intent(ACTION_USB_PERMISSION),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
        usbManager.requestPermission(device, intent)
    }

    fun stop() {
        debugLog.log("stop() called")
        ioManager?.listener = null
        ioManager?.stop()
        ioManager = null
        port = null
        buffer.clear()
        _connectionState.value = SiConnectionState.DISCONNECTED
    }

    // Station is in autosend/master mode — it does not accept framed responses.
    // Audio feedback is handled by the app via playBeep().

    /**
     * Parses SportIdent EPS serial frames to extract SI card numbers.
     *
     * Frame format:
     *   [0xFF wakeup (optional)] + 0x02 (STX) + CMD + LEN + DATA[LEN] + CRC_H + CRC_L + 0x03 (ETX)
     *
     * Card autosend command: C_TRANS_REC = 0xD3
     *
     * DATA layout for C_TRANS_REC:
     *   [0..1]  Station code (2 bytes)
     *   [2..5]  SI card number (4 bytes, big-endian)
     *   [6+]    Time info (not used)
     *
     * SI5 special: if raw 4-byte value < 500_000, series = data[2], value = data[3..5] as 3-byte int.
     *   If series < 2: card = value; else card = series * 100_000 + value.
     * SI6/8/9+: card = data[2..5] as full 4-byte big-endian int (value >= 500_000).
     */
    private fun parseData(data: ByteArray) {
        // Log raw hex so we can spot NAK (0x15) or unexpected responses
        debugLog.log("RX: ${data.joinToString(" ") { "%02X".format(it.toInt() and 0xFF) }}")
        buffer.addAll(data.toList())

        val buf = buffer.toMutableList()
        var i = 0
        while (i < buf.size) {
            // Skip optional 0xFF wakeup bytes
            if (buf[i] == 0xFF.toByte()) {
                i++
                continue
            }
            // Look for STX = 0x02
            if (buf[i] != 0x02.toByte()) {
                i++
                continue
            }
            // Need at least STX + CMD + LEN = 3 bytes to read length
            if (i + 2 >= buf.size) break

            val cmd = buf[i + 1]
            val len = buf[i + 2].toInt() and 0xFF
            // Full frame: STX(1) + CMD(1) + LEN(1) + DATA(len) + CRC(2) + ETX(1) = len + 6
            val frameEnd = i + len + 6
            if (buf.size < frameEnd) break  // Incomplete frame — wait for more data

            val cmdInt = cmd.toInt() and 0xFF
            val cmdName = when (cmdInt) {
                0xD3 -> "TRANS_REC"
                0xE5 -> "SI5_IN"
                0xE6 -> "SI6_IN"
                0xE7 -> "SI_OUT"
                0xE8 -> "SI9_IN"
                0xE9 -> "SI_IN"
                0x83 -> "GET_SYS_VAL"
                0xF7 -> "GET_TIME"
                0xF0 -> "GET_MS"
                else -> "0x${cmdInt.toString(16).uppercase().padStart(2, '0')}"
            }
            debugLog.log("Frame $cmdName LEN=$len")

            // Card number is at DATA[2..5] for insert and autosend frames only — not removal (E7)
            val isCardFrame = cmdInt == 0xD3 ||
                              cmdInt == 0xE5 || cmdInt == 0xE6 ||
                              cmdInt == 0xE8 || cmdInt == 0xE9
            if (cmdInt == 0xE7) sendAck()
            if (isCardFrame && len >= 6) {
                val dataStart = i + 3
                // DATA[2] is a type/flag byte — card number is always in DATA[3..5]
                val threeByte = ((buf[dataStart + 3].toLong() and 0xFF) shl 16) or
                                ((buf[dataStart + 4].toLong() and 0xFF) shl 8) or
                                 (buf[dataStart + 5].toLong() and 0xFF)

                val cardNum: Long = if (threeByte < 500_000L) {
                    // SI5: DATA[3]=series, DATA[4..5]=2-byte value within series
                    val series = buf[dataStart + 3].toLong() and 0xFF
                    val value = ((buf[dataStart + 4].toLong() and 0xFF) shl 8) or
                                 (buf[dataStart + 5].toLong() and 0xFF)
                    if (series < 2L) value else series * 100_000L + value
                } else {
                    // SI6/8/9: straight 3-byte number
                    threeByte
                }
                debugLog.log("*** SI chip: $cardNum ***")
                _cardReadEvents.trySend(cardNum.toString())
                playSuccess()
                sendAck()
            }
            i = frameEnd
        }

        buffer.clear()
        if (i < buf.size) buffer.addAll(buf.subList(i, buf.size))
    }
}
