import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { api } from '../api';
export function LoginGate({ children }: { children: ReactNode }) {
  const [state, setState] = useState<{ setupRequired: boolean; authenticated: boolean }>();
  const [email, setEmail] = useState(''); const [password, setPassword] = useState(''); const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  useEffect(() => { api<typeof state>('/auth/status').then(setState).catch(e => setError(e.message)); }, []);
  if (state?.authenticated) return children;
  return <div className="view-container" style={{ maxWidth: 480, margin: '12vh auto' }}><div className="cyber-panel" style={{ padding: 28 }}>
    <h1 className="view-title">SAMMICRAWL</h1><p>{state?.setupRequired ? 'Tạo tài khoản quản trị cho dự án của bạn' : 'Đăng nhập dự án'}</p>
    <form onSubmit={async e => { e.preventDefault(); setBusy(true); setError(''); try { await api(`/auth/${state?.setupRequired ? 'setup' : 'login'}`, { email, password }); setState({ setupRequired: false, authenticated: true }); } catch (e) { setError((e as Error).message); } finally { setBusy(false); } }}>
      <label>Email<input className="cyber-input-field" type="email" required value={email} onChange={e => setEmail(e.target.value)} autoComplete="username" /></label>
      <label>Mật khẩu<input className="cyber-input-field" type="password" required minLength={state?.setupRequired ? 12 : 1} value={password} onChange={e => setPassword(e.target.value)} autoComplete={state?.setupRequired ? 'new-password' : 'current-password'} /></label>
      {error && <p role="alert">{error}</p>}<button className="btn-launch-full" disabled={!state || busy}>{busy ? 'Đang xử lý…' : state?.setupRequired ? 'Tạo tài khoản quản trị' : 'Đăng nhập'}</button>
    </form></div></div>;
}
