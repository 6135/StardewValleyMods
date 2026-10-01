import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// GitHub Pages serves the repository site under /StardewValleyMods/designer/
export default defineConfig({
  base: '/StardewValleyMods/designer/',
  plugins: [react()]
});
