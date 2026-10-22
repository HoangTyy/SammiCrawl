import React, { useState } from 'react';
import { 
  ShieldCheck, 
  Wifi, 
  Server, 
  CheckCircle2, 
  RefreshCw 
} from 'lucide-react';

export const ProxySettingsView: React.FC = () => {
  const [proxyList, setProxyList] = useState<string>('');
  const [proxyMode, setProxyMode] = useState('direct');
  const [apiEndpoint, setApiEndpoint] = useState('http://localhost:7142/api/crawl');
  const [testingApi, setTestingApi] = useState(false);
  const [apiStatus, setApiStatus] = useState<string | null>(null);

  const testConnection = () => {
    setTestingApi(true);
    setApiStatus(null);
    setTimeout(() => {
      setTestingApi(false);
      setApiStatus('connected');
    }, 800);
  };

  return (
    <div className="view-container proxy-view">
      <div className="view-header-strip">
        <div>
          <h2 className="view-title">Proxy Pool & Anti-Detection Stealth</h2>
          <p className="view-subtitle">Cấu hình mạng IP xoay vòng, dấu vân tay trình duyệt (Fingerprint) và cầu nối Backend .NET</p>
        </div>
      </div>

      <div className="proxy-grid-2">
        {/* Left Column: Proxy Manager */}
        <div className="cyber-panel proxy-card">
          <div className="panel-header-line">
            <div className="panel-title-group">
              <Wifi size={16} className="text-cyan" />
              <h3 className="panel-title-text">Hồ chứa IP (Proxy Pool Manager)</h3>
            </div>
            <span className="badge-ip-count">0 IPs Active</span>
          </div>

          <div className="form-group-cyber">
            <label className="cyber-label">Chế độ định tuyến mạng:</label>
            <div className="radio-pills-row">
              {[
                { id: 'direct', label: 'Direct IP (Cào trực tiếp)' },
                { id: 'rotating', label: 'Rotating Residential' },
                { id: 'datacenter', label: 'Dedicated Datacenter' }
              ].map(opt => (
                <button
                  key={opt.id}
                  type="button"
                  className={`radio-pill-btn ${proxyMode === opt.id ? 'active' : ''}`}
                  onClick={() => setProxyMode(opt.id)}
                >
                  {opt.label}
                </button>
              ))}
            </div>
          </div>

          <div className="form-group-cyber">
            <label className="cyber-label">Danh sách Proxy (Định dạng: IP:Port:User:Pass)</label>
            <textarea
              rows={6}
              placeholder="103.142.25.10:8080:username:password&#10;114.119.130.45:3128&#10;(Mỗi dòng một địa chỉ proxy...)"
              value={proxyList}
              onChange={(e) => setProxyList(e.target.value)}
              className="cyber-textarea-field"
            />
            <span className="cyber-hint">Hệ thống tự động xoay vòng IP sau mỗi 5 yêu cầu để tránh checkpoint Facebook.</span>
          </div>

          <div className="proxy-stats-sub">
            <div className="proxy-sub-item">
              <span className="sub-label">Tỉ lệ xoay vòng:</span>
              <span className="sub-val">Auto-Rotate per target</span>
            </div>
            <div className="proxy-sub-item">
              <span className="sub-label">Giao thức hỗ trợ:</span>
              <span className="sub-val">HTTP, HTTPS, SOCKS5</span>
            </div>
          </div>
        </div>

        {/* Right Column: Stealth & Backend .NET Bridge */}
        <div className="proxy-right-col">
          {/* Stealth Fingerprint */}
          <div className="cyber-panel stealth-card">
            <div className="panel-header-line">
              <div className="panel-title-group">
                <ShieldCheck size={16} className="text-emerald" />
                <h3 className="panel-title-text">Bảo vệ Chống Bot (Anti-Fingerprint)</h3>
              </div>
              <span className="tag-stealth-active">Active</span>
            </div>

            <div className="stealth-checks-list">
              <div className="stealth-item">
                <CheckCircle2 size={15} className="text-emerald" />
                <div className="stealth-item-info">
                  <span className="stealth-name">WebGL & Canvas Noise</span>
                  <span className="stealth-desc">Mô phỏng ngẫu nhiên cấu trúc card đồ họa thật</span>
                </div>
              </div>

              <div className="stealth-item">
                <CheckCircle2 size={15} className="text-emerald" />
                <div className="stealth-item-info">
                  <span className="stealth-name">WebRTC Leak Shield</span>
                  <span className="stealth-desc">Chặn rò rỉ IP nội bộ qua giao thức truyền thông WebRTC</span>
                </div>
              </div>

              <div className="stealth-item">
                <CheckCircle2 size={15} className="text-emerald" />
                <div className="stealth-item-info">
                  <span className="stealth-name">Humanized Mouse & Touch</span>
                  <span className="stealth-desc">Đường cong di chuột Bézier ngẫu nhiên chống AI Bot Detect</span>
                </div>
              </div>
            </div>
          </div>

          {/* Backend .NET 10 Bridge */}
          <div className="cyber-panel backend-bridge-card">
            <div className="panel-header-line">
              <div className="panel-title-group">
                <Server size={16} className="text-indigo" />
                <h3 className="panel-title-text">Backend .NET 10 API Bridge</h3>
              </div>
              {apiStatus === 'connected' && (
                <span className="tag-cluster-online">Đã kết nối</span>
              )}
            </div>

            <div className="form-group-cyber">
              <label className="cyber-label">REST API Ingest Endpoint:</label>
              <div className="input-with-button">
                <input
                  type="text"
                  value={apiEndpoint}
                  onChange={(e) => setApiEndpoint(e.target.value)}
                  className="cyber-input-field"
                />
                <button
                  type="button"
                  className="btn-test-conn"
                  onClick={testConnection}
                  disabled={testingApi}
                >
                  {testingApi ? <RefreshCw size={14} className="spin-fast" /> : <RefreshCw size={14} />}
                  <span>Test Ping</span>
                </button>
              </div>
              <span className="cyber-hint">Kết nối trực tiếp tới ASP.NET Core Web API của dự án SammiCrawl.</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
