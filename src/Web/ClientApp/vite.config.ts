import {defineConfig} from "vite";
import {sveltekit} from "@sveltejs/kit/vite";
import tailwindcss from "@tailwindcss/vite";

// https://vite.dev/config/
export default defineConfig({
    envPrefix: ["VITE_", "API_"],
    plugins: [sveltekit(), tailwindcss()],
    server: {
        proxy: {
            "/api": {
                target: "http://localhost:8080",
                changeOrigin: true,
            },
        },
    },
});
