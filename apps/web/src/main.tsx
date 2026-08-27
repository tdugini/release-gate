import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import './styles.css';
import './management.css';
import './pending-review.css';
import './auth.css';
import './v1-polish.css';
import './v1-second-pass.css';
import './v1-third-pass.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
