# Runtime-only image. The web UI is prebuilt into web/dist (no Node needed on the Pi).
FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 PIP_NO_CACHE_DIR=1 PIP_DISABLE_PIP_VERSION_CHECK=1

RUN pip install pymagnum==2.0.* \
 && useradd --system --no-create-home --groups dialout magnum

WORKDIR /app
COPY app/ app/
COPY web/dist/ web/dist/

USER magnum
EXPOSE 17223
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD ["python", "-c", "import urllib.request,sys; sys.exit(0 if urllib.request.urlopen('http://127.0.0.1:17223/healthz', timeout=4).status == 200 else 1)"]
CMD ["python", "-m", "app"]
