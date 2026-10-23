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
      {/* Top Bento Metrics */}
      <div className="bento-metrics-row">
        {/* Metric 1 */}
        <div className="bento-stat-card card-cyan">
          <div className="stat-card-header">
            <span className="stat-label">{t.dashboard.statTargets}</span>
            <div className="stat-icon-wrap cyan">
              <Crosshair size={16} />
            </div>
          </div>
          <div className="stat-value-group">
            <span className="stat-number">{telemetry.activeTargets}</span>
            <span className="stat-badge cyan">{t.dashboard.zeroTargets}</span>
          </div>
          <span className="stat-footnote">{t.dashboard.statTargetsFootnote}</span>
        </div>

        {/* Metric 2 */}
        <div className="bento-stat-card card-emerald">
          <div className="stat-card-header">
            <span className="stat-label">{t.dashboard.statCaptured}</span>
            <div className="stat-icon-wrap emerald">
              <Globe2 size={16} />
            </div>
          </div>
          <div className="stat-value-group">
            <span className="stat-number">{telemetry.postsCaptured}</span>
            <span className="stat-badge emerald">{t.dashboard.zeroCaptured}</span>
          </div>
          <span className="stat-footnote">{t.dashboard.statCapturedFootnote}</span>
        </div>

        {/* Metric 3 */}
        <div className="bento-stat-card card-indigo">
          <div className="stat-card-header">
            <span className="stat-label">{t.dashboard.statQueue}</span>
            <div className="stat-icon-wrap indigo">
              <Layers size={16} />
            </div>
          </div>
          <div className="stat-value-group">
            <span className="stat-number">{telemetry.queueDepth}</span>
            <span className="stat-badge indigo">{t.dashboard.queueIdle}</span>
          </div>
          <span className="stat-footnote">{t.dashboard.statQueueFootnote}</span>
        </div>

        {/* Metric 4 */}
        <div className="bento-stat-card card-amber">
          <div className="stat-card-header">
            <span className="stat-label">{t.dashboard.statStealth}</span>
            <div className="stat-icon-wrap amber">
              <Cpu size={16} />
            </div>
          </div>
          <div className="stat-value-group">
            <span className="stat-number">{telemetry.systemHealth}%</span>
            <span className="stat-badge amber">{t.dashboard.stealthReady}</span>
          </div>
          <span className="stat-footnote">{t.dashboard.statStealthFootnote}</span>
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
                <h3 className="panel-title-text">{t.dashboard.clusterTopology}</h3>
              </div>
              <span className="tag-cluster-online">{t.dashboard.nodesOnline}</span>
            </div>

            <div className="nodes-grid">
              {[
                { name: 'Worker-Alpha', engine: 'Thu thập Facebook', status: 'Chưa triển khai', load: '0%' },
                { name: 'Worker-Beta', engine: 'Thu thập Facebook', status: 'Chưa triển khai', load: '0%' },
                { name: 'Worker-Gamma', engine: 'Lọc bài bằng Clef', status: 'Chưa triển khai', load: '0%' },
                { name: 'Worker-Delta', engine: 'Tải ảnh và video', status: 'Chưa triển khai', load: '0%' }
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
