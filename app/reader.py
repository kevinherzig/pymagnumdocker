"""Single background thread that owns the serial port(s).

HTTP handlers never touch the serial port: they read the cached snapshot,
so any number of browsers costs no extra serial I/O.
"""
import logging
import threading
import time
from collections import deque
from datetime import datetime, timezone

log = logging.getLogger(__name__)


def _now_iso():
    return datetime.now(timezone.utc).replace(microsecond=0).astimezone().isoformat()


def _find(devices, kind):
    for d in devices:
        if d.get("device") == kind:
            return d.get("data") or {}
    return None


# Plausible ranges; anything outside is published as null and listed in quality.nulled
LIMITS = {
    "vdc": (8, 70), "soc": (0, 100), "battery_amps": (-600, 600), "charge_amps": (-600, 600),
    "battery_temp": (-40, 120), "ac_out_w": (0, 10000), "ac_in_w": (0, 15000),
    "vac_in": (0, 300), "vac_out": (0, 300), "hz": (0, 70), "pv_volts": (0, 300),
}
# Charger state from the inverter mode byte (Magnum protocol / pymagnum InverterDevice.modes).
# The charger LED (byte 9) blinks in standby, so it is not used for state.
CHARGER_STATES = {
    0x00: "Standby", 0x01: "Equalize", 0x02: "Float", 0x04: "Absorb", 0x08: "Bulk",
    0x09: "Battery Saver", 0x10: "Charging", 0x20: "Off", 0x40: "Off (Inverting)",
    0x50: "Off (Inverter Standby)", 0x80: "Off (Search)",
}
CHARGING_MODES = {0x01, 0x02, 0x04, 0x08, 0x10}

INVERTER_FIELDS = ("charger_state", "charging", "charge_amps", "battery_temp", "ac_out_w", "ac_in_w", "vac_in", "vac_out", "hz",
                   "mode_text", "fault_text", "invled_text", "chgled_text")


def sanitize(summary, inverter_ok=True):
    """Null out-of-range values; drop inverter fields when its data isn't trustworthy."""
    nulled = []
    for k, (lo, hi) in LIMITS.items():
        v = summary.get(k)
        if v is not None and not lo <= v <= hi:
            summary[k] = None
            nulled.append(k)
    if not inverter_ok:
        for k in INVERTER_FIELDS:
            summary[k] = None
        summary["fault"] = 0
        summary["shore"] = False
        if summary.get("battery_source") == "inverter":
            summary["vdc"] = summary["battery_amps"] = None
    if summary.get("mode_text") == "??":
        summary["mode_text"] = None
        nulled.append("mode_text")
    return nulled


def summarize(devices, bmk_present=None):
    """Flatten pymagnum device dicts into the few numbers the dashboard plots.

    Battery voltage/amps/SOC come from the BMK (battery monitor) when one is live
    on the bus, otherwise from the inverter (no SOC). bmk_present=None means
    "BMK present if a BMK device is in the list"."""
    inv = _find(devices, "INVERTER") or {}
    bmk = _find(devices, "BMK")
    ags = _find(devices, "AGS")
    pt = _find(devices, "PT100")

    def num(d, k):
        v = (d or {}).get(k)
        return float(v) if isinstance(v, (int, float)) else None

    def watts(v, a):
        v, a = num(inv, v), num(inv, a)
        return round(v * a) if v is not None and a is not None else None

    vac_in = num(inv, "VACin")
    use_bmk = bool(bmk) if bmk_present is None else (bmk_present and bool(bmk))
    src = bmk if use_bmk else inv
    return {
        "battery_source": "bmk" if use_bmk else ("inverter" if inv else None),
        "vdc": num(src, "vdc"),
        "soc": num(bmk, "soc") if use_bmk else None,
        "battery_amps": num(src, "adc"),
        "charge_amps": num(inv, "adc"),
        "battery_temp": num(inv, "bat"),
        "ac_out_w": watts("VACout", "AACout"),
        "ac_in_w": watts("VACin", "AACin"),
        "vac_in": vac_in,
        "vac_out": num(inv, "VACout"),
        "hz": num(inv, "Hz"),
        "shore": bool(vac_in and vac_in > 80),
        "mode_text": inv.get("mode_text") or None,
        "fault": inv.get("fault") or 0,
        "fault_text": inv.get("fault_text") or None,
        "charger_state": CHARGER_STATES.get(inv.get("mode")) if inv else None,
        "charging": (inv.get("mode") in CHARGING_MODES) if inv else None,
        "invled_text": inv.get("invled_text") or None,
        "chgled_text": inv.get("chgled_text") or None,
        "ags_status": ags.get("status_text") if ags else None,
        "ags_running": ags.get("running") if ags else None,
        "pv_volts": num(pt, "pv_voltage"),
        "pt100_mode": pt.get("mode_text") if pt else None,
        "bmk_fault_text": (bmk.get("Fault_Text") or None) if bmk else None,
        "identity": identity(devices),
    }


def _nominal_volts(model):
    # pymagnum InverterDevice: model <= 50 -> 12 V, <= 107 -> 24 V, <= 150 -> 48 V
    if not isinstance(model, int) or model <= 0:
        return None
    return 12 if model <= 50 else 24 if model <= 107 else 48 if model <= 150 else None


def identity(devices):
    """Static-ish identity of the system for the page header (model, firmware, accessories)."""
    inv = _find(devices, "INVERTER") or {}
    rev = lambda d: (str(d.get("revision")) if d and d.get("revision") not in (None, "", "0.0") else None)  # noqa: E731
    model_text = inv.get("model_text") or None
    return {
        "model": inv.get("model") or None,
        "model_text": model_text if model_text != "Unknown" else None,
        "firmware": rev(inv),
        "nominal_volts": _nominal_volts(inv.get("model")),
        "stack_mode": inv.get("stackmode_text") or None,
        "accessories": {k.lower(): rev(_find(devices, k)) or "present"
                        for k in ("REMOTE", "BMK", "AGS", "PT100", "RTR", "ACLD") if _find(devices, k) is not None},
    }


class Reader:
    def __init__(self, config, magnum_factory=None):
        self.config = config
        self._factory = magnum_factory or self._default_factory
        self._readers = {}
        self._lock = threading.Lock()
        self._changed = threading.Condition(self._lock)
        self._snapshot = None   # list in legacy /api shape
        self._summary = None
        self._updated = 0.0
        self._error = None
        self._seq = 0
        self.history = deque(maxlen=config.history_size)
        self._last_hist = 0.0
        self._stop = threading.Event()
        self._inv_seen = {}       # comm -> accepted inverter packet count
        self._inv_last_ok = 0.0
        self.inverter_stale_after = 30.0
        self._bmk_seen = {}
        self._bmk_last = 0.0
        self.bmk_timeout = 10.0

    def _default_factory(self, device):
        from .framing import SafeMagnum
        c = self.config
        return SafeMagnum(device=device, packets=c.packets, trace=c.trace,
                      timeout=c.timeout, cleanpackets=c.cleanpackets)

    def open(self):
        for dev in self.config.devices:
            try:
                r = self._factory(dev)
                r.getDevices()  # test read
                self._readers[r.getComm_Device()] = r
            except Exception as e:  # noqa: BLE001
                log.error("%s: %s", dev, e)
        return len(self._readers)

    def poll_once(self):
        snapshot, all_devices, err = [], [], None
        ts = _now_iso()
        for comm, r in self._readers.items():
            try:
                devs = r.getDevices()
            except Exception as e:  # noqa: BLE001
                err = f"{comm}: {e}"
                continue
            if devs:
                snapshot.append({"comm_device": comm, "data": {"datetime": ts, "devices": devs}})
                all_devices.extend(devs)
        now = time.time()
        rejected = 0
        for comm, r in self._readers.items():
            acc = getattr(r, "accepted_inverter", None)
            if acc is None:              # plain pymagnum reader (tests/legacy): trust it
                self._inv_last_ok = now
            elif acc != self._inv_seen.get(comm):
                self._inv_seen[comm] = acc
                self._inv_last_ok = now
            rejected += getattr(r, "rejected_inverter", 0)
            counts = getattr(r, "packet_counts", None)
            if counts is None:           # plain reader: BMK present if pymagnum reports it
                if any(d.get("device") == "BMK" for d in all_devices):
                    self._bmk_last = now
            elif counts.get("BMK_81", 0) != self._bmk_seen.get(comm, 0):
                self._bmk_seen[comm] = counts.get("BMK_81", 0)
                self._bmk_last = now
        bmk_present = (now - self._bmk_last) <= self.bmk_timeout
        inverter_ok = (now - self._inv_last_ok) <= self.inverter_stale_after
        with self._lock:
            self._error = err
            if snapshot:
                self._snapshot = snapshot
                summary = summarize(all_devices, bmk_present)
                summary["comm_device"] = ", ".join(c for c in self._readers if any(x["comm_device"] == c for x in snapshot)) or None
                for r in self._readers.values():
                    leds = getattr(r, "leds", None)
                    if leds is not None and leds.frames:
                        summary["invled_text"] = leds.state(1)
                        summary["chgled_text"] = leds.state(2)
                nulled = sanitize(summary, inverter_ok)
                summary["quality"] = {
                    "ok": inverter_ok and not nulled,
                    "inverter": "ok" if inverter_ok else "stale",
                    "inverter_age": round(now - self._inv_last_ok, 1) if self._inv_last_ok else None,
                    "nulled": nulled,
                    "rejected_inverter_packets": rejected,
                    "bmk_age": round(now - self._bmk_last, 1) if self._bmk_last else None,
                }
                self._summary = summary
                self._updated = now
                if now - self._last_hist >= self.config.history_interval:
                    s = self._summary
                    self.history.append([int(now), s["vdc"], s["soc"], s["ac_out_w"], s["ac_in_w"], s["charge_amps"], s["battery_temp"]])
                    self._last_hist = now
            self._seq += 1
            self._changed.notify_all()

    def run(self):
        while not self._stop.is_set():
            start = time.monotonic()
            self.poll_once()
            self._stop.wait(max(0.0, self.config.interval - (time.monotonic() - start)))

    def start(self):
        t = threading.Thread(target=self.run, name="magnum-reader", daemon=True)
        t.start()
        return t

    def stop(self):
        self._stop.set()
        with self._lock:
            self._changed.notify_all()

    # ---- read side (used by HTTP handlers) ----
    def legacy(self):
        with self._lock:
            return self._snapshot, self._error

    def state(self):
        with self._lock:
            return {
                "updated": self._updated or None,
                "age": round(time.time() - self._updated, 1) if self._updated else None,
                "error": self._error,
                "summary": self._summary,
                "seq": self._seq,
            }

    def history_payload(self):
        with self._lock:
            return {
                "fields": ["t", "vdc", "soc", "ac_out_w", "ac_in_w", "charge_amps", "battery_temp"],
                "rows": list(self.history),
            }

    def wait_for_update(self, last_seq, timeout):
        with self._lock:
            self._changed.wait_for(lambda: self._seq != last_seq or self._stop.is_set(), timeout=timeout)
            return self._seq
