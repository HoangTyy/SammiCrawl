import React from 'react';
import { 
  Sparkles, 
  LayoutDashboard, 
  FileText, 
  GitFork, 
  ThumbsUp, 
  ListOrdered, 
  Bookmark, 
  Download,
  ShieldCheck,
  Activity
} from 'lucide-react';

interface SidebarProps {
  currentTab: string;
  onTabChange: (tab: string) => void;
}

export const Sidebar: React.FC<SidebarProps> = ({ currentTab, onTabChange }) => {
  const navSections = [
    {
      title: 'OVERVIEW',
      items: [
        { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard }
      ]
    },
    {
      title: 'CONTENT',
      items: [
        { id: 'articles', label: 'Articles', icon: FileText },
        { id: 'pipeline', label: 'Pipeline', icon: GitFork }
      ]
    },
    {
      title: 'CRAWLING',
      items: [
        { id: 'fb-crawler', label: 'FB Crawler', icon: ThumbsUp },
        { id: 'tasks-queue', label: 'Tasks & Queue', icon: ListOrdered }
      ]
    },
    {
      title: 'SYSTEM',
      items: [
        { id: 'brands', label: 'Brands', icon: Bookmark },
        { id: 'export-centre', label: 'Export Centre', icon: Download }
      ]
    }
  ];

  return (
    <aside className="sidebar">
      {/* Brand Header matching screenshot */}
      <div className="brand-header">
        <div className="brand-icon-wrapper">
          <Sparkles className="brand-icon" size={20} />
        </div>
        <div className="brand-info">
          <h2 className="brand-title">Brand AI Engine</h2>
          <p className="brand-subtitle">Content Operations</p>
        </div>
      </div>

      {/* Navigation Groups */}
      <nav className="nav-container">
        {navSections.map((section) => (
          <div key={section.title} className="nav-group">
            <h3 className="nav-group-title">{section.title}</h3>
            <ul className="nav-list">
              {section.items.map((item) => {
                const IconComponent = item.icon;
                const isActive = currentTab === item.id;
                return (
                  <li key={item.id}>
                    <button
                      type="button"
                      className={`nav-item-btn ${isActive ? 'active' : ''}`}
                      onClick={() => onTabChange(item.id)}
                    >
                      <IconComponent className="nav-item-icon" size={18} />
                      <span className="nav-item-label">{item.label}</span>
                    </button>
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
      </nav>

      {/* System Status Footer */}
      <div className="sidebar-footer">
        <div className="engine-health-card">
          <div className="engine-health-header">
            <span className="status-dot pulsing"></span>
            <span className="engine-health-title">FB Engine Ready</span>
          </div>
          <p className="engine-health-desc">Puppeteer & Node 10 API Idle</p>
          <div className="engine-meta">
            <span><ShieldCheck size={12} /> Stealth Mode</span>
            <span><Activity size={12} /> 0 Active Worker</span>
          </div>
        </div>
      </div>
    </aside>
  );
};
