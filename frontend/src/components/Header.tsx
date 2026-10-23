import React from 'react';
import { 
  Search, 
  Plus, 
  Bell, 
  ShieldCheck, 
  Cpu 
} from 'lucide-react';

interface HeaderProps {
  currentTab: string;
  onOpenNewTask: () => void;
}

export const Header: React.FC<HeaderProps> = ({ currentTab, onOpenNewTask }) => {
  const getTabTitle = () => {
    switch (currentTab) {
      case 'dashboard':
        return 'Tổng quan Hệ thống Cào';
      case 'fb-crawler':
        return 'Facebook Crawler Studio';
      case 'tasks-queue':
        return 'Tasks & Queue Giám sát';
      case 'articles':
        return 'Kho Bài viết Đã cào';
      case 'pipeline':
        return 'Pipeline Xử lý Dữ liệu';
      default:
        return 'Facebook Crawler Dashboard';
    }
  };

  return (
    <header className="app-header">
      <div className="header-left">
        <div className="breadcrumb">
          <span className="breadcrumb-root">SammiCrawl</span>
          <span className="breadcrumb-separator">/</span>
          <span className="breadcrumb-active">{getTabTitle()}</span>
        </div>
        <h1 className="header-page-title">{getTabTitle()}</h1>
      </div>

      <div className="header-right">
        {/* Search */}
        <div className="header-search">
          <Search size={16} className="search-icon" />
          <input 
            type="text" 
            placeholder="Tìm Fanpage, Group, ID tác vụ..." 
            className="search-input"
          />
        </div>

        {/* Engine Status Badges */}
        <div className="engine-badge stealth">
          <ShieldCheck size={14} />
          <span>Anti-Detect: Bật</span>
        </div>

        <div className="engine-badge backend">
          <Cpu size={14} />
          <span>API .NET: Sẵn sàng</span>
        </div>

        {/* Action Button */}
        <button 
          type="button" 
          className="btn-create-task"
          onClick={onOpenNewTask}
        >
          <Plus size={16} />
          <span>Tạo Task Cào Mới</span>
        </button>

        {/* Bell */}
        <button type="button" className="icon-btn-header" title="Thông báo hệ thống">
          <Bell size={18} />
          <span className="badge-count">0</span>
        </button>

        {/* User profile */}
        <div className="user-profile-badge">
          <div className="user-avatar">SC</div>
          <div className="user-info">
            <span className="user-name">HoangTyy</span>
            <span className="user-role">Administrator</span>
          </div>
        </div>
      </div>
    </header>
  );
};
