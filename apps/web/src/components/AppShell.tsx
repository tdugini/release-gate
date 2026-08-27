import { useRef } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from './AuthProvider';

export function AppShell() {
  const { identity, signOut } = useAuth();
  const mobileMenuRef = useRef<HTMLDetailsElement>(null);
  const workspaceLabel = import.meta.env.PROD ? 'Self-hosted deployment' : 'Local workspace';
  const initials = identity.displayName
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('') || 'RG';

  const closeMobileMenu = () => {
    if (mobileMenuRef.current) mobileMenuRef.current.open = false;
  };

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand__mark">RG</span>
          <span>ReleaseGate</span>
        </div>

        <nav className="sidebar__nav" aria-label="Primary navigation">
          <NavLink to="/" end>
            <svg className="sidebar__nav-icon" viewBox="0 0 24 24" aria-hidden="true">
              <path d="M4.75 5.75h5.1l1.45 1.7h7.95v10.8H4.75z" />
            </svg>
            <span>Projects</span>
          </NavLink>
        </nav>

        <details className="mobile-account-menu" ref={mobileMenuRef}>
          <summary aria-label="Open account menu">
            <span className="mobile-account-menu__avatar" aria-hidden="true">
              {initials}
            </span>
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="m8 10 4 4 4-4" />
            </svg>
          </summary>

          <div className="mobile-account-menu__panel">
            <div className="mobile-account-menu__identity">
              <span className="mobile-account-menu__avatar" aria-hidden="true">
                {initials}
              </span>
              <div>
                <strong>{identity.displayName}</strong>
                <small>{identity.subject}</small>
              </div>
            </div>

            <div className="mobile-account-menu__roles" aria-label="Assigned roles">
              {identity.roles.map((role) => <span key={role}>{role}</span>)}
            </div>

            <div className="mobile-account-menu__divider" />

            <NavLink
              className="mobile-account-menu__item"
              to="/"
              end
              onClick={closeMobileMenu}
            >
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M4.75 5.75h5.1l1.45 1.7h7.95v10.8H4.75z" />
              </svg>
              <span>Projects</span>
            </NavLink>

            <button
              className="mobile-account-menu__item mobile-account-menu__signout"
              type="button"
              onClick={signOut}
            >
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M10 5H5v14h5M14 8l4 4-4 4M18 12H9" />
              </svg>
              <span>Sign out</span>
            </button>
          </div>
        </details>

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
            {workspaceLabel}
          </span>
          <small>v1.0 · release control plane</small>
        </div>
      </aside>

      <main className="workspace">
        <Outlet />
      </main>
    </div>
  );
}
