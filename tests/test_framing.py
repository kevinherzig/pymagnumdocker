"""Replays a real RS485 capture (inverter in mode 0x00 / charger off) through SafeMagnum."""
import os
import unittest

from magnum.magnum import Magnum

from app.framing import SafeMagnum, inverter_fields_ok
from app.reader import sanitize, summarize

CAPTURE = os.path.join(os.path.dirname(__file__), "data", "magcap_2026-10-22.log")
BATCH = 50


def capture_chunks():
    """Raw gap-delimited chunks from 'unix_time hex hex ...' lines (imperfect framing, as on the wire)."""
    with open(CAPTURE) as f:
        return [bytearray(bytes.fromhex("".join(p[1:]))) for p in (l.split() for l in f) if len(p) > 1]


def replay(cls):
    chunks = capture_chunks()
    m = cls(device="replay", packets=BATCH)
    m.stored_packets = chunks          # pymagnum's built-in replay path (no serial port)
    snapshots = []
    for _ in range(len(chunks) // BATCH):
        devs = m.getDevices()
        snapshots.append(next((d["data"] for d in devs if d["device"] == "INVERTER"), None))
    return m, snapshots


def sane(inv):
    return (inv["model"] == 0x14 and inv["revision"] == "5.9" and 13.0 <= inv["vdc"] <= 15.5
            and abs(inv["adc"]) < 50 and 0 <= inv["bat"] <= 50 and inv["fault_text"] == "None"
            and inv["mode_text"] in ("Standby", "BATSAVER") and inv["VACin"] > 150)


class FramingTest(unittest.TestCase):
    def test_capture_every_snapshot_sane(self):
        m, snaps = replay(SafeMagnum)
        got = [s for s in snaps if s]
        self.assertGreater(len(got), 0.9 * len(snaps))
        bad = [s for s in got if not sane(s)]
        self.assertEqual(bad, [], f"{len(bad)} garbage inverter snapshots, first: {bad[:1]}")
        self.assertEqual(m.locked, (0x14, 59))
        self.assertGreater(m.accepted_inverter, 1000)
        # charger was left off at the end of the capture => mode 0x00 Standby
        self.assertEqual(got[-1]["mode_text"], "Standby")

    def test_stock_pymagnum_for_reference(self):
        # Documents the upstream behaviour this module works around (not asserted strictly).
        _, snaps = replay(Magnum)
        bad = [s for s in snaps if s and not sane(s)]
        print(f"\n  stock pymagnum: {len(bad)}/{len(snaps)} garbage or stale inverter snapshots")

    def test_split_bmk_tail_never_parsed_as_inverter(self):
        # the 16-byte tail of a gap-split BMK packet that stock pymagnum reads as an
        # old-style inverter: mode 0x64, model 45 (MS2812), BACKFEED, vdc -2150 V
        m = SafeMagnum(device="replay", packets=1)
        msg = m._parsePacket(bytearray.fromhex("6405ac0003040a06e50001b0e0002d0d"))
        self.assertEqual(msg[0], "UNKNOWN")
        m2, _ = replay(SafeMagnum)
        self.assertEqual(m2.packet_counts.get("INVERTER_C", 0), 0)
        self.assertGreater(m2.packet_counts["BMK_81"], 50)

    def test_remote_packet_rejected(self):
        remote = bytes.fromhex("001e1492641e1d0600739b9200ff0630000028 0000".replace(" ", ""))
        self.assertFalse(inverter_fields_ok(remote))
        inv = bytes.fromhex("000000910000 61bc00013b19251f1400000002 5800".replace(" ", ""))
        self.assertTrue(inverter_fields_ok(inv))

    def test_sanitize_nulls_garbage(self):
        s = summarize([{"device": "INVERTER", "data": {"vdc": -2124.8, "adc": 260.0, "bat": 176.0,
                                                       "VACin": 6.0, "VACout": 10.0, "mode_text": "??"}}])
        nulled = sanitize(s)
        self.assertIsNone(s["vdc"]); self.assertIsNone(s["battery_temp"]); self.assertIsNone(s["mode_text"])
        self.assertIn("vdc", nulled)
        s2 = summarize([{"device": "INVERTER", "data": {"adc": 260.0, "VACin": 120.0}}])
        sanitize(s2, inverter_ok=False)
        self.assertIsNone(s2["charge_amps"]); self.assertFalse(s2["shore"])



INV_DATA = {"device": "INVERTER", "data": {"mode_text": "Standby", "vdc": 14.5, "adc": 3.0, "VACin": 120.0,
                                           "AACin": 0.0, "VACout": 120.0, "AACout": 0.0, "bat": 24.0, "fault": 0}}
BMK_DATA = {"device": "BMK", "data": {"vdc": 12.9, "adc": -4.2, "soc": 77}}


class FakeReader:
    """Mimics SafeMagnum's counters so the reader can tell which devices are live."""
    def __init__(self, dev):
        self.bmk = True
        self.packet_counts = {"BMK_81": 0}
        self.accepted_inverter = 0
    def getComm_Device(self): return "/dev/fake"
    def getDevices(self):
        self.accepted_inverter += 1
        if self.bmk:
            self.packet_counts["BMK_81"] += 1
        # like pymagnum, the BMK device object lingers after the BMK goes away
        return [INV_DATA, BMK_DATA]


class BatterySourceTest(unittest.TestCase):
    def make(self, bmk):
        from app.config import Config
        from app.reader import Reader
        holder = {}
        def factory(dev):
            holder["r"] = FakeReader(dev); holder["r"].bmk = bmk; return holder["r"]
        rd = Reader(Config(history_interval=60), magnum_factory=factory)
        rd.bmk_timeout = 0.2
        rd.open()
        return rd, holder["r"]

    def test_bmk_preferred_then_falls_back_when_it_disappears(self):
        rd, fake = self.make(bmk=True)
        rd.poll_once()
        s = rd.state()["summary"]
        self.assertEqual(s["battery_source"], "bmk")
        self.assertEqual((s["vdc"], s["battery_amps"], s["soc"]), (12.9, -4.2, 77))
        fake.bmk = False            # BMK unplugged mid-run
        import time; time.sleep(0.3)
        rd.poll_once()
        s = rd.state()["summary"]
        self.assertEqual(s["battery_source"], "inverter")
        self.assertEqual((s["vdc"], s["battery_amps"], s["soc"]), (14.5, 3.0, None))

    def test_no_bmk_uses_inverter(self):
        s = summarize([INV_DATA])
        self.assertEqual((s["battery_source"], s["vdc"], s["soc"]), ("inverter", 14.5, None))
        rd, _ = self.make(bmk=False)
        rd._bmk_last = 0.0
        rd.poll_once()
        self.assertEqual(rd.state()["summary"]["battery_source"], "inverter")


if __name__ == "__main__":
    unittest.main()
