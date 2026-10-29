import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    globals: true,
    testTimeout: 30000,
      setupFiles: "./vitest.setup.ts", // or .js — match whatever extension you saved it as
    coverage: {
      provider: "v8", // you're already using this, based on the report header
      reporter: ["text", "html", "json-summary"],
      exclude: [
        "**/*.css",
        "**/*.d.ts",
        "**/node_modules/**",
        "**/tests/**",
        "**/dist/**",
        // keep any excludes you already have here
      ],
    },
  },
});
