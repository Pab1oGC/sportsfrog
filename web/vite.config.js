import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      // path.resolve gives back native separators. On Windows that's
      // backslashes, and Vite's own resolver for a *relative* import
      // ("./session-store") normalizes to forward slashes — so the same file
      // reached through the "src/..." alias and through a relative import
      // from a different folder got two different module ids, and therefore
      // two separate copies of any module-level state. session-store.js's
      // in-memory refresh token was exactly that: set through one copy
      // (auth/context/jwt/action.js, relative import) and read through the
      // other (lib/axios.js, aliased import), so it always looked absent
      // unless "recordarme" had also written it to localStorage.
      src: path.resolve(__dirname, "./src").split(path.sep).join("/"),
    },
  },
  server: {
    // Without this Vite binds its dev server to localhost only, so a phone
    // on the same Wi-Fi can't reach it at all (connection refused) even
    // with the right LAN IP in the URL. `true` binds every interface
    // (0.0.0.0) and prints the LAN address Vite picked on startup.
    host: true,
    // Vite refuses requests whose Host header it doesn't recognize, as a
    // guard against DNS-rebinding attacks. A quick cloudflared tunnel picks
    // a random "*.trycloudflare.com" subdomain each time it starts, so
    // listing today's host would break the moment the tunnel is restarted —
    // the wildcard is what actually keeps working across runs. Plain IPv4/
    // IPv6 hosts (e.g. a LAN IP like 192.168.1.4) are always allowed by
    // Vite regardless of this list, so LAN testing by IP needs no entry here.
    allowedHosts: [".trycloudflare.com"],
    proxy: {
      "/api": {
        target: "http://localhost:5293",
        changeOrigin: true,
        rewrite: function(path) {
          return path.replace(/^\/api/, "");
        },
      },
    },
  },
});