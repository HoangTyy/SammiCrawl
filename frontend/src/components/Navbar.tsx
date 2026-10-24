import React from 'react';
import { 
  Radar, 
  LayoutGrid, 
  Terminal, 
  Database, 
  ShieldCheck, 
  Plus, 
  Activity,
  Sun,
  Moon,
  Languages,
  LogOut,
  Radio, Building2, Bookmark, Trash2
} from 'lucide-react';
import type { NavigationTab, EngineTelemetry } from '../types';
import { useApp } from '../context/AppContext';

interface NavbarProps {
  currentTab: NavigationTab;
  onSelectTab: (tab: NavigationTab) => void;
  telemetry: EngineTelemetry;
  onOpenMissionModal: () => void;
  onLogout: () => Promise<void>;
  loggingOut: boolean;
}

export const Navbar: React.FC<NavbarProps> = ({
  currentTab,
  onSelectTab,
  onOpenMissionModal,
  onLogout,
  loggingOut
}) => {
  const { theme, toggleTheme, language, toggleLanguage, t } = useApp();

  const groups = [
    { label: 'Tổng quan', icon: LayoutGrid, tabs: [{ id: 'dashboard', label: 'Báo cáo tổng quan', icon: LayoutGrid }] },
    { label: 'Thu thập', icon: Terminal, tabs: [
      { id: 'crawler-studio', label: 'Cào bài', icon: Terminal },
      { id: 'queue', label: 'Hàng đợi tác vụ', icon: Activity },
      { id: 'crawl-presets', label: 'Mẫu cấu hình cào bài', icon: Bookmark }
    ] },
    { label: 'Nội dung', icon: Database, tabs: [
      { id: 'data-vault', label: 'Bài viết & duyệt đăng', icon: Database },
      { id: 'channels', label: 'Kênh đăng', icon: Radio }
    ] },
    { label: 'Quản lý', icon: Building2, tabs: [
      { id: 'proxy-stealth', label: 'Thương hiệu', icon: Building2 },
      { id: 'facebook-accounts', label: 'Tài khoản Facebook', icon: ShieldCheck }
    ] },
    { label: 'Thùng rác', icon: Trash2, tabs: [{ id: 'trash', label: 'Thùng rác', icon: Trash2 }] }
  ] satisfies { label: string; icon: typeof LayoutGrid; tabs: { id: NavigationTab; label: string; icon: typeof LayoutGrid }[] }[];
  const activeGroup = groups.find(group => group.tabs.some(tab => tab.id === currentTab)) ?? groups[0];

  return (
    <header className="cyber-navbar">
      <div className="navbar-left">
        <div className="brand-badge-cyber">
          <div className="brand-logo-hex">
            <Radar size={20} className="brand-logo-icon" />
          </div>
          <div className="brand-titles">
            <div className="brand-name-row">
              <span className="brand-main-title">SAMMICRAWL</span>
              <span className="brand-tag-spec">CORE // v1.0</span>
            </div>
            <span className="brand-sub-title">{t.navbar.brandSub}</span>
          </div>
        </div>
      </div>

      <div className="app-navigation">
        <nav className="workspace-nav-groups" aria-label="Nhóm công cụ">
          {groups.map(group => {
            const Icon = group.icon;
            const active = group === activeGroup;
            return <button key={group.label} type="button" className={`workspace-group-button ${active ? 'active' : ''}`} aria-pressed={active} onClick={() => { if (!active) onSelectTab(group.tabs[0].id); }}><Icon size={16} aria-hidden="true"/><span>{group.label}</span></button>;
          })}
        </nav>
        {activeGroup.tabs.length > 1 && <nav className="workspace-nav-pages" aria-label={`Công cụ ${activeGroup.label.toLowerCase()}`}>
          {activeGroup.tabs.map(tab => {
            const Icon = tab.icon;
            return <button key={tab.id} type="button" className={`workspace-page-button ${currentTab === tab.id ? 'active' : ''}`} aria-current={currentTab === tab.id ? 'page' : undefined} onClick={() => onSelectTab(tab.id)}><Icon size={15} aria-hidden="true"/>{tab.label}</button>;
          })}
        </nav>}
      </div>

      <div className="navbar-right">
        {/* Language Switcher */}
        <button
          type="button"
          className="btn-toggle-switch"
          onClick={toggleLanguage}
          title={language === 'vi' ? 'Switch to English' : 'Chuyển sang Tiếng Việt'}
        >
          <Languages size={15} />
          <span className="lang-code">{language.toUpperCase()}</span>
        </button>

        {/* Theme Switcher */}
        <button
          type="button"
          className="btn-toggle-switch"
          onClick={toggleTheme}
          title={theme === 'dark' ? t.navbar.lightMode : t.navbar.darkMode}
        >
          {theme === 'dark' ? <Sun size={16} className="sun-icon" /> : <Moon size={16} className="moon-icon" />}
        </button>

        <button
          type="button"
          className="btn-toggle-switch"
          onClick={() => void onLogout()}
          disabled={loggingOut}
          title="Đăng xuất"
        >
          <LogOut size={16} />
          <span>{loggingOut ? 'Đang đăng xuất…' : 'Đăng xuất'}</span>
        </button>

        {/* Action Button */}
        <button
          type="button"
          className="btn-launch-cyber"
          onClick={onOpenMissionModal}
        >
          <Plus size={15} />
          <span>Cào bài mới</span>
        </button>
      </div>
    </header>
  );
};
