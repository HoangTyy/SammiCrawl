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

  const tabs = [
    { id: 'dashboard' as NavigationTab, label: 'Báo cáo tổng quan', icon: LayoutGrid },
    { id: 'crawler-studio' as NavigationTab, label: 'Cào bài', icon: Terminal },
    { id: 'queue' as NavigationTab, label: 'Hàng đợi tác vụ', icon: Activity },
    { id: 'data-vault' as NavigationTab, label: 'Bài viết & duyệt đăng', icon: Database },
    { id: 'channels' as NavigationTab, label: 'Kênh đăng', icon: Radio },
    { id: 'facebook-accounts' as NavigationTab, label: 'Tài khoản Facebook', icon: ShieldCheck },
    { id: 'proxy-stealth' as NavigationTab, label: 'Thương hiệu', icon: Building2 },
    { id: 'crawl-presets' as NavigationTab, label: 'Mẫu cấu hình cào bài', icon: Bookmark },
    { id: 'trash' as NavigationTab, label: 'Thùng rác', icon: Trash2 }
  ];

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

      <nav className="navbar-center-tabs">
        {tabs.map(tab => {
          const Icon = tab.icon;
          const isActive = currentTab === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              className={`cyber-tab-btn ${isActive ? 'active' : ''}`}
              onClick={() => onSelectTab(tab.id)}
            >
              <Icon size={15} className="tab-icon" />
              <span className="tab-label">{tab.label}</span>
              {isActive && <div className="tab-active-glow"></div>}
            </button>
          );
        })}
      </nav>

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
          <span>{t.navbar.launchMission}</span>
        </button>
      </div>
    </header>
  );
};
