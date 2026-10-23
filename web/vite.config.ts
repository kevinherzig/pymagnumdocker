import { defineConfig } from "vite";
export default defineConfig({
  build: { outDir: "dist", emptyOutDir: true, target: "es2020" },
  server: { proxy: { "/api": "http://localhost:17223" } },
});
