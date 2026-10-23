// Drag-to-reorder for dashboard cards. Tiny and dependency-free (Pointer Events), works with
// mouse, touch and pen. Dragging only starts from the grip handle in each card's corner, so
// taps elsewhere (e.g. the temperature °C/°F toggle) and page scrolling keep working on phones.
// Order is persisted in localStorage per section, like the temperature unit.

/** Saved order first (known ids only, de-duplicated); any ids not saved are appended in default order. */
export function mergeOrder(saved: unknown, defaults: readonly string[]): string[] {
  const known = new Set(defaults);
  const out: string[] = [];
  if (Array.isArray(saved)) {
    for (const id of saved) if (typeof id === "string" && known.has(id) && !out.includes(id)) out.push(id);
  }
  for (const id of defaults) if (!out.includes(id)) out.push(id);
  return out;
}

/** Read a saved order from storage; corrupt/missing values fall back to the defaults. */
export function loadOrder(store: Pick<Storage, "getItem">, key: string, defaults: readonly string[]): string[] {
  let saved: unknown = null;
  try { saved = JSON.parse(store.getItem(key) ?? "null"); } catch { saved = null; }
  return mergeOrder(saved, defaults);
}

export const sameOrder = (a: readonly string[], b: readonly string[]) => a.length === b.length && a.every((v, i) => v === b[i]);

interface Drag { item: HTMLElement; pointer: number; dx: number; dy: number; x: number; y: number; raf: number; }

const EDGE = 56; // px from viewport top/bottom that auto-scrolls while dragging

export class Sortable {
  private items = new Map<string, HTMLElement>();
  private drag: Drag | null = null;
  onChange: () => void = () => {};

  constructor(private container: HTMLElement, private key: string, private defaults: readonly string[]) {}

  /** Register a card under an id and give it a grip handle. Call apply() after adding all items. */
  add(id: string, item: HTMLElement, label: string): void {
    item.dataset.tileId = id;
    item.classList.add("sortable");
    this.items.set(id, item);
    const h = document.createElement("button");
    h.type = "button";
    h.className = "drag-handle";
    h.textContent = "⠿";
    h.title = `Drag to move ${label} (or focus and use arrow keys)`;
    h.ariaLabel = `Move ${label}`;
    h.addEventListener("pointerdown", (e) => this.start(e, item));
    // Never let the handle trigger the card's own click/keyboard behaviour (temperature toggle).
    h.addEventListener("click", (e) => e.stopPropagation());
    h.addEventListener("keydown", (e) => {
      e.stopPropagation();
      const dir = e.key === "ArrowLeft" || e.key === "ArrowUp" ? -1 : e.key === "ArrowRight" || e.key === "ArrowDown" ? 1 : 0;
      if (!dir) return;
      e.preventDefault();
      const sib = this.neighbour(item, dir);
      if (!sib) return;
      if (dir < 0) sib.before(item); else sib.after(item);
      h.focus();
      this.save();
    });
    item.append(h);
  }

  order(): string[] {
    return Array.from(this.container.children).map((c) => (c as HTMLElement).dataset.tileId).filter((v): v is string => !!v);
  }

  apply(order = loadOrder(localStorage, this.key, this.defaults)): void {
    for (const id of order) { const el = this.items.get(id); if (el) this.container.append(el); }
  }

  isDefault(): boolean { return sameOrder(this.order(), mergeOrder(null, this.defaults)); }

  reset(): void {
    localStorage.removeItem(this.key);
    this.apply(mergeOrder(null, this.defaults));
    this.onChange();
  }

  private save(): void {
    const o = this.order();
    if (sameOrder(o, mergeOrder(null, this.defaults))) localStorage.removeItem(this.key);
    else localStorage.setItem(this.key, JSON.stringify(o));
    this.onChange();
  }

  private visibleSiblings(item: HTMLElement): HTMLElement[] {
    return Array.from(this.container.children).filter((c): c is HTMLElement => c !== item && c instanceof HTMLElement && !c.hidden && !!c.dataset.tileId);
  }

  private neighbour(item: HTMLElement, dir: number): HTMLElement | null {
    const all = Array.from(this.container.children).filter((c): c is HTMLElement => c instanceof HTMLElement && !c.hidden && !!c.dataset.tileId);
    return all[all.indexOf(item) + dir] ?? null;
  }

  private onMove = (e: PointerEvent) => {
    if (this.drag?.pointer !== e.pointerId) return;
    e.preventDefault();
    this.drag.x = e.clientX; this.drag.y = e.clientY;
  };
  private onUp = (e: PointerEvent) => this.end(e.pointerId);

  private start(e: PointerEvent, item: HTMLElement): void {
    if (this.drag || (e.pointerType === "mouse" && e.button !== 0)) return;
    e.preventDefault();
    e.stopPropagation();
    const r = item.getBoundingClientRect();
    // Listen on window rather than using pointer capture: moving the card in the DOM drops capture.
    window.addEventListener("pointermove", this.onMove);
    window.addEventListener("pointerup", this.onUp);
    window.addEventListener("pointercancel", this.onUp);
    this.drag = { item, pointer: e.pointerId, dx: e.clientX - r.left, dy: e.clientY - r.top, x: e.clientX, y: e.clientY, raf: 0 };
    item.classList.add("dragging");
    this.container.classList.add("sorting");
    document.body.classList.add("is-dragging");
    const tick = () => { if (!this.drag) return; this.step(); this.drag.raf = requestAnimationFrame(tick); };
    tick();
  }

  private step(): void {
    const d = this.drag!;
    if (d.y < EDGE) window.scrollBy(0, -Math.ceil((EDGE - d.y) / 4));
    else if (d.y > innerHeight - EDGE) window.scrollBy(0, Math.ceil((d.y - innerHeight + EDGE) / 4));
    // Swap with whichever card the pointer is over (cards are not transformed, so rects are their slots).
    for (const s of this.visibleSiblings(d.item)) {
      const r = s.getBoundingClientRect();
      if (d.x >= r.left && d.x <= r.right && d.y >= r.top && d.y <= r.bottom) {
        if (d.item.compareDocumentPosition(s) & Node.DOCUMENT_POSITION_FOLLOWING) s.after(d.item); else s.before(d.item);
        break;
      }
    }
    // Keep the dragged card under the finger/cursor, relative to its current slot.
    d.item.style.transform = "";
    const r = d.item.getBoundingClientRect();
    d.item.style.transform = `translate(${d.x - d.dx - r.left}px, ${d.y - d.dy - r.top}px)`;
  }

  private end(pointer: number): void {
    const d = this.drag;
    if (!d || d.pointer !== pointer) return;
    this.drag = null;
    cancelAnimationFrame(d.raf);
    window.removeEventListener("pointermove", this.onMove);
    window.removeEventListener("pointerup", this.onUp);
    window.removeEventListener("pointercancel", this.onUp);
    d.item.style.transform = "";
    d.item.classList.remove("dragging");
    this.container.classList.remove("sorting");
    document.body.classList.remove("is-dragging");
    this.save();
  }
}
