/// <reference types="vitest/config" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import path from "node:path";

const api = process.env.API_URL ?? "http://localhost:8080";

// The dev server proxies backend paths, so the browser sees ONE origin
// (like nginx does in Docker). That keeps the refresh-token cookie same-site.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { "@": path.resolve(__dirname, "src") },
  },
  server: {
    port: 5173,
    proxy: {
      // gRPC-Web calls: /api/phonebook.v1.PhonebookService/ListContacts -> :8080/phonebook.v1...
      "/api": { target: api, changeOrigin: true, rewrite: (p) => p.replace(/^\/api/, "") },
      // SignalR (WebSockets)
      "/hubs": { target: api, changeOrigin: true, ws: true },
      "/scalar": { target: api, changeOrigin: true },
      "/swagger": { target: api, changeOrigin: true },
    },
  },
  build: {
    rollupOptions: {
      output: {
        // Split big libraries into their own cached files.
        manualChunks: {
          react: ["react", "react-dom", "react-dom/client", "react-router"],
          query: ["@tanstack/react-query"],
          forms: ["react-hook-form", "@hookform/resolvers", "zod"],
          grpc: ["@connectrpc/connect", "@connectrpc/connect-web", "@bufbuild/protobuf"],
          signalr: ["@microsoft/signalr"],
        },
      },
    },
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    css: false,
  },
});
