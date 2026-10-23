"""Hardened packet classification on top of pymagnum.

pymagnum tells inverter packets apart from remote packets with a heuristic
that breaks when the inverter's mode byte is 0x00 (Standby / charger off):
both packets then start and end with 0x00, and an operator-precedence bug in
its revision/model check (`version == (rev and model == m)`) makes the
result depend on start-up order. Real inverter packets get dropped as
REMOTE_00, and merged/misframed bytes get parsed as INV, which produces
garbage. The live symptom (mode 0x64, model 45, BACKFEED, vdc -2124 V) is
a BMK packet (81 64 05 ac ...) split by a gap: its 16-byte tail looks like
an old-style 16-byte inverter packet (INV_C).

SafeMagnum validates every 21-byte candidate against the inverter packet
layout (known mode, fault and model codes, sane voltages) and locks onto the
model/firmware revision once seen consistently. Only validated packets reach
pymagnum's InverterDevice; anything else is never parsed as INV.
"""
import time
from collections import Counter, deque
from struct import unpack

from magnum import INV, INV_C, UNKNOWN
from magnum.inverterdevice import InverterDevice
from magnum.magnum import Magnum

INV_LEN = 21
TRAILERS = (0xFF, 0xFE)   # observed padding byte after the 21-byte inverter packet
LOCK_AFTER = 3            # consecutive consistent packets before locking model/revision


def inverter_fields_ok(p: bytes) -> bool:
    """Structural sanity check of a 21-byte inverter packet (no history needed)."""
    if len(p) != INV_LEN:
        return False
    mode, fault = p[0], p[1]
    vdc = int.from_bytes(p[2:4], "big", signed=True)        # 0.1 V
    adc = int.from_bytes(p[4:6], "big", signed=True)        # A
    vac_out, vac_in = p[6], p[7]
    revision, batt_temp, model = p[10], p[11], p[14]
    return (
        mode in InverterDevice.modes
        and fault in InverterDevice.faults
        and model in InverterDevice.inverter_models
        and 80 <= vdc <= 700            # 8-70 V covers 12/24/48 V systems
        and -600 <= adc <= 600
        and vac_out <= 160 and vac_in <= 160 * 2  # allow 240 V split-phase inputs
        and 10 <= revision <= 99        # firmware 1.0-9.9
        and batt_temp <= 120
    )


LED_WINDOW = 2.0   # s; the charger/inverter LEDs blink at ~0.9 s period


class LedTracker:
    """Per-frame LED history so a blinking LED reads 'Blinking' instead of flipping On/Off
    between 2 s polls (the inverter reports the instantaneous LED level in bytes 8/9)."""

    def __init__(self, window=LED_WINDOW):
        self.window = window
        self.frames = deque()

    def add(self, t, inv, chg):
        self.frames.append((t, inv, chg))
        while self.frames and t - self.frames[0][0] > self.window:
            self.frames.popleft()

    def state(self, idx):
        """idx 1 = inverter LED, 2 = charger LED -> 'On' | 'Off' | 'Blinking' | None."""
        vals = {f[idx] for f in self.frames}
        if not vals:
            return None
        if len(vals) > 1:
            return "Blinking"
        return "On" if vals.pop() else "Off"


class SafeMagnum(Magnum):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.locked = None        # (model, revision) once learned
        self._candidate = None
        self._streak = 0
        self.rejected_inverter = 0
        self.accepted_inverter = 0
        self.clock = time.monotonic
        self.leds = LedTracker()
        self.packet_counts = Counter()   # packet type -> count (lets the reader see which devices are live)

    # -- inverter identification -------------------------------------------
    def _is_inverter(self, p: bytes) -> bool:
        if not inverter_fields_ok(p):
            return False
        ident = (p[14], p[10])
        if self.locked is not None:
            return ident == self.locked
        if ident == self._candidate:
            self._streak += 1
        else:
            self._candidate, self._streak = ident, 1
        if self._streak >= LOCK_AFTER:
            self.locked = ident
        return True

    def _inv_message(self, p: bytes):
        fmt = self.unpackFormats[INV]
        self.accepted_inverter += 1
        self.leds.add(self.clock(), p[8] != 0, p[9] != 0)
        return [INV, bytearray(p), unpack(">" + fmt, p), fmt]

    def _parsePacket(self, packet):
        raw = bytes(packet)
        if self.flip:
            raw = bytes((~b) & 0xFF for b in raw)
        if len(raw) in (INV_LEN, INV_LEN + 1):
            core = raw[:INV_LEN]
            if (len(raw) == INV_LEN or raw[-1] in TRAILERS) and self._is_inverter(core):
                return self._inv_message(core)
        msg = super()._parsePacket(packet)
        if msg[0] in (INV, INV_C):
            # pymagnum guessed INV/INV_C but it failed validation (21-byte inverters are
            # validated above; old 16-byte INV_C models are not supported by this app): never feed it to the inverter
            self.rejected_inverter += 1
            return [UNKNOWN, packet, {}, ""]
        return msg

    def getPackets(self):
        msgs = super().getPackets()
        self.packet_counts.update(m[0] for m in msgs)
        return msgs

    # -- re-framing ------------------------------------------------------------
    def readPackets(self):
        """Split chunks where the inverter packet and the following packet(s)
        arrived without a detectable gap (inverter packet is first in the cycle)."""
        out = []
        for chunk in super().readPackets():
            out.extend(self._split(bytes(chunk)))
        return out

    def _split(self, chunk: bytes):
        if len(chunk) > INV_LEN + 1 and inverter_fields_ok(chunk[:INV_LEN]):
            cut = INV_LEN + (1 if chunk[INV_LEN] in TRAILERS else 0)
            return [bytearray(chunk[:cut])] + self._split_rest(chunk[cut:])
        return [bytearray(chunk)]

    @staticmethod
    def _split_rest(rest: bytes):
        # remote packet (21 bytes + optional trailer) normally follows the inverter
        if len(rest) > INV_LEN + 1:
            cut = INV_LEN + (1 if rest[INV_LEN] in TRAILERS else 0)
            return [bytearray(rest[:cut]), bytearray(rest[cut:])]
        return [bytearray(rest)] if rest else []
