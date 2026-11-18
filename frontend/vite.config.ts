/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      // O cookie de sessão é HttpOnly/Secure/SameSite=Strict: em desenvolvimento
      // a API é acessada pelo mesmo origin através deste proxy.
      '/api': {
        target: process.env.VITE_API_TARGET ?? 'http://localhost:5000',
        changeOrigin: false,
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    css: false,
  },
});
