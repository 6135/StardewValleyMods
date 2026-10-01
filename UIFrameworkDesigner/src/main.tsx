import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { missingShapes } from './fieldShapes';
import { startAutosave } from './io/autosave';
import { App } from './ui/App';
import './index.css';

if (import.meta.env.DEV) {
  const missing = missingShapes();
  if (missing.length > 0) {
    console.warn(`fieldShapes.ts has no entry for: ${missing.join(', ')}`);
  }
}

startAutosave();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>
);
