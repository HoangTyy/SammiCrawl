import { useEffect, useState } from 'react';
import type { FormEvent, ReactNode } from 'react';
import { ArrowRight, CircleAlert, Eye, EyeOff, LoaderCircle, LockKeyhole, Mail, Moon, Radar, Sun } from 'lucide-react';
import { api } from '../api';
import { useApp } from '../context/AppContext';
import './LoginGate.css';

type AuthState = { setupRequired: boolean; authenticated: boolean };

export function LoginGate({ children }: { children: ReactNode }) {
  const { theme, toggleTheme } = useApp();
  const [state, setState] = useState<AuthState>();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const loadStatus = () => {
    setError('');
    void api<AuthState>('/auth/status').then(setState).catch(error => setError(error.message));
  };
  useEffect(loadStatus, []);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError('');
    try {
      await api(`/auth/${state?.setupRequired ? 'setup' : 'login'}`, { email, password });
      setPassword('');
      setState({ setupRequired: false, authenticated: true });
    } catch (error) {
      setError((error as Error).message);
    } finally {
      setBusy(false);
    }
  };

  if (state?.authenticated) return children;

  return <main className="login-page">
    <button className="login-theme" type="button" onClick={toggleTheme} aria-label={theme === 'dark' ? 'Chuyển sang giao diện sáng' : 'Chuyển sang giao diện tối'}>
      {theme === 'dark' ? <Sun size={18} /> : <Moon size={18} />}
    </button>
    <div className="login-container">
      <div className="login-brand"><span className="login-logo"><Radar size={24} strokeWidth={1.8} /></span><span>Sammi<span className="login-brand-accent">Crawl</span></span></div>
      <section className="login-card" aria-labelledby="login-title">
        <header className="login-heading">
          <span className="login-eyebrow">KHÔNG GIAN NỘI DUNG</span>
          <h1 id="login-title">{state?.setupRequired ? 'Bắt đầu cùng SammiCrawl' : 'Chào mừng trở lại'}</h1>
          <p>{state?.setupRequired ? 'Tạo tài khoản quản trị đầu tiên cho dự án.' : 'Đăng nhập để tiếp tục quản lý dự án của bạn.'}</p>
        </header>
        <form className="login-form" onSubmit={submit} aria-busy={busy}>
          <div className="login-field">
            <label htmlFor="login-email">Địa chỉ email</label>
            <div className="login-input-wrap"><Mail size={18} aria-hidden="true" /><input id="login-email" type="email" required autoFocus placeholder="ban@example.com" value={email} onChange={event => setEmail(event.target.value)} autoComplete="username" disabled={busy} /></div>
          </div>
          <div className="login-field">
            <label htmlFor="login-password">Mật khẩu</label>
            <div className="login-input-wrap"><LockKeyhole size={18} aria-hidden="true" /><input id="login-password" type={showPassword ? 'text' : 'password'} required minLength={state?.setupRequired ? 12 : 1} placeholder={state?.setupRequired ? 'Ít nhất 12 ký tự' : 'Nhập mật khẩu của bạn'} value={password} onChange={event => setPassword(event.target.value)} autoComplete={state?.setupRequired ? 'new-password' : 'current-password'} disabled={busy} /><button className="login-password-toggle" type="button" onClick={() => setShowPassword(value => !value)} aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} aria-pressed={showPassword}>{showPassword ? <EyeOff size={18} /> : <Eye size={18} />}</button></div>
          </div>
          {error && <div className="login-error" role="alert"><CircleAlert size={17} /><span>{error}</span></div>}
          <button className="login-submit" type="submit" disabled={!state || busy}>
            {busy || !state ? <LoaderCircle className="login-spinner" size={18} /> : null}
            <span>{busy ? 'Đang xử lý…' : !state ? 'Đang kết nối…' : state.setupRequired ? 'Tạo tài khoản quản trị' : 'Đăng nhập'}</span>
            {state && !busy && <ArrowRight size={18} />}
          </button>
          {!state && error && <button className="login-retry" type="button" onClick={loadStatus}>Thử kết nối lại</button>}
        </form>
        <div className="login-card-footer"><LockKeyhole size={14} /><span>Truy cập không gian làm việc của bạn</span></div>
      </section>
      <p className="login-caption">Thu thập nội dung. Biên tập theo thương hiệu.</p>
    </div>
  </main>;
}
