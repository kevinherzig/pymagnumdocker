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


def summarize(devices):
    """Flatten pymagnum device dicts into the few numbers the dashboard plots."""
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
    return {
        "vdc": num(bmk, "vdc") if bmk else num(inv, "vdc"),
        "soc": num(bmk, "soc"),
        "battery_amps": num(bmk, "adc"),
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
        "invled_text": inv.get("invled_text") or None,
        "chgled_text": inv.get("chgled_text") or None,
        "ags_status": ags.get("status_text") if ags else None,
        "ags_running": ags.get("running") if ags else None,
        "pv_volts": num(pt, "pv_voltage"),
        "pt100_mode": pt.get("mode_text") if pt else None,
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

    def _default_factory(self, device):
        from magnum.magnum import Magnum
        c = self.config
        return Magnum(device=device, packets=c.packets, trace=c.trace,
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
        with self._lock:
            self._error = err
            if snapshot:
                self._snapshot = snapshot
                self._summary = summarize(all_devices)
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
