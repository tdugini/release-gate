import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from './AuthProvider';

export function AppShell() {
  const { identity, signOut } = useAuth();

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
          <div className="control-plane-user">
            <div>
              <strong>{identity.displayName}</strong>
              <small>{identity.subject}</small>
            </div>
            <div className="control-plane-user__roles" aria-label="Assigned roles">
              {identity.roles.map((role) => <span key={role}>{role}</span>)}
            </div>
            <button className="sidebar__signout" type="button" onClick={signOut}>
              Sign out
            </button>
          </div>

          <span className="environment-chip">
            <span className="environment-chip__dot" />
            Local workspace
          </span>
          <small>v0.6 · authenticated control plane</small>
        </div>
      </aside>

      <main className="workspace">
        <Outlet />
      </main>
    </div>
  );
}
