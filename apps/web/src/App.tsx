import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { AppShell } from './components/AppShell';
import { AuthProvider } from './components/AuthProvider';
import { ToastProvider } from './components/ToastProvider';
import { FlagPage } from './pages/FlagPage';
import { ProjectPage } from './pages/ProjectPage';
import { ProjectsPage } from './pages/ProjectsPage';

export function App() {
  return (
    <AuthProvider>
      <ToastProvider>
        <BrowserRouter>
          <Routes>
            <Route element={<AppShell />}>
              <Route index element={<ProjectsPage />} />
              <Route path="/projects/:projectKey" element={<ProjectPage />} />
              <Route path="/projects/:projectKey/flags/:flagKey" element={<FlagPage />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </ToastProvider>
    </AuthProvider>
  );
}
