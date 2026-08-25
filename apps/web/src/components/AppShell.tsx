import { NavLink, Outlet } from 'react-router-dom';

export function AppShell() {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand__mark">RG</span>
          <span>ReleaseGate</span>
        </div>

        <nav className="sidebar__nav" aria-label="Primary navigation">
          <NavLink to="/" end>
            Projects
          </NavLink>
          <span className="sidebar__disabled">Audit log <small>soon</small></span>
          <span className="sidebar__disabled">SDK keys <small>soon</small></span>
        </nav>

        <div className="sidebar__footer">
          <span className="environment-chip">
            <span className="environment-chip__dot" />
            Local workspace
          </span>
          <small>v0.1 · control plane</small>
        </div>
      </aside>

      <main className="workspace">
        <Outlet />
      </main>
    </div>
  );
}
