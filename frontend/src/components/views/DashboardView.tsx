import { ContentReport } from '../ContentReport';
import React, { useState } from 'react';
import { 
  ArrowUpRight, 
  Send, 
  Copy, 
  Check, 
  Activity,
  Sparkles
} from 'lucide-react';
import type { EngineTelemetry, TargetType } from '../../types';
import { useApp } from '../../context/AppContext';

interface DashboardViewProps {
  telemetry: EngineTelemetry;
  logs: string[];
  onLaunchTarget: (url: string, targetType: TargetType, maxPosts: number) => void;
  onNavigateToStudio: () => void;
}

export const DashboardView: React.FC<DashboardViewProps> = ({
  telemetry,
  logs,
  onLaunchTarget,
  onNavigateToStudio
}) => {
  const { t } = useApp();
  const [quickUrl, setQuickUrl] = useState('');

  const [copied, setCopied] = useState(false);

  const handleQuickSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!quickUrl.trim()) return;
    onLaunchTarget(quickUrl, 'page', 25);
    setQuickUrl('');
  };

  const handleCopyLogs = () => {
    navigator.clipboard.writeText(logs.join('\n'));
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="view-container dashboard-view">
      <ContentReport />

      {/* Main Grid: Launcher + Cluster Nodes + Terminal */}
      <div className="dashboard-grid-layout">
        {/* Left Column: Quick Launcher & Cluster Nodes */}
        <div className="dash-left-col">
          {/* Quick Mission Trigger */}
          <div className="cyber-panel launcher-panel">
            <div className="panel-header-line">
              <div className="panel-title-group">
                <Sparkles size={16} className="text-cyan" />
                <h3 className="panel-title-text">{t.dashboard.quickTitle}</h3>
              </div>
              <button 
                type="button" 
                className="btn-ghost-cyan" 
                onClick={onNavigateToStudio}
              >
                <span>{t.dashboard.quickStudioMode}</span>
                <ArrowUpRight size={14} />
              </button>
            </div>

            <p className="panel-desc-text">
              {t.dashboard.quickDesc}
            </p>

            <form onSubmit={handleQuickSubmit} className="quick-dispatch-form">
              <div className="dispatch-input-row">
                <input
                  type="url"
                  placeholder={t.dashboard.urlPlaceholder}
                  value={quickUrl}
                  onChange={(e) => setQuickUrl(e.target.value)}
                  className="cyber-input"
                  required
                />
                <button type="submit" className="btn-dispatch-submit">
                  <Send size={15} />
                  <span>{t.dashboard.btnDeploy}</span>
                </button>
              </div>

            </form>
          </div>

          {/* Cluster Nodes Map */}
          <div className="cyber-panel nodes-panel">
            <div className="panel-header-line">
              <div className="panel-title-group">
                <Activity size={16} className="text-emerald" />
                <h3 className="panel-title-text">Thành phần xử lý</h3>
              </div>
              <span className="tag-cluster-online">Worker local</span>
            </div>

            <div className="nodes-grid">
              {[
                { name: 'Facebook local', engine: 'Thu thập và chống trùng', status: telemetry.activeTargets > 0 ? 'Đang chạy' : 'Sẵn sàng', load: '—' },
                { name: 'Media', engine: 'Ảnh feed / video HTTP', status: 'Có giới hạn', load: '—' },
                { name: 'Clef', engine: 'Đánh giá sau thu thập', status: 'Chạy tuần tự', load: '—' },
                { name: 'Xuất bản', engine: 'Đăng Facebook', status: 'Chưa triển khai', load: '—' }
              ].map((node, i) => (
                <div key={i} className="node-tile">
                  <div className="node-tile-top">
                    <span className="node-dot idle"></span>
                    <span className="node-name">{node.name}</span>
                  </div>
                  <span className="node-engine">{node.engine}</span>
                  <div className="node-footer">
                    <span className="node-status">{node.status}</span>
                    <span className="node-load">{node.load}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Right Column: Live Terminal Stream */}
        <div className="dash-right-col">
          <div className="cyber-panel terminal-panel">
            <div className="terminal-header-bar">
              <div className="terminal-dots">
                <span className="dot red"></span>
                <span className="dot yellow"></span>
                <span className="dot green"></span>
              </div>
              <span className="terminal-title">{t.dashboard.terminalTitle}</span>
              <button 
                type="button" 
                className="btn-copy-terminal"
                onClick={handleCopyLogs}
              >
                {copied ? <Check size={12} /> : <Copy size={12} />}
                <span>{copied ? t.dashboard.copied : t.dashboard.copy}</span>
              </button>
            </div>

            <div className="terminal-body-screen">
              {logs.map((log, i) => (
                <div key={i} className="terminal-log-row">
                  <span className="log-marker">&gt;&gt;</span>
                  <span className="log-text">{log}</span>
                </div>
              ))}
              <div className="terminal-cursor-line">
                <span className="terminal-prompt">$</span>
                <span className="terminal-blink">_</span>
              </div>
            </div>

            <div className="terminal-footer-meta">
              <span>{telemetry.systemHealth > 0 ? 'API đã kết nối' : 'API chưa kết nối'}</span>
              <span>{t.dashboard.memory} —</span>
              <span>{t.dashboard.proxyRotation} 0</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
