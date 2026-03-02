import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

export default defineConfig({
  plugins: [vue()],
  test: {
    environment: "jsdom",
    globals: true,
    include: ["src/**/*.test.js"],
  },
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
    port: 5173,
  },
});
