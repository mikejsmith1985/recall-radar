// Vite build and dev-server settings. In development the API runs separately on port 5180,
// so `/api` and `/health` are proxied there; in production the API serves the built files.
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export const apiDevServerUrl = "http://127.0.0.1:5180";

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      "/api": apiDevServerUrl,
      "/health": apiDevServerUrl,
    },
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
  },
  test: {
    environment: "jsdom",
    globals: true,
    include: ["src/**/*.test.{ts,tsx}", "vite.config.test.ts"],
  },
});
