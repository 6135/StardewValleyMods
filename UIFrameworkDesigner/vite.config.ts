import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// GitHub Pages serves the repository site under /StardewValleyMods/
export default defineConfig({
  base: '/StardewValleyMods/',
  plugins: [react()]
});
