import React, { useState } from 'react';
import { 
  Inbox, 
  Plus, 
  RotateCw, 
  CheckCircle2, 
  Clock, 
  AlertCircle, 
  Trash2, 
  ExternalLink,
  Bot,
  Search
} from 'lucide-react';
import { FacebookIcon } from './FacebookIcon';
import type { CrawlTask, TaskStatus } from '../types';

interface TaskQueueSectionProps {
  tasks: CrawlTask[];
  onOpenNewTask: () => void;
  onDeleteTask: (id: string) => void;
}

export const TaskQueueSection: React.FC<TaskQueueSectionProps> = ({ 
  tasks, 
  onOpenNewTask,
  onDeleteTask 
}) => {
  const [filterStatus, setFilterStatus] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState<string>('');

  const filteredTasks = tasks.filter(task => {
    if (filterStatus !== 'all' && task.status !== filterStatus) return false;
    if (searchQuery && !task.url.toLowerCase().includes(searchQuery.toLowerCase()) && !task.targetName.toLowerCase().includes(searchQuery.toLowerCase())) {
      return false;
    }
    return true;
  });

  const getStatusBadge = (status: TaskStatus) => {
    switch (status) {
      case 'running':
        return (
          <span className="task-status-badge running">
            <RotateCw size={12} className="spin-icon" /> Đang cào
          </span>
        );
      case 'completed':
        return (
          <span className="task-status-badge completed">
            <CheckCircle2 size={12} /> Hoàn thành
          </span>
        );
      case 'failed':
        return (
          <span className="task-status-badge failed">
            <AlertCircle size={12} /> Thất bại
          </span>
        );
      case 'pending':
      default:
        return (
          <span className="task-status-badge pending">
            <Clock size={12} /> Đang chờ
          </span>
        );
    }
  };

  return (
    <div className="task-queue-card">
      <div className="task-queue-header">
        <div className="task-queue-title-wrap">
          <h2 className="task-queue-title">Hàng đợi Tác vụ & Tiến trình Cào</h2>
          <span className="task-count-pill">{tasks.length} Tác vụ</span>
        </div>

        <div className="task-filter-bar">
          {/* Status Tabs */}
          <div className="filter-tabs">
            {[
              { id: 'all', label: 'Tất cả' },
              { id: 'running', label: 'Đang chạy' },
              { id: 'pending', label: 'Chờ xử lý' },
              { id: 'completed', label: 'Hoàn thành' },
              { id: 'failed', label: 'Lỗi' }
            ].map(tab => (
              <button
                key={tab.id}
                type="button"
                className={`filter-tab-btn ${filterStatus === tab.id ? 'active' : ''}`}
                onClick={() => setFilterStatus(tab.id)}
              >
                {tab.label}
              </button>
            ))}
          </div>

          <div className="filter-search-wrap">
            <Search size={14} className="filter-search-icon" />
            <input
              type="text"
              placeholder="Lọc theo tên / URL..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="filter-search-input"
            />
          </div>

          <button 
            type="button" 
            className="btn-create-task-sm"
            onClick={onOpenNewTask}
          >
            <Plus size={14} />
            <span>Thêm Task</span>
          </button>
        </div>
      </div>

      {/* Main Content: Empty State or Table */}
      {filteredTasks.length === 0 ? (
        <div className="empty-state-box">
          <div className="empty-state-icon-wrapper">
            <div className="empty-icon-glow"></div>
            <Inbox className="empty-state-icon" size={40} />
          </div>

          <h3 className="empty-state-title">Chưa có tiến trình cào dữ liệu nào</h3>
          <p className="empty-state-desc">
            Hàng đợi hiện đang trống. Hãy nhập một liên kết Fanpage, Group hoặc Profile Facebook 
            vào thanh công cụ phía trên để khởi động phiên cào dữ liệu đầu tiên.
          </p>

          <div className="empty-state-actions">
            <button 
              type="button" 
              className="btn-empty-create"
              onClick={onOpenNewTask}
            >
              <Plus size={16} />
              <span>Khởi tạo Task đầu tiên</span>
            </button>
          </div>

          <div className="empty-state-features">
            <div className="feature-tip-item">
              <Bot size={15} className="feature-tip-icon" />
              <span>Stealth Browser né bot checkpoint</span>
            </div>
            <div className="feature-tip-item">
              <FacebookIcon size={15} className="feature-tip-icon" />
              <span>Tự động phân tích cấu trúc bài viết & comments</span>
            </div>
            <div className="feature-tip-item">
              <RotateCw size={15} className="feature-tip-icon" />
              <span>Đồng bộ tức thời sang Backend .NET 10 Web API</span>
            </div>
          </div>
        </div>
      ) : (
        <div className="tasks-table-wrapper">
          <table className="tasks-table">
            <thead>
              <tr>
                <th>Mục tiêu Facebook</th>
                <th>Phân loại</th>
                <th>Tiến độ</th>
                <th>Trạng thái</th>
                <th>Thời gian</th>
                <th className="text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {filteredTasks.map(task => (
                <tr key={task.id}>
                  <td>
                    <div className="task-target-cell">
                      <FacebookIcon size={16} className="fb-cell-icon" />
                      <div className="task-target-info">
                        <span className="task-target-name">{task.targetName}</span>
                        <a 
                          href={task.url} 
                          target="_blank" 
                          rel="noreferrer" 
                          className="task-target-url"
                        >
                          {task.url} <ExternalLink size={11} />
                        </a>
                      </div>
                    </div>
                  </td>
                  <td>
                    <span className="target-type-badge">
                      {task.targetType.toUpperCase()}
                    </span>
                  </td>
                  <td>
                    <div className="progress-cell">
                      <div className="progress-bar-bg">
                        <div 
                          className="progress-bar-fill"
                          style={{ width: `${task.progress}%` }}
                        ></div>
                      </div>
                      <span className="progress-text">{task.crawledPosts}/{task.maxPosts} bài ({task.progress}%)</span>
                    </div>
                  </td>
                  <td>{getStatusBadge(task.status)}</td>
                  <td className="task-date-cell">{task.createdAt}</td>
                  <td className="text-right">
                    <button 
                      type="button"
                      className="btn-action-icon delete"
                      title="Xóa tác vụ"
                      onClick={() => onDeleteTask(task.id)}
                    >
                      <Trash2 size={15} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};
