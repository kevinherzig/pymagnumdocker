"""Smoke tests with a fake pymagnum reader (no serial hardware needed)."""
import json
import threading
import time
import unittest
import urllib.request
from http.client import HTTPConnection

from app.api import make_server
from app.config import parse_args
from app.reader import Reader, summarize

DEVICES = [
    {"device": "INVERTER", "data": {"mode_text": "INVERT", "invled_text": "On", "chgled_text": "Off", "fault": 0,
                                    "fault_text": "", "vdc": 12.6, "adc": -20.0, "VACout": 120.0, "AACout": 5.0,
                                    "VACin": 0.0, "AACin": 0.0, "bat": 22.0, "Hz": 60.0}},
    {"device": "BMK", "data": {"vdc": 12.7, "soc": 88, "adc": -21.5}},
]


class FakeMagnum:
    def __init__(self, dev): self.dev = dev
    def getDevices(self): return [dict(d, data=dict(d["data"])) for d in DEVICES]
    def getComm_Device(self): return self.dev


class SmokeTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cfg = parse_args(["--port", "0", "--interval", "0.1", "--history-interval", "0"])
        cls.reader = Reader(cfg, magnum_factory=FakeMagnum)
        assert cls.reader.open() == 1
        cls.reader.poll_once()
        cls.reader.start()
        cls.srv = make_server(cls.reader, cfg)
        cls.port = cls.srv.server_address[1]
        threading.Thread(target=cls.srv.serve_forever, daemon=True).start()

    @classmethod
    def tearDownClass(cls):
        cls.reader.stop(); cls.srv.shutdown()

    def get(self, path):
        with urllib.request.urlopen(f"http://127.0.0.1:{self.port}{path}", timeout=3) as r:
            return r.status, r.read()

    def test_summary(self):
        s = summarize(DEVICES)
        self.assertEqual(s["ac_out_w"], 600)
        self.assertEqual(s["vdc"], 12.7)
        self.assertEqual(s["soc"], 88)
        self.assertFalse(s["shore"])

    def test_legacy_api_shape(self):
        st, body = self.get("/api")
        data = json.loads(body)
        self.assertEqual(st, 200)
        self.assertEqual(data[0]["comm_device"], "/dev/ttyUSB0")
        self.assertEqual(data[0]["data"]["devices"][0]["device"], "INVERTER")

    def test_state_history_health(self):
        self.assertEqual(json.loads(self.get("/api/state")[1])["summary"]["soc"], 88)
        h = json.loads(self.get("/api/history")[1])
        self.assertGreaterEqual(len(h["rows"]), 1)
        self.assertEqual(self.get("/healthz")[0], 200)

    def test_static_and_traversal(self):
        st, body = self.get("/")
        self.assertIn(b"Magnum", body)
        c = HTTPConnection("127.0.0.1", self.port, timeout=3)
        c.request("GET", "/../app/config.py")
        self.assertEqual(c.getresponse().status, 404)

    def test_sse(self):
        c = HTTPConnection("127.0.0.1", self.port, timeout=3)
        c.request("GET", "/api/stream")
        r = c.getresponse()
        self.assertEqual(r.getheader("Content-Type"), "text/event-stream")
        line = r.fp.readline()
        self.assertTrue(line.startswith(b"data: "))
        c.close()


if __name__ == "__main__":
    unittest.main()
