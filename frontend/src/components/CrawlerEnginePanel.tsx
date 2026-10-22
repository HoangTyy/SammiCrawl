import React, { useState } from 'react';
import { 
  Terminal, 
  Cpu, 
  ShieldAlert, 
  Check, 
  Copy 
} from 'lucide-react';

interface CrawlerEnginePanelProps {
  logs: string[];
}

export const CrawlerEnginePanel: React.FC<CrawlerEnginePanelProps> = ({ logs }) => {
  const [copied, setCopied] = useState(false);

  const handleCopyLogs = () => {
    navigator.clipboard.writeText(logs.join('\n'));
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="engine-side-panel">
      {/* Engine Status Card */}
      <div className="panel-card">
        <div className="panel-card-header">
          <div className="panel-title-wrap">
            <Cpu size={16} className="text-purple" />
            <h3 className="panel-card-title">Cấu hình Engine Crawler</h3>
          </div>
          <span className="badge-active">Online</span>
        </div>

        <div className="config-items-list">
          <div className="config-item">
            <span className="config-label">Trình duyệt giả lập</span>
            <span className="config-value">Chromium Headless (Stealth)</span>
          </div>

          <div className="config-item">
            <span className="config-label">Tần số cuộn trang (Scroll)</span>
            <span className="config-value">Mô phỏng Human (3-6s)</span>
          </div>

          <div className="config-item">
            <span className="config-label">Proxy Pool</span>
            <span className="config-value text-amber">0 IP (Chế độ IP Trực tiếp)</span>
          </div>

          <div className="config-item">
            <span className="config-label">Backend API Bridge</span>
            <span className="config-value text-emerald">ASP.NET Core (.NET 10)</span>
          </div>

          <div className="config-item">
            <span className="config-label">Cơ chế lưu trữ</span>
            <span className="config-value">SQL Server / MySQL Ready</span>
          </div>
        </div>

        <div className="proxy-notice-box">
          <ShieldAlert size={16} className="notice-icon" />
          <div className="notice-text">
            <strong>Lưu ý về Checkpoint:</strong> Khi cào khối lượng lớn (&gt; 500 bài), bạn nên cấu hình xoay IP Proxy và cookies phụ để tránh bị Facebook hạn chế.
          </div>
        </div>
      </div>

      {/* Terminal Live Stream */}
      <div className="panel-card console-card">
        <div className="panel-card-header">
          <div className="panel-title-wrap">
            <Terminal size={16} className="text-purple" />
            <h3 className="panel-card-title">Nhật ký Hệ thống (Real-time Logs)</h3>
          </div>
          <button 
            type="button" 
            className="btn-copy-log" 
            onClick={handleCopyLogs}
            title="Sao chép nhật ký"
          >
            {copied ? <Check size={13} /> : <Copy size={13} />}
            <span>{copied ? 'Đã chép' : 'Copy'}</span>
          </button>
        </div>

        <div className="console-stream-box">
          {logs.map((log, index) => (
            <div key={index} className="log-line">
              <span className="log-prefix">{'>'}</span>
              <span className="log-content">{log}</span>
            </div>
          ))}
          <div className="console-cursor">_</div>
        </div>
      </div>
    </div>
  );
};
