// Minimal canvas line chart for the 24h history (no external libs).
export class Spark {
  readonly root: HTMLElement;
  private canvas: HTMLCanvasElement;
  private cap: HTMLElement;
  constructor(private label: string, private color: string, private unit: string) {
    this.root = document.createElement("div");
    this.root.className = "card chart";
    this.cap = document.createElement("h2");
    this.cap.textContent = label;
    this.canvas = document.createElement("canvas");
    this.root.append(this.cap, this.canvas);
  }
  draw(points: [number, number][]): void {
    const c = this.canvas, dpr = window.devicePixelRatio || 1;
    const w = c.clientWidth || 300, h = c.clientHeight || 80;
    c.width = w * dpr; c.height = h * dpr;
    const g = c.getContext("2d");
    if (!g) return;
    g.scale(dpr, dpr);
    g.clearRect(0, 0, w, h);
    if (points.length < 2) {
      this.cap.textContent = `${this.label} (collecting…)`;
      return;
    }
    let lo = Infinity, hi = -Infinity;
    for (const [, y] of points) { lo = Math.min(lo, y); hi = Math.max(hi, y); }
    if (hi === lo) { hi += 1; lo -= 1; }
    const t0 = points[0][0], t1 = points[points.length - 1][0];
    const x = (t: number) => ((t - t0) / Math.max(1, t1 - t0)) * (w - 4) + 2;
    const y = (v: number) => h - 4 - ((v - lo) / (hi - lo)) * (h - 8);
    g.strokeStyle = this.color; g.lineWidth = 1.5; g.beginPath();
    points.forEach(([t, v], i) => (i ? g.lineTo(x(t), y(v)) : g.moveTo(x(t), y(v))));
    g.stroke();
    const f = (n: number) => (Math.abs(n) >= 100 ? n.toFixed(0) : n.toFixed(1));
    this.cap.textContent = `${this.label}  ${f(lo)}–${f(hi)} ${this.unit}`;
  }
}
