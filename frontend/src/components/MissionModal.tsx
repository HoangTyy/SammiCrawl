import React, { useState } from 'react';
import { 
  X, 
  Send, 
  Sparkles
} from 'lucide-react';
import type { TargetType, CrawlTask } from '../types';
import { useApp } from '../context/AppContext';

interface MissionModalProps {
  isOpen: boolean;
  onClose: () => void;
  onDeploy: (task: Omit<CrawlTask, 'id' | 'status' | 'crawledPosts' | 'createdAt' | 'progress' | 'workerNode' | 'ipAddress' | 'latencyMs'>) => void;
}

export const MissionModal: React.FC<MissionModalProps> = ({
  isOpen,
  onClose,
  onDeploy
}) => {
  const { t } = useApp();
  const [url, setUrl] = useState('');
  const [targetName, setTargetName] = useState('');
  const [targetType, setTargetType] = useState<TargetType>('page');
  const [maxPosts, setMaxPosts] = useState(25);
  const [includeComments, setIncludeComments] = useState(true);
  const [extractMedia, setExtractMedia] = useState(true);

  if (!isOpen) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!url.trim()) return;

    const computedName = targetName.trim() || (
      url.replace(/https?:\/\/(www\.)?facebook\.com\/?/, '').split('/')[0] || 'FB Mission'
    );

    onDeploy({
      url,
      targetName: computedName,
      targetType,
      maxPosts,
      includeComments,
      extractMedia
    });

    setUrl('');
    setTargetName('');
    onClose();
  };

  return (
    <div className="cyber-modal-overlay" onClick={onClose}>
      <div className="cyber-modal-card" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header-cyber">
          <div className="header-title-box">
            <Sparkles size={18} className="text-cyan" />
            <div>
              <h3 className="modal-main-title">{t.modal.title}</h3>
              <p className="modal-sub-title">{t.modal.subtitle}</p>
            </div>
          </div>
          <button type="button" className="btn-close-modal" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="modal-cyber-form">
          <div className="form-group-cyber">
            <label className="cyber-label">
              {t.modal.urlLabel} <span className="req">*</span>
            </label>
            <input
              type="url"
              placeholder="https://www.facebook.com/vietnamnet.vn"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              className="cyber-input-field"
              required
            />
          </div>

          <div className="form-row-2">
            <div className="form-group-cyber">
              <label className="cyber-label">{t.modal.nameLabel}</label>
              <input
                type="text"
                placeholder={t.modal.namePlaceholder}
                value={targetName}
                onChange={(e) => setTargetName(e.target.value)}
                className="cyber-input-field"
              />
            </div>

            <div className="form-group-cyber">
              <label className="cyber-label">{t.modal.typeLabel}</label>
              <select
                value={targetType}
                onChange={(e) => setTargetType(e.target.value as TargetType)}
                className="cyber-select-field"
              >
                <option value="page">{t.dashboard.fanpage}</option>
                <option value="group">{t.dashboard.group}</option>
                <option value="profile">{t.dashboard.profile}</option>
                <option value="reel">Viral Reel</option>
              </select>
            </div>
          </div>

          <div className="range-control-group">
            <div className="range-header">
              <span className="cyber-label">{t.modal.limitLabel}</span>
              <span className="range-badge">{maxPosts} {t.modal.postsUnit}</span>
            </div>
            <input
              type="range"
              min={10}
              max={150}
              step={5}
              value={maxPosts}
              onChange={(e) => setMaxPosts(Number(e.target.value))}
              className="cyber-range-slider"
            />
          </div>

          <div className="toggles-grid">
            <label className="cyber-toggle-box">
              <input
                type="checkbox"
                checked={includeComments}
                onChange={(e) => setIncludeComments(e.target.checked)}
              />
              <div className="toggle-content">
                <span className="toggle-text-main">{t.modal.toggleComments}</span>
              </div>
            </label>

            <label className="cyber-toggle-box">
              <input
                type="checkbox"
                checked={extractMedia}
                onChange={(e) => setExtractMedia(e.target.checked)}
              />
              <div className="toggle-content">
                <span className="toggle-text-main">{t.modal.toggleMedia}</span>
              </div>
            </label>
          </div>

          <div className="modal-cyber-footer">
            <button type="button" className="btn-ghost-cancel" onClick={onClose}>
              {t.modal.btnCancel}
            </button>
            <button type="submit" className="btn-dispatch-submit">
              <Send size={15} />
              <span>{t.modal.btnSubmit}</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
