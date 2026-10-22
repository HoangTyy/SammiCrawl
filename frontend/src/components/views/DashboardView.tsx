import React, { useState } from 'react';
import { 
  Crosshair, 
  Layers, 
  Cpu, 
  ArrowUpRight, 
  Send, 
  Copy, 
  Check, 
  Activity,
  Globe2,
  Sparkles
} from 'lucide-react';
import type { EngineTelemetry, TargetType } from '../../types';

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
  const [quickUrl, setQuickUrl] = useState('');
  const [selectedType, setSelectedType] = useState<TargetType>('page');
  const [copied, setCopied] = useState(false);

  const handleQuickSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!quickUrl.trim()) return;
    onLaunchTarget(quickUrl, selectedType, 25);
    setQuickUrl('');
  };

  const handleCopyLogs = () => {
    navigator.clipboard.writeText(logs.join('\n'));
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="view-container dashboard-view">
      {/* Top Bento Metrics */}
      <div className="bento-metrics-row">
        {/* Metric 1 */}
        <div className="bento-stat-card card-cyan">
          <div className="stat-card-header">
            <span className="stat-label">Mục tiêu giám sát</span>
            <div className="stat-icon-wrap cyan">
              <Crosshair size={16} />
            </div>
          </div>
          <div className="stat-value-group">
            <span className="stat-number">{telemetry.activeTargets}</span>
            <span className="stat-badge cyan">Zero Targets</span>
          </div>
          <span className="stat-footnote">Chưa có Fanpage / Group nào được gán</span>
        </div>

        {/* Metric 2 */}
        <div className="bento-stat-card card-emerald">
          <div className="stat-card-header">
            <span className="stat-label">Dữ liệu thu nạp</span>
            <div className="stat-icon-wrap emerald">
              <Globe2 size={16} />
            </div>
          </div>
          <div className="stat-value-group">
            <span className="stat-number">{telemetry.postsCaptured}</span>
            <span className="stat-badge emerald">0 Posts / 0 Comments</span>
          </div>
          <span className="stat-footnote">Sẵn sàng nhận payload từ Puppeteer Node</span>
        </div>

        {/* Metric 3 */}
        <div className="bento-stat-card card-indigo">
          <div className="stat-card-header">
            <span className="stat-label">Hàng đợi điều phối</span>
            <div className="stat-icon-wrap indigo">
              <Layers size={16} />
            </div>
          </div>
          <div className="stat-value-group">
            <span className="stat-number">{telemetry.queueDepth}</span>
            <span className="stat-badge indigo">Queue Idle</span>
          </div>
          <span className="stat-footnote">0 tác vụ đang chờ phân bổ CPU</span>
        </div>

        {/* Metric 4 */}
        <div className="bento-stat-card card-amber">
          <div className="stat-card-header">
            <span className="stat-label">Trạng thái Stealth Node</span>
            <div className="stat-icon-wrap amber">
              <Cpu size={16} />
            </div>
          </div>
          <div className="stat-value-group">
            <span className="stat-number">{telemetry.systemHealth}%</span>
            <span className="stat-badge amber">100% Anti-Ban Ready</span>
          </div>
          <span className="stat-footnote">Chromium User-Agent Rotator đã nạp</span>
        </div>
      </div>

      {/* Main Grid: Launcher + Cluster Nodes + Terminal */}
      <div className="dashboard-grid-layout">
        {/* Left Column: Quick Launcher & Cluster Nodes */}
        <div className="dash-left-col">
          {/* Quick Mission Trigger */}
          <div className="cyber-panel launcher-panel">
            <div className="panel-header-line">
              <div className="panel-title-group">
                <Sparkles size={16} className="text-cyan" />
                <h3 className="panel-title-text">Quick Crawl Dispatcher</h3>
              </div>
              <button 
                type="button" 
                className="btn-ghost-cyan" 
                onClick={onNavigateToStudio}
              >
                <span>Chế độ Studio Nâng cao</span>
                <ArrowUpRight size={14} />
              </button>
            </div>

            <p className="panel-desc-text">
              Dán đường dẫn Facebook để hệ thống phân bổ tác vụ tự động vào Cluster Crawler Node.
            </p>

            <form onSubmit={handleQuickSubmit} className="quick-dispatch-form">
              <div className="dispatch-input-row">
                <input
                  type="url"
                  placeholder="https://www.facebook.com/vietnamnet.vn..."
                  value={quickUrl}
                  onChange={(e) => setQuickUrl(e.target.value)}
                  className="cyber-input"
                  required
                />
                <button type="submit" className="btn-dispatch-submit">
                  <Send size={15} />
                  <span>Kích hoạt</span>
                </button>
              </div>

              <div className="dispatch-chips-row">
                <span className="chips-title">Loại mục tiêu:</span>
                {(['page', 'group', 'profile'] as TargetType[]).map(type => (
                  <button
                    key={type}
                    type="button"
                    className={`chip-btn ${selectedType === type ? 'active' : ''}`}
                    onClick={() => setSelectedType(type)}
                  >
                    {type === 'page' ? 'Fanpage' : type === 'group' ? 'Nhóm (Group)' : 'Profile'}
                  </button>
                ))}
              </div>
            </form>
          </div>

          {/* Cluster Nodes Map */}
          <div className="cyber-panel nodes-panel">
            <div className="panel-header-line">
              <div className="panel-title-group">
                <Activity size={16} className="text-emerald" />
                <h3 className="panel-title-text">Crawler Cluster Nodes (Worker Topology)</h3>
              </div>
              <span className="tag-cluster-online">4 Nodes Online</span>
            </div>

            <div className="nodes-grid">
              {[
                { name: 'Worker-Alpha', engine: 'Chromium Stealth', status: 'Idle', load: '0%' },
                { name: 'Worker-Beta', engine: 'Chromium Stealth', status: 'Idle', load: '0%' },
                { name: 'Worker-Gamma', engine: 'API Scraper', status: 'Idle', load: '0%' },
                { name: 'Worker-Delta', engine: 'Media Extractor', status: 'Idle', load: '0%' }
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
              <span className="terminal-title">SAMMICRAWL // LIVE TELEMETRY LOGS</span>
              <button 
                type="button" 
                className="btn-copy-terminal"
                onClick={handleCopyLogs}
              >
                {copied ? <Check size={12} /> : <Copy size={12} />}
                <span>{copied ? 'Copied' : 'Copy'}</span>
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
              <span>Bridge: .NET 10 API Connected</span>
              <span>Memory: 42MB</span>
              <span>Proxy Rotation: 0 Active</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
