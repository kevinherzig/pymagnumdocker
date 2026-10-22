# Pymagnum Hardware & Docker Image

I liked the work that Charles Godwin did in creating a Python based platform for reading data from the Magnum Energy series of inverters.  After spending a good amount of time setting it up and using it, I wanted to contribute back to the community.

I created a docker image tested on simple, cheap hardware.  I'll highlight the hardware and provide set up instructions.

This is an advanced undertaking.  I'm not going to get into details about how to install the OS, set it all up, use SSH to install things, and configure your router for access. 

Here is a live example.  This link will bring you to my magnum running in my RV.

[Live Example](https://magnum.herzig.net/)

**HARDWARE**

This image runs great on a Raspberry PI 2W.  
[link](https://www.amazon.com/dp/B09LH5SBPS?ref_=ppx_hzsearch_conn_dt_b_fed_asin_title_2)
![enter image description here](https://m.media-amazon.com/images/I/61rVLsPzmeL._AC_SX679_.jpg)

An SD Card to boot the system
[SD Card](https://www.amazon.com/Amazon-Basics-Reader-Adapter-1-Pack/dp/B0DB5BW783/ref=sr_1_14?dib=eyJ2IjoiMSJ9.WYqHlY2vJkPfbe6PrvA33GYloKYpVJQl7fYgHWrk3d03rjBdDwnitNFlFOZ-H3QLR4IuWv3Gn6I-JEmdy4fZjaS1vH5d5oQyFgy4lk3cPKr75N6eUPU76i6jTLBDbJuP_CJV9zVZgeaKl4JxqTk2R5PhB_JjTIxAEMjovVcaAlfSd3TesYV1-YGn5phF_nBv0uEtl635Y0owernnpheLTCO_rx6k4mzm0tlX49JHt74.fzw9mNhci5qddmFUd51ABp-8QBsfI835uV5ac3Rk3HY&dib_tag=se&keywords=sd%2bcard&qid=1767103560&sr=8-14&th=1)

Next you'll need a RS485 Interface.
https://www.amazon.com/dp/B0DR218X7P?ref_=ppx_hzsearch_conn_dt_b_fed_asin_title_2

![enter image description here](https://m.media-amazon.com/images/I/610CxeyhJdL._AC_SY300_SX300_QL70_FMwebp_.jpg)


Finally you'll need a way to plug in regular USB-A devices (the above RS485 interface) into the Raspberry PI 2W.
https://www.amazon.com/dp/B083WML1XB?ref_=ppx_hzsearch_conn_dt_b_fed_asin_title_1&th=1
![enter image description here](https://m.media-amazon.com/images/I/61maT4U8lSL._AC_SX679_.jpg)

Optinally a HDMI cable in case you need to debug what's going on with the OS
[link](https://www.amazon.com/UGREEN-Adapter-Ethernet-Compatible-Raspberry/dp/B06WW3LPWL/ref=sr_1_5?crid=3L490GV5BXJS9&dib=eyJ2IjoiMSJ9.iSNSg-dq1X6kRDNJ3GAACg9hweY8PCjgVpCmNt7CmfRtDTPIvne2cyjg96vRFJL16pPnq8Qnq-a6m9KcbW3wyd9r-0gg2_VByr37iSnRVGcMbw-SaGPdzB4HNhVLKleQfnSHyE_y_9Fqks_iMZwbqsSBSb0XGTE_2SGJCw19IRDJV0tOy_sqPh7MWaHAXqOBuRSdP0FSNPrgsjc-qxoXLnOCCDeBZPwQmC5Hygy0tkw.7xf68af2SgTGhVT9Vg2DPR0hiAciYP5sRaoLKaLDGn0&dib_tag=se&keywords=raspberry%2bpi%2b2w%2bhdmi%2bcable&qid=1767105269&sprefix=raspberry%2bpi%2b2w%2bhdmi%2bcabl,aps,151&sr=8-5&th=1)

A RJ-11 Splitter
https://www.amazon.com/dp/B0CG97KWV6?ref_=ppx_hzsearch_conn_dt_b_fed_asin_title_7&th=1
![enter image description here](https://m.media-amazon.com/images/I/51tR7kVIW+L._SX522_.jpg)

And some RJ11 telephone cable
https://www.amazon.com/dp/B08L8MQWFL?ref_=ppx_hzsearch_conn_dt_b_fed_asin_title_8&th=1

**Raspberry PI Setup**

Follow the instructions from the Raspberry PI web page to flash the SD card.  I recommend installing the Raspberry PI Lite version of the O/S since the 2W is very memory limited an and a desktop will add a lot of memory and CPU overhead.

I set up the Wifi access point and Raspberry PI password through the installer.  That way I can boot it, look at my router (or the boot up screen of the PI) to find the IP address, and SSH into it to install the rest.

[link](https://www.raspberrypi.com/documentation/computers/getting-started.html)

## Magnum Docker setup

Once you're logged into the Pi:

```sh
sudo apt install docker.io docker-compose-plugin   # or Docker's convenience script
sudo usermod -aG docker $USER                      # log out/in afterwards
git clone https://github.com/kevinherzig/pymagnumdocker
cd pymagnumdocker
docker compose up -d --build
```

Open `http://<pi-ip>:17223/`. The image is pure Python (no compiler, no Node), so it builds in a minute or two on the Zero 2 W.

### Configuration (environment variables in `compose.yml`)

| Variable | Default | Meaning |
|---|---|---|
| `MAGNUM_DEVICES` | `/dev/ttyUSB0` | Serial device(s), comma-separated (also map them under `devices:`) |
| `MAGNUM_PORT` | `17223` | HTTP port |
| `MAGNUM_INTERVAL` | `2` | Seconds between serial polls |
| `MAGNUM_HISTORY_INTERVAL` | `60` | Seconds between history samples |
| `MAGNUM_HISTORY_HOURS` | `24` | History kept in memory (about 1,440 rows by default) |
| `MAGNUM_PACKETS`, `MAGNUM_TIMEOUT` | `50`, `0.005` | pymagnum reader tuning |

### HTTP endpoints

- `/`: dashboard (gauges, status tiles, 24 h charts, stale indicator)
- `/api`: the original pymagnum REST JSON (`[{comm_device, data:{datetime, devices}}]`), unchanged for existing clients
- `/api/state`: flattened summary, plus `age` (seconds since the last good read) and `error`
- `/api/history`: in-memory ring buffer `{fields, rows}`
- `/api/stream`: Server-Sent Events push of `/api/state` after every poll
- `/healthz`: 200 if data is less than 60 s old (used by the Docker healthcheck)

## How it works

```
app/
  config.py   CLI flags / MAGNUM_* env vars
  reader.py   one background thread owns the serial port; caches the latest snapshot + 24 h history
  api.py      stdlib HTTP server: static UI, JSON API, SSE
  main.py     wiring + clean SIGTERM shutdown
web/          TypeScript + Vite front end (no framework, no runtime deps)
  src/gauge.ts  tiny SVG gauge    src/chart.ts  tiny canvas chart
  dist/         prebuilt output, committed so the Pi never needs Node
tests/        smoke tests with a fake reader (python -m unittest)
```

HTTP requests never touch the serial port, so any number of open dashboards costs no extra RS485 traffic. All assets are served locally, so the dashboard works offline in the RV.

## Development

```sh
cd web && npm ci && npm run dev       # Vite dev server, proxies /api to localhost:17223
npm run build                         # type-check + write web/dist (commit the result)
python -m unittest discover tests     # needs: pip install pymagnum
```
