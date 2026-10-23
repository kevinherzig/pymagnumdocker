"""Configuration from CLI flags with environment-variable fallbacks."""
import argparse
import os
from dataclasses import dataclass, field


@dataclass
class Config:
    devices: list = field(default_factory=lambda: ["/dev/ttyUSB0"])
    listen: str = ""
    port: int = 17223
    interval: float = 2.0          # seconds between serial polls
    history_interval: float = 60.0  # seconds between history samples
    history_hours: float = 24.0
    packets: int = 50
    timeout: float = 0.005
    trace: bool = False
    cleanpackets: bool = True
    static_dir: str = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "web", "dist")

    @property
    def history_size(self) -> int:
        return max(1, int(self.history_hours * 3600 / max(self.history_interval, 1.0)))


def _env(name, default):
    return os.environ.get("MAGNUM_" + name, default)


def parse_args(argv=None) -> Config:
    p = argparse.ArgumentParser(description="Magnum Energy dashboard server")
    p.add_argument("-d", "--device", action="append", help="Serial device (repeatable). Env MAGNUM_DEVICES, comma-separated")
    p.add_argument("-l", "--listen", default=_env("LISTEN", "ALL"), help="Listen address, ALL for every interface")
    p.add_argument("-p", "--port", type=int, default=int(_env("PORT", 17223)))
    p.add_argument("--interval", type=float, default=float(_env("INTERVAL", 2.0)), help="Serial poll interval (s)")
    p.add_argument("--history-interval", type=float, default=float(_env("HISTORY_INTERVAL", 60.0)))
    p.add_argument("--history-hours", type=float, default=float(_env("HISTORY_HOURS", 24.0)))
    p.add_argument("--packets", type=int, default=int(_env("PACKETS", 50)))
    p.add_argument("--timeout", type=float, default=float(_env("TIMEOUT", 0.005)))
    p.add_argument("--trace", action="store_true", default=_env("TRACE", "") == "1")
    p.add_argument("--nocleanup", action="store_true", default=False)
    a = p.parse_args(argv)
    devices = a.device or [d.strip() for d in _env("DEVICES", "/dev/ttyUSB0").split(",") if d.strip()]
    return Config(
        devices=devices,
        listen="" if a.listen.upper() == "ALL" else a.listen,
        port=a.port,
        interval=a.interval,
        history_interval=a.history_interval,
        history_hours=a.history_hours,
        packets=a.packets,
        timeout=a.timeout,
        trace=a.trace,
        cleanpackets=not a.nocleanup,
    )
