import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// GitHub Pages serves the repository site under /StardewValleyMods/designer/
// React is its own chunk so the app chunk stays under the size warning; the schema pass (Ajv), the game-art skin and
// the i18n loader are split by their dynamic imports.
export default defineConfig({
  base: '/StardewValleyMods/designer/',
  plugins: [react()],
  build: {
    rolldownOptions: {
      output: {
        codeSplitting: { groups: [{ name: 'react', test: /[\/]node_modules[\/](react|react-dom|scheduler)[\/]/ }] }
      }
    }
  }
});
