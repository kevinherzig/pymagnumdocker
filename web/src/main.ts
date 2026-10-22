import "./style.css";
import { Gauge } from "./gauge";
import { Spark } from "./chart";
import type { History, State, Summary } from "./types";

const STALE_AFTER = 10; // seconds
const RED = "#ef5350", YEL = "#ffca28", GRN = "#66bb6a";

const gauges = {
  vdc: new Gauge({ label: "Battery", unit: "V", min: 10, max: 15, decimals: 1,
    zones: [{ to: 11.8, color: RED }, { to: 12.2, color: YEL }, { to: 14.8, color: GRN }, { to: 15, color: RED }] }),
  soc: new Gauge({ label: "State of charge", unit: "%", min: 0, max: 100,
    zones: [{ to: 30, color: RED }, { to: 50, color: YEL }, { to: 100, color: GRN }] }),
  ac_out_w: new Gauge({ label: "AC out", unit: "W", min: 0, max: 3000, color: "#ffa726" }),
  ac_in_w: new Gauge({ label: "AC in", unit: "W", min: 0, max: 3600, color: "#4fc3f7" }),
  charge_amps: new Gauge({ label: "DC amps", unit: "A", min: -150, max: 150, color: "#ab47bc" }),
  battery_temp: new Gauge({ label: "Battery temp", unit: "°C", min: -10, max: 60,
    zones: [{ to: 0, color: YEL }, { to: 45, color: GRN }, { to: 60, color: RED }] }),
};
type GaugeKey = keyof typeof gauges;

const tileDefs: [string, (s: Summary) => [string, string?]][] = [
  ["Inverter", (s) => [s.invled_text || s.mode_text || "—"]],
  ["Charger", (s) => [s.chgled_text || "—"]],
  ["Shore power", (s) => (s.shore ? [`Yes ${s.vac_in?.toFixed(0) ?? ""} V`, "ok"] : ["No", "muted"])],
  ["Generator (AGS)", (s) => (s.ags_status ? [s.ags_status, s.ags_running ? "ok" : undefined] : ["Not installed", "muted"])],
  ["Faults", (s) => (s.fault ? [s.fault_text || `Code ${s.fault}`, "bad"] : ["None", "ok"])],
];

const charts: [string, Spark][] = [
  ["vdc", new Spark("Battery V", GRN, "V")],
  ["soc", new Spark("SOC", "#4fc3f7", "%")],
  ["ac_out_w", new Spark("AC out", "#ffa726", "W")],
  ["ac_in_w", new Spark("AC in", "#4fc3f7", "W")],
];

const $ = (id: string) => document.getElementById(id) as HTMLElement;
Object.values(gauges).forEach((g) => $("gauges").append(g.root));
const tiles = tileDefs.map(([label]) => {
  const d = document.createElement("div");
  d.className = "card tile";
  d.innerHTML = `<h2></h2><div class="v">—</div>`;
  (d.querySelector("h2") as HTMLElement).textContent = label;
  $("tiles").append(d);
  return d;
});
charts.forEach(([, c]) => $("charts").append(c.root));

let lastUpdate = 0; // epoch seconds from server

function render(st: State): void {
  if (st.updated) lastUpdate = Date.now() / 1000 - (st.age ?? 0);
  const s = st.summary;
  if (!s) return;
  (Object.keys(gauges) as GaugeKey[]).forEach((k) => gauges[k].set(s[k]));
  tileDefs.forEach(([, fn], i) => {
    const [text, cls] = fn(s);
    const v = tiles[i].querySelector(".v") as HTMLElement;
    v.textContent = text;
    tiles[i].className = `card tile ${cls ?? ""}`;
  });
}

function tickStale(): void {
  const pill = $("stale");
  if (!lastUpdate) return;
  const age = Math.round(Date.now() / 1000 - lastUpdate);
  const stale = age > STALE_AFTER;
  pill.textContent = stale ? `stale · ${age}s ago` : "live";
  pill.className = `pill ${stale ? "bad" : "ok"}`;
}

async function loadHistory(): Promise<void> {
  try {
    const h: History = await (await fetch("/api/history")).json();
    for (const [key, chart] of charts) {
      const i = h.fields.indexOf(key);
      const pts = h.rows.filter((r) => r[i] != null).map((r) => [r[0] as number, r[i] as number] as [number, number]);
      chart.draw(pts);
    }
  } catch { /* retry on next interval */ }
}

function connect(): void {
  const es = new EventSource("/api/stream");
  es.onmessage = (e) => render(JSON.parse(e.data) as State);
  es.onerror = () => { es.close(); setTimeout(connect, 5000); };
}

connect();
loadHistory();
setInterval(tickStale, 1000);
setInterval(loadHistory, 60_000);
