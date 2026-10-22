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
  ArrowRight,
  Cpu
} from 'lucide-react';
import type { CrawlTask, TaskStatus } from '../../types';

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
            <RotateCw size={12} className="spin-fast" /> Crawling
          </span>
        );
      case 'completed':
        return (
          <span className="queue-status-badge completed">
            <CheckCircle2 size={12} /> Stored
          </span>
        );
      case 'failed':
        return (
          <span className="queue-status-badge failed">
            <AlertCircle size={12} /> Checkpoint
          </span>
        );
      case 'pending':
      default:
        return (
          <span className="queue-status-badge pending">
            <Clock size={12} /> In Queue
          </span>
        );
    }
  };

  return (
    <div className="view-container queue-view">
      {/* Pipeline Visualizer Strip */}
      <div className="pipeline-strip-card">
        <div className="pipeline-header">
          <span className="pipeline-title">KIẾN TRÚC LUỒNG XỬ LÝ (DATA PIPELINE TOPOLOGY)</span>
          <span className="pipeline-tag">Auto-Balanced</span>
        </div>

        <div className="pipeline-steps-row">
          <div className="pipeline-step-box active">
            <div className="step-num">01</div>
            <div className="step-details">
              <span className="step-name">Dispatcher</span>
              <span className="step-sub">Nhận URL & Phân bổ</span>
            </div>
          </div>
          <ArrowRight size={16} className="pipeline-arrow" />

          <div className="pipeline-step-box active">
            <div className="step-num">02</div>
            <div className="step-details">
              <span className="step-name">Stealth Browser</span>
              <span className="step-sub">Evasion & Auto-Scroll</span>
            </div>
          </div>
          <ArrowRight size={16} className="pipeline-arrow" />

          <div className="pipeline-step-box active">
            <div className="step-num">03</div>
            <div className="step-details">
              <span className="step-name">DOM Parser</span>
              <span className="step-sub">Bóc bài & comments</span>
            </div>
          </div>
          <ArrowRight size={16} className="pipeline-arrow" />

          <div className="pipeline-step-box">
            <div className="step-num">04</div>
            <div className="step-details">
              <span className="step-name">AI Cleaning</span>
              <span className="step-sub">Lọc trùng & Spam</span>
            </div>
          </div>
          <ArrowRight size={16} className="pipeline-arrow" />

          <div className="pipeline-step-box">
            <div className="step-num">05</div>
            <div className="step-details">
              <span className="step-name">.NET 10 Storage</span>
              <span className="step-sub">EF Core Database Sync</span>
            </div>
          </div>
        </div>
      </div>

      {/* Queue Table Card */}
      <div className="cyber-panel queue-table-panel">
        <div className="panel-header-line">
          <div className="panel-title-group">
            <Activity size={16} className="text-cyan" />
            <h3 className="panel-title-text">Hàng đợi Tác vụ Real-time ({tasks.length})</h3>
          </div>

          <div className="queue-controls-right">
            {/* Filter buttons */}
            <div className="cyber-filter-pill-group">
              {[
                { id: 'all', label: 'Tất cả' },
                { id: 'running', label: 'Đang chạy' },
                { id: 'pending', label: 'Chờ' },
                { id: 'completed', label: 'Xong' },
                { id: 'failed', label: 'Lỗi' }
              ].map(tab => (
                <button
                  key={tab.id}
                  type="button"
                  className={`filter-btn-cyber ${activeFilter === tab.id ? 'active' : ''}`}
                  onClick={() => setActiveFilter(tab.id)}
                >
                  {tab.label}
                </button>
              ))}
            </div>

            <input
              type="text"
              placeholder="Lọc ID / Tên mục tiêu..."
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
              <span>Tạo Mission</span>
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

            <h4 className="empty-heading">Hàng đợi điều phối rỗng (Zero Tasks in Queue)</h4>
            <p className="empty-sub">
              Hiện tại không có tác vụ nào đang chiếm dụng tài nguyên Worker Node. 
              Các tiến trình Chromium Headless đang ở trạng thái ngủ (Standby) sẵn sàng nhận lệnh.
            </p>

            <button 
              type="button" 
              className="btn-launch-cyber"
              onClick={onNavigateToStudio}
            >
              <Plus size={15} />
              <span>Khởi tạo Mission cào mới từ Studio</span>
            </button>
          </div>
        ) : (
          <div className="queue-table-scroll">
            <table className="cyber-table">
              <thead>
                <tr>
                  <th>Mục tiêu Facebook</th>
                  <th>Phân loại</th>
                  <th>Worker Node</th>
                  <th>Tiến độ</th>
                  <th>Độ trễ</th>
                  <th>Trạng thái</th>
                  <th className="text-right">Quản trị</th>
                </tr>
              </thead>
              <tbody>
                {filteredTasks.map(task => (
                  <tr key={task.id}>
                    <td>
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
                      </div>
                    </td>
                    <td>
                      <span className="chip-target-type">{task.targetType.toUpperCase()}</span>
                    </td>
                    <td>
                      <span className="worker-node-tag">{task.workerNode}</span>
                    </td>
                    <td>
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
                    <td>
                      <span className="latency-text">{task.latencyMs}ms</span>
                    </td>
                    <td>{getStatusBadge(task.status)}</td>
                    <td className="text-right">
                      <button 
                        type="button" 
                        className="btn-delete-cyber"
                        onClick={() => onDeleteTask(task.id)}
                        title="Hủy tác vụ"
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
