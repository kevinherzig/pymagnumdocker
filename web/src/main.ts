import "./style.css";
import { Gauge } from "./gauge";
import { Spark } from "./chart";
import type { History, State, Summary } from "./types";

const STALE_AFTER = 10; // seconds
const RED = "#ef5350", YEL = "#ffca28", GRN = "#66bb6a";

// Battery temp tile toggles °C/°F on click; choice persisted in localStorage.
function tempGauge() {
  const KEY = "tempUnit";
  const c = new Gauge({ label: "Battery temp", unit: "°C · tap for °F", min: -10, max: 60,
    zones: [{ to: 0, color: YEL }, { to: 45, color: GRN }, { to: 60, color: RED }] });
  const f = new Gauge({ label: "Battery temp", unit: "°F · tap for °C", min: 14, max: 140,
    zones: [{ to: 32, color: YEL }, { to: 113, color: GRN }, { to: 140, color: RED }] });
  let useF = localStorage.getItem(KEY) === "F";
  let last: number | null = null;
  const root = document.createElement("div");
  root.className = "temp-toggle";
  root.style.cursor = "pointer";
  root.tabIndex = 0;
  root.setAttribute("role", "button");
  const show = () => {
    root.replaceChildren(useF ? f.root : c.root);
    root.title = root.ariaLabel = `Battery temperature in ${useF ? "Fahrenheit" : "Celsius"}; click to switch`;
    set(last);
  };
  const set = (v: number | null) => {
    last = v;
    (useF ? f : c).set(v == null ? v : useF ? v * 9 / 5 + 32 : v);
  };
  const toggle = () => { useF = !useF; localStorage.setItem(KEY, useF ? "F" : "C"); show(); };
  root.addEventListener("click", toggle);
  root.addEventListener("keydown", (e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); toggle(); } });
  show();
  return { root, set };
}

const gauges = {
  vdc: new Gauge({ label: "Battery", unit: "V", min: 10, max: 15, decimals: 1,
    zones: [{ to: 11.8, color: RED }, { to: 12.2, color: YEL }, { to: 14.8, color: GRN }, { to: 15, color: RED }] }),
  charge_amps: new Gauge({ label: "DC amps", unit: "A  (− = discharging)", min: -150, max: 150, decimals: 1, color: "#ab47bc" }),
  soc: new Gauge({ label: "State of charge", unit: "%", min: 0, max: 100,
    zones: [{ to: 30, color: RED }, { to: 50, color: YEL }, { to: 100, color: GRN }] }),
  ac_out_w: new Gauge({ label: "AC out", unit: "W", min: 0, max: 3000, color: "#ffa726" }),
  ac_in_w: new Gauge({ label: "AC in", unit: "W", min: 0, max: 3600, color: "#4fc3f7" }),
  battery_temp: tempGauge(),
};
type GaugeKey = keyof typeof gauges;

const tileDefs: [string, (s: Summary) => [string, string?]][] = [
  ["Inverter", (s) => [s.invled_text ? `LED ${s.invled_text}` : "—", s.invled_text === "On" ? "ok" : "muted"]],
  ["Charger", (s) => [s.charger_state || "—", s.charging ? "ok" : "muted"]],
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
// Battery watts = volts x amps (computed here; same source + sign convention as the amps gauge)
const wattsGauge = new Gauge({ label: "Battery watts", unit: "W  (− = discharging)", min: -2500, max: 2500, color: "#26a69a" });
gauges.charge_amps.root.after(wattsGauge.root);
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
let dataWarning = "";

function render(st: State): void {
  if (st.updated) lastUpdate = Date.now() / 1000 - (st.age ?? 0);
  const s = st.summary;
  if (!s) return;
  renderHeader(s);
  (Object.keys(gauges) as GaugeKey[]).forEach((k) => gauges[k].set(s[k]));
  // SOC needs a battery monitor (BMK); hide the gauge when readings come from the inverter
  gauges.soc.root.hidden = s.battery_source !== "bmk";
  (gauges.vdc.root.querySelector("h2") as HTMLElement).textContent =
    s.battery_source === "bmk" ? "Battery (BMK)" : "Battery (inverter)";
  // Prefer the BMK's battery current (signed: negative = discharging); fall back to inverter DC amps
  const useBmkAmps = s.battery_source === "bmk" && s.battery_amps != null;
  gauges.charge_amps.set(useBmkAmps ? s.battery_amps : s.charge_amps);
  (gauges.charge_amps.root.querySelector("h2") as HTMLElement).textContent =
    useBmkAmps ? "Battery amps (BMK)" : "DC amps (inverter)";
  const amps = useBmkAmps ? s.battery_amps : s.charge_amps;
  wattsGauge.set(s.vdc != null && amps != null ? s.vdc * amps : null);
  (wattsGauge.root.querySelector("h2") as HTMLElement).textContent =
    useBmkAmps ? "Battery watts (BMK)" : "DC watts (inverter)";
  dataWarning = s.quality && !s.quality.ok
    ? (s.quality.inverter === "stale" ? "inverter data stale" : `ignored: ${s.quality.nulled.join(", ")}`)
    : "";
  tileDefs.forEach(([, fn], i) => {
    const [text, cls] = fn(s);
    const v = tiles[i].querySelector(".v") as HTMLElement;
    v.textContent = text;
    tiles[i].className = `card tile ${cls ?? ""}`;
  });
}

const ACC_NAMES: Record<string, string> = { remote: "Remote", bmk: "BMK", ags: "AGS", pt100: "PT100", rtr: "Router", acld: "ACLD" };

// Header: inverter identity (model · firmware · nominal V · stack mode), accessories + port,
// then the live status line the original page had (system/fault, mode, VDC, Hz, last update).
function renderHeader(s: Summary): void {
  const id = s.identity;
  const parts: string[] = [];
  if (id?.model_text) parts.push(`Magnum ${id.model_text}`);
  if (id?.firmware) parts.push(`firmware ${id.firmware}`);
  if (id?.nominal_volts) parts.push(`${id.nominal_volts} V`);
  if (id?.stack_mode) parts.push(id.stack_mode);
  const title = parts.length && id?.model_text ? parts.join(" · ") : "Magnum Energy";
  $("title").textContent = title;
  document.title = title;
  const acc = Object.entries(id?.accessories ?? {}).map(([k, v]) => `${ACC_NAMES[k] ?? k.toUpperCase()} ${v === "present" ? "✓" : v}`);
  if (s.comm_device) acc.push(s.comm_device);
  $("meta").textContent = acc.join(" · ");
  const fault = s.fault ? (s.fault_text || `Fault ${s.fault}`) : (s.bmk_fault_text && s.bmk_fault_text !== "Normal" ? `BMK ${s.bmk_fault_text}` : "Normal");
  const st = [`System: ${fault}`];
  if (s.mode_text) st.push(`Inverter: ${s.mode_text}`);
  if (s.vdc != null) st.push(`${s.vdc.toFixed(1)} VDC`);
  if (s.hz != null) st.push(`${s.hz.toFixed(1)} Hz`);
  if (lastUpdate) st.push(`Updated ${new Date(lastUpdate * 1000).toLocaleTimeString()}`);
  $("status").textContent = st.join(" · ");
}

function tickStale(): void {
  const pill = $("stale");
  if (!lastUpdate) return;
  const age = Math.round(Date.now() / 1000 - lastUpdate);
  const stale = age > STALE_AFTER;
  pill.textContent = stale ? `stale · ${age}s ago` : dataWarning ? `live · ${dataWarning}` : "live";
  pill.className = `pill ${stale ? "bad" : dataWarning ? "warn" : "ok"}`;
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
