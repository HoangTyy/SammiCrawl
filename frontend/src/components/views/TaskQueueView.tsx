import { HelpTooltip } from '../HelpTooltip';
import React, { useState } from 'react';
import { 
  Activity, 
  RotateCw, 
  CheckCircle2, 
  AlertCircle, 
  Clock, 
  Trash2, 
  ExternalLink,
  Plus, 
  Cpu
} from 'lucide-react';
import type { CrawlTask, TaskStatus } from '../../types';
import { useApp } from '../../context/AppContext';

interface TaskQueueViewProps {
  tasks: CrawlTask[];
  onDeleteTask: (id: string) => void;
  onNavigateToStudio: () => void;
}

export const TaskQueueView: React.FC<TaskQueueViewProps> = ({
  tasks,
  onDeleteTask,
  onNavigateToStudio
}) => {
  const { t } = useApp();
  const [activeFilter, setActiveFilter] = useState<string>('all');
  const [searchKey, setSearchKey] = useState<string>('');

  const filteredTasks = tasks.filter(t => {
    if (activeFilter !== 'all' && t.status !== activeFilter) return false;
    if (searchKey && !t.targetName.toLowerCase().includes(searchKey.toLowerCase()) && !t.url.toLowerCase().includes(searchKey.toLowerCase())) {
      return false;
    }
    return true;
  });

  const getStatusBadge = (status: TaskStatus) => {
    switch (status) {
      case 'running':
        return (
          <span className="queue-status-badge running">
            <RotateCw size={12} className="spin-fast" /> {t.queue.filterRunning}
          </span>
        );
      case 'completed':
        return (
          <span className="queue-status-badge completed">
            <CheckCircle2 size={12} /> {t.queue.filterCompleted}
          </span>
        );
      case 'failed':
        return (
          <span className="queue-status-badge failed">
            <AlertCircle size={12} /> {t.queue.filterFailed}
          </span>
        );
      case 'paused': return <span className="queue-status-badge pending"><Clock size={12} /> Tạm dừng</span>;
      case 'pending':
      default:
        return (
          <span className="queue-status-badge pending">
            <Clock size={12} /> {t.queue.filterPending}
          </span>
        );
    }
  };

  return (
    <div className="view-container queue-view"><div className="view-header-strip"><h2 className="view-title">Hàng đợi tác vụ<HelpTooltip text="Theo dõi trạng thái và tiến trình các tác vụ thu thập bài viết." /></h2></div>
      <div className="config-notice">Đây là các lượt cào bài, tách biệt với bản nháp chờ duyệt. Tác vụ tạm dừng sau khi khởi động lại sẽ không tự chạy tiếp.</div>

      {/* Queue Table Card */}
      <div className="cyber-panel queue-table-panel">
        <div className="panel-header-line">
          <div className="panel-title-group">
            <Activity size={16} className="text-cyan" />
            <h3 className="panel-title-text">Lượt cào bài ({tasks.length})</h3>
          </div>

          <div className="queue-controls-right">
            {/* Filter buttons */}
            <div className="cyber-filter-pill-group" role="group" aria-label="Lọc trạng thái tác vụ">
              {[
                { id: 'all', label: t.queue.filterAll },
                { id: 'running', label: t.queue.filterRunning },
                { id: 'pending', label: t.queue.filterPending },
                { id: 'paused', label: 'Tạm dừng' },
                { id: 'completed', label: t.queue.filterCompleted },
                { id: 'failed', label: t.queue.filterFailed }
              ].map(tab => (
                <button
                  key={tab.id}
                  type="button"
                  aria-pressed={activeFilter === tab.id}
                  data-status={tab.id}
                  className={`filter-btn-cyber ${activeFilter === tab.id ? 'active' : ''}`}
                  onClick={() => setActiveFilter(tab.id)}
                >
                  {tab.label} <span className="queue-filter-count">{tab.id === 'all' ? tasks.length : tasks.filter(task => task.status === tab.id).length}</span>
                </button>
              ))}
            </div>

            <input
              type="text"
              aria-label="Tìm tác vụ theo tên hoặc URL"
              placeholder={t.queue.searchPlaceholder}
              value={searchKey}
              onChange={(e) => setSearchKey(e.target.value)}
              className="queue-search-input"
            />

            <button 
              type="button" 
              className="btn-add-mission-sm"
              onClick={onNavigateToStudio}
            >
              <Plus size={14} />
              <span>Cào bài mới</span>
            </button>
          </div>
        </div>

        {/* Empty state or table */}
        {filteredTasks.length === 0 ? (
          <div className="cyber-empty-state">
            <div className="radar-empty-graphic">
              <div className="radar-circle ring-1"></div>
              <div className="radar-circle ring-2"></div>
              <Cpu size={32} className="radar-icon-center" />
            </div>

            <h4 className="empty-heading">{tasks.length ? 'Không có tác vụ phù hợp' : t.queue.emptyHeading}</h4>
            <p className="empty-sub">{tasks.length ? 'Thử đổi trạng thái hoặc từ khóa tìm kiếm.' : t.queue.emptySub}</p>

            <button 
              type="button" 
              className="btn-launch-cyber"
              onClick={onNavigateToStudio}
            >
              <Plus size={15} />
              <span>Tạo lượt cào mới</span>
            </button>
          </div>
        ) : (
          <div className="queue-table-scroll">
            <table className="cyber-table">
              <thead>
                <tr>
                  <th>{t.queue.colTarget}</th>
                  <th>Nguồn</th>
                  <th>Tài nguyên</th>
                  <th>{t.queue.colProgress}</th>
                  <th>Bỏ qua trùng</th>
                  <th>{t.queue.colStatus}</th>
                  <th className="text-right">{t.queue.colActions}</th>
                </tr>
              </thead>
              <tbody>
                {filteredTasks.map(task => (
                  <tr key={task.id}>
                    <td data-label="Mục tiêu Facebook">
                      <div className="table-target-wrap">
                        <span className="target-title-main">{task.targetName}</span>
                        <a 
                          href={task.url} 
                          target="_blank" 
                          rel="noreferrer" 
                          className="target-url-sub"
                        >
                          {task.url} <ExternalLink size={10} />
                        </a>
                        {task.errorReason&&<small className="task-worker-message">{task.errorCode ? `${task.errorCode}: ` : ''}{task.errorReason}</small>}
                      </div>
                    </td>
                    <td data-label="Nguồn">
                      <span className="chip-target-type">{task.targetType.toUpperCase()}</span>
                    </td>
                    <td data-label="Tài nguyên">
                      <span className="worker-node-tag">{task.workerNode}</span>
                    </td>
                    <td data-label="Tiến độ">
                      <div className="progress-cyber-wrap">
                        <div className="progress-cyber-bar">
                          <div 
                            className="progress-cyber-fill"
                            style={{ width: `${task.progress}%` }}
                          ></div>
                        </div>
                        <span className="progress-num">{task.crawledPosts}/{task.maxPosts} ({task.progress}%)</span>
                      </div>
                    </td>
                    <td data-label="Bỏ qua trùng">
                      <span className="latency-text">{task.duplicatesSkipped ?? 0} bài</span>
                    </td>
                    <td data-label="Trạng thái">{getStatusBadge(task.status)}</td>
                    <td data-label="Thao tác" className="text-right">
                      <button 
                        type="button" 
                        className="btn-delete-cyber"
                        onClick={() => onDeleteTask(task.id)}
                        title="Xóa tác vụ"
                      >
                        <Trash2 size={14} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};
