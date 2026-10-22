import React from 'react';
import { 
  Radar, 
  LayoutGrid, 
  Terminal, 
  Database, 
  Wand2, 
  ShieldCheck, 
  Plus, 
  Activity 
} from 'lucide-react';
import type { NavigationTab, EngineTelemetry } from '../types';

interface NavbarProps {
  currentTab: NavigationTab;
  onSelectTab: (tab: NavigationTab) => void;
  telemetry: EngineTelemetry;
  onOpenMissionModal: () => void;
}

export const Navbar: React.FC<NavbarProps> = ({
  currentTab,
  onSelectTab,
  telemetry,
  onOpenMissionModal
}) => {
  const tabs = [
    { id: 'dashboard' as NavigationTab, label: 'Dashboard', icon: LayoutGrid },
    { id: 'crawler-studio' as NavigationTab, label: 'Crawl Studio', icon: Terminal },
    { id: 'queue' as NavigationTab, label: 'Hàng đợi (Queue)', icon: Activity },
    { id: 'data-vault' as NavigationTab, label: 'Kho Dữ liệu', icon: Database },
    { id: 'ai-rewriter' as NavigationTab, label: 'AI Rewriter', icon: Wand2 },
    { id: 'proxy-stealth' as NavigationTab, label: 'Proxy & Stealth', icon: ShieldCheck }
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
            <span className="brand-sub-title">Facebook Neural Data Engine</span>
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
        {/* Telemetry pill */}
        <div className="telemetry-pill">
          <span className="telemetry-dot pulse-emerald"></span>
          <span className="telemetry-text">.NET 10 API // {telemetry.apiLatencyMs}ms</span>
        </div>

        {/* Nodes pill */}
        <div className="telemetry-pill nodes">
          <span className="telemetry-text">Cluster: {telemetry.workerNodesOnline} Nodes Ready</span>
        </div>

        {/* Action Button */}
        <button
          type="button"
          className="btn-launch-cyber"
          onClick={onOpenMissionModal}
        >
          <Plus size={15} />
          <span>Launch Mission</span>
        </button>
      </div>
    </header>
  );
};
