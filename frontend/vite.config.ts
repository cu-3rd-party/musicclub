import { defineConfig } from "vite";
import preact from "@preact/preset-vite";

export default defineConfig({
  plugins: [preact()],
  build: {
    minify: "terser",
    terserOptions: {
      compress: {
        passes: 2,
        drop_console: true,
      },
    },
  },
  server: {
    port: 5173
  }
});
