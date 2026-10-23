// Unit tests for the tile-order persistence logic (run: npm test; uses node:test, no extra deps).
import { test } from "node:test";
import assert from "node:assert/strict";
import { loadOrder, mergeOrder, sameOrder } from "../src/layout";

const DEF = ["vdc", "charge_amps", "battery_watts", "soc", "ac_out_w", "ac_in_w", "battery_temp"];
const store = (v: string | null) => ({ getItem: () => v });

test("no saved layout -> default order", () => {
  assert.deepEqual(mergeOrder(null, DEF), DEF);
  assert.deepEqual(loadOrder(store(null), "k", DEF), DEF);
});

test("saved order is restored", () => {
  const saved = ["battery_temp", "soc", "vdc", "charge_amps", "battery_watts", "ac_out_w", "ac_in_w"];
  assert.deepEqual(loadOrder(store(JSON.stringify(saved)), "k", DEF), saved);
});

test("new tile ids are appended in default order; removed ids dropped; duplicates ignored", () => {
  const saved = ["soc", "gone", "vdc", "soc", 42];
  assert.deepEqual(mergeOrder(saved, DEF), ["soc", "vdc", "charge_amps", "battery_watts", "ac_out_w", "ac_in_w", "battery_temp"]);
});

test("corrupt storage falls back to defaults", () => {
  assert.deepEqual(loadOrder(store("{not json"), "k", DEF), DEF);
  assert.deepEqual(loadOrder(store('{"a":1}'), "k", DEF), DEF);
});

test("sameOrder", () => {
  assert.ok(sameOrder(DEF, [...DEF]));
  assert.ok(!sameOrder(DEF, [...DEF].reverse()));
  assert.ok(!sameOrder(DEF, DEF.slice(1)));
});
