"""HTTP layer: static files, JSON API and a Server-Sent Events stream."""
import json
import mimetypes
import os
import socket
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit

SSE_KEEPALIVE = 15.0


def make_handler(reader, static_dir):
    static_root = os.path.realpath(static_dir)

    class Handler(BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"
        server_version = "MagnumDash/2"

        def log_request(self, code="-", size="-"):
            code = getattr(code, "value", code)
            if isinstance(code, int) and not 200 <= code < 400:
                super().log_request(code, size)

        def handle_one_request(self):
            try:
                super().handle_one_request()
            except (ConnectionResetError, BrokenPipeError, socket.timeout):
                self.close_connection = True

        def _send(self, body: bytes, ctype: str, status=HTTPStatus.OK, cache="no-store"):
            self.send_response(status)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", cache)
            self.end_headers()
            if self.command != "HEAD":
                self.wfile.write(body)

        def _json(self, obj, status=HTTPStatus.OK):
            self._send(json.dumps(obj, separators=(",", ":")).encode(), "application/json", status)

        def do_HEAD(self):
            self.do_GET()

        def do_GET(self):
            path = urlsplit(self.path).path
            if path == "/api":
                return self._legacy()
            if path == "/api/state":
                return self._json(reader.state())
            if path == "/api/history":
                return self._json(reader.history_payload())
            if path == "/api/stream":
                return self._stream()
            if path == "/healthz":
                st = reader.state()
                ok = st["age"] is not None and st["age"] < 60
                return self._json({"ok": ok, "age": st["age"]}, HTTPStatus.OK if ok else HTTPStatus.SERVICE_UNAVAILABLE)
            return self._static(path)

        def _legacy(self):
            """Original pymagnum REST shape: [{comm_device, data:{datetime, devices}}]."""
            snap, err = reader.legacy()
            if snap:
                return self._json(snap)
            if err:
                return self.send_error(HTTPStatus.INTERNAL_SERVER_ERROR, err)
            self.send_response(HTTPStatus.NO_CONTENT)
            self.send_header("Content-Length", "0")
            self.end_headers()

        def _stream(self):
            self.send_response(HTTPStatus.OK)
            self.send_header("Content-Type", "text/event-stream")
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Accel-Buffering", "no")
            self.end_headers()
            self.close_connection = True
            seq = None
            try:
                while True:
                    new = reader.wait_for_update(seq, SSE_KEEPALIVE)
                    if new == seq:
                        self.wfile.write(b": keepalive\n\n")
                    else:
                        seq = new
                        data = json.dumps(reader.state(), separators=(",", ":"))
                        self.wfile.write(f"data: {data}\n\n".encode())
                    self.wfile.flush()
            except (BrokenPipeError, ConnectionResetError, socket.timeout, OSError):
                return

        def _static(self, path):
            rel = "index.html" if path in ("", "/") else path.lstrip("/")
            full = os.path.realpath(os.path.join(static_root, rel))
            if not full.startswith(static_root + os.sep) or not os.path.isfile(full):
                return self.send_error(HTTPStatus.NOT_FOUND, "Not found")
            ctype = mimetypes.guess_type(full)[0] or "application/octet-stream"
            if ctype.startswith("text/") or ctype.endswith("javascript"):
                ctype += "; charset=utf-8"
            cache = "public, max-age=31536000, immutable" if "/assets/" in full else "no-cache"
            with open(full, "rb") as f:
                self._send(f.read(), ctype, cache=cache)

    return Handler


def make_server(reader, config):
    srv = ThreadingHTTPServer((config.listen, config.port), make_handler(reader, config.static_dir))
    srv.daemon_threads = True
    return srv
