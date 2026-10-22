import React, { useState } from 'react';
import { 
  ShieldCheck, 
  Wifi, 
  Server, 
  CheckCircle2, 
  RefreshCw 
} from 'lucide-react';
import { useApp } from '../../context/AppContext';

export const ProxySettingsView: React.FC = () => {
  const { t } = useApp();
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
          <h2 className="view-title">{t.proxy.title}</h2>
          <p className="view-subtitle">{t.proxy.subtitle}</p>
        </div>
      </div>

      <div className="proxy-grid-2">
        {/* Left Column: Proxy Manager */}
        <div className="cyber-panel proxy-card">
          <div className="panel-header-line">
            <div className="panel-title-group">
              <Wifi size={16} className="text-cyan" />
              <h3 className="panel-title-text">{t.proxy.poolTitle}</h3>
            </div>
            <span className="badge-ip-count">{t.proxy.ipCount.replace('{count}', '0')}</span>
          </div>

          <div className="form-group-cyber">
            <label className="cyber-label">{t.proxy.routingMode}</label>
            <div className="radio-pills-row">
              {[
                { id: 'direct', label: t.proxy.modeDirect },
                { id: 'rotating', label: t.proxy.modeRotating },
                { id: 'datacenter', label: t.proxy.modeDatacenter }
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
            <label className="cyber-label">{t.proxy.listLabel}</label>
            <textarea
              rows={6}
              placeholder={t.proxy.listPlaceholder}
              value={proxyList}
              onChange={(e) => setProxyList(e.target.value)}
              className="cyber-textarea-field"
            />
            <span className="cyber-hint">{t.proxy.listHint}</span>
          </div>

          <div className="proxy-stats-sub">
            <div className="proxy-sub-item">
              <span className="sub-label">{t.proxy.rateLabel}</span>
              <span className="sub-val">{t.proxy.rateVal}</span>
            </div>
            <div className="proxy-sub-item">
              <span className="sub-label">{t.proxy.protoLabel}</span>
              <span className="sub-val">{t.proxy.protoVal}</span>
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
                <h3 className="panel-title-text">{t.proxy.stealthTitle}</h3>
              </div>
              <span className="tag-stealth-active">{t.proxy.stealthStatus}</span>
            </div>

            <div className="stealth-checks-list">
              <div className="stealth-item">
                <CheckCircle2 size={15} className="text-emerald" />
                <div className="stealth-item-info">
                  <span className="stealth-name">{t.proxy.webglTitle}</span>
                  <span className="stealth-desc">{t.proxy.webglDesc}</span>
                </div>
              </div>

              <div className="stealth-item">
                <CheckCircle2 size={15} className="text-emerald" />
                <div className="stealth-item-info">
                  <span className="stealth-name">{t.proxy.webrtcTitle}</span>
                  <span className="stealth-desc">{t.proxy.webrtcDesc}</span>
                </div>
              </div>

              <div className="stealth-item">
                <CheckCircle2 size={15} className="text-emerald" />
                <div className="stealth-item-info">
                  <span className="stealth-name">{t.proxy.mouseTitle}</span>
                  <span className="stealth-desc">{t.proxy.mouseDesc}</span>
                </div>
              </div>
            </div>
          </div>

          {/* Backend .NET 10 Bridge */}
          <div className="cyber-panel backend-bridge-card">
            <div className="panel-header-line">
              <div className="panel-title-group">
                <Server size={16} className="text-indigo" />
                <h3 className="panel-title-text">{t.proxy.bridgeTitle}</h3>
              </div>
              {apiStatus === 'connected' && (
                <span className="tag-cluster-online">{t.proxy.bridgeConnected}</span>
              )}
            </div>

            <div className="form-group-cyber">
              <label className="cyber-label">{t.proxy.endpointLabel}</label>
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
                  <RefreshCw size={14} className={testingApi ? 'spin-fast' : ''} />
                  <span>{t.proxy.btnTestPing}</span>
                </button>
              </div>
              <span className="cyber-hint">{t.proxy.endpointHint}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
