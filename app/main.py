"""Entry point: python -m app"""
import logging
import signal
import sys

from .api import make_server
from .config import parse_args
from .reader import Reader


def main(argv=None):
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
    cfg = parse_args(argv)
    logging.info("Options: %s", cfg)
    reader = Reader(cfg)
    if reader.open() == 0:
        logging.error("No usable Magnum devices connected.")
        return 2
    reader.start()
    server = make_server(reader, cfg)

    def shutdown(*_):
        logging.info("Shutting down.")
        reader.stop()
        raise SystemExit(0)

    signal.signal(signal.SIGTERM, shutdown)
    signal.signal(signal.SIGINT, shutdown)
    logging.info("Listening on %s:%d", cfg.listen or "*", cfg.port)
    try:
        server.serve_forever()
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
