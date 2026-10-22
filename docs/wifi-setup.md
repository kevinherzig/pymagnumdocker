# Wi-Fi setup with balena wifi-connect

The Pi moves between campgrounds, so `compose.yml` includes a `wifi-connect` service. If the Pi can't get online, it opens a captive-portal access point called **Magnum-Setup** where you can choose a new network from your phone.

## How it behaves
- Every `CHECK_CONN_FREQ` seconds (120 by default) the container checks connectivity. **Note:** the balena block checks *internet* reachability (`1.1.1.1`), not just "joined a known SSID". If you're on a known Wi-Fi that has no internet (an offline RV router, for example), the portal will still come up. While the portal runs, the Pi's Wi-Fi radio is the access point, so the Pi is not on any other network.
- NetworkManager reconnects to saved networks by itself at boot and whenever one comes in range. The portal only takes over when that fails.
- `ACTIVITY_TIMEOUT=600` closes an idle portal after 10 minutes, so a temporary outage on a known network doesn't leave the Pi stuck in AP mode. It retries on the next check.
- Join **Magnum-Setup** from a phone. The captive page should open on its own; if not, browse to `http://192.168.42.1`. Pick the network and enter its password. The dashboard (port 17223) keeps running throughout.

## Host requirements (Raspberry Pi OS)
- **NetworkManager** must manage `wlan0`. It's the default on Raspberry Pi OS **Bookworm** and later. On Bullseye or older (dhcpcd/wpa_supplicant), switch with `sudo raspi-config` → *Advanced Options* → *Network Config* → *NetworkManager*, then reboot.
  Check with `nmcli general status` and `nmcli device` (wlan0 should be `connected` or `disconnected`, not `unmanaged`).
- D-Bus at `/run/dbus` (standard). The container mounts it read/write to talk to NetworkManager.
- Port 80 must be free on the host (the portal web server uses host networking).
- Use 64-bit Pi OS for the default `aarch64` image. On 32-bit, set `WIFI_CONNECT_ARCH=armv7hf` in a `.env` file next to `compose.yml`.

## Fallback: preload Wi-Fi from the boot partition
Use this if the portal doesn't work, or before first boot:
1. **Raspberry Pi Imager (simplest):** in OS customisation, set the SSID, password and Wi-Fi country. Imager writes these into the boot partition and they're applied on first boot.
2. **Bookworm+ after first boot:** put the SD card in a computer and edit `firstrun.sh` / `custom.toml` on the `bootfs` partition if present. Or, from a console or HDMI session:
   `sudo nmcli dev wifi connect "<SSID>" password "<password>"`
3. **Legacy (Bullseye and older, no NetworkManager):** `wpa_supplicant.conf` on the boot partition:
   ```
   country=US
   ctrl_interface=DIR=/var/run/wpa_supplicant GROUP=netdev
   update_config=1
   network={
     ssid="YourSSID"
     psk="YourPassword"
   }
   ```
   Bookworm ignores this file. Use options 1 or 2 there.

Adding several networks (home, RV hotspot, common campgrounds) ahead of time with `nmcli` means the portal rarely needs to appear.

## Disabling it
`docker compose up -d magnum` starts only the dashboard. You can also delete the `wifi-connect` service from `compose.yml`.
