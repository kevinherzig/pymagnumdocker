// Tiny dependency-free SVG arc gauge (no external libs => works offline).
export interface Zone { to: number; color: string; }
export interface GaugeOpts { label: string; unit: string; min: number; max: number; decimals?: number; zones?: Zone[]; color?: string; }

const NS = "http://www.w3.org/2000/svg";
const R = 80, CX = 100, CY = 100, SWEEP = 240, START = 150;

function polar(deg: number): [number, number] {
  const r = (deg * Math.PI) / 180;
  return [CX + R * Math.cos(r), CY + R * Math.sin(r)];
}
function arc(a0: number, a1: number): string {
  const [x0, y0] = polar(a0), [x1, y1] = polar(a1);
  return `M${x0.toFixed(2)} ${y0.toFixed(2)}A${R} ${R} 0 ${a1 - a0 > 180 ? 1 : 0} 1 ${x1.toFixed(2)} ${y1.toFixed(2)}`;
}
function el<K extends keyof SVGElementTagNameMap>(tag: K, attrs: Record<string, string>): SVGElementTagNameMap[K] {
  const e = document.createElementNS(NS, tag);
  for (const [k, v] of Object.entries(attrs)) e.setAttribute(k, v);
  return e;
}

export class Gauge {
  readonly root: HTMLElement;
  private bar: SVGPathElement;
  private text: SVGTextElement;
  constructor(private o: GaugeOpts) {
    this.root = document.createElement("div");
    this.root.className = "card gauge";
    const svg = el("svg", { viewBox: "0 0 200 170", role: "img", "aria-label": o.label });
    svg.append(el("path", { d: arc(START, START + SWEEP), class: "track" }));
    let from = o.min;
    for (const z of o.zones ?? []) {
      svg.append(el("path", { d: arc(this.angle(from), this.angle(z.to)), class: "zone", stroke: z.color }));
      from = z.to;
    }
    this.bar = el("path", { d: "", class: "bar", stroke: o.color ?? "#4fc3f7" });
    this.text = el("text", { x: "100", y: "108", class: "value" });
    const unit = el("text", { x: "100", y: "134", class: "unit" });
    unit.textContent = o.unit;
    svg.append(this.bar, this.text, unit);
    const h = document.createElement("h2");
    h.textContent = o.label;
    this.root.append(svg, h);
    this.set(null);
  }
  private angle(v: number): number {
    const f = Math.min(1, Math.max(0, (v - this.o.min) / (this.o.max - this.o.min)));
    return START + f * SWEEP;
  }
  set(v: number | null): void {
    if (v == null || Number.isNaN(v)) {
      this.text.textContent = "—";
      this.bar.setAttribute("d", "");
      return;
    }
    this.text.textContent = v.toFixed(this.o.decimals ?? 0);
    const a = this.angle(v);
    this.bar.setAttribute("d", a > START + 0.5 ? arc(START, a) : "");
    const z = this.o.zones?.find((z) => v <= z.to);
    if (z) this.bar.setAttribute("stroke", z.color);
  }
}
