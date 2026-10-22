import React, { useState } from 'react';
import { 
  X, 
  Send, 
  Sparkles
} from 'lucide-react';
import type { TargetType, CrawlTask } from '../types';

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
              <h3 className="modal-main-title">Khởi chạy Mission Cào Dữ liệu Facebook</h3>
              <p className="modal-sub-title">Phân bổ luồng điều phối đến Cluster Worker</p>
            </div>
          </div>
          <button type="button" className="btn-close-modal" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="modal-cyber-form">
          <div className="form-group-cyber">
            <label className="cyber-label">
              Liên kết Facebook mục tiêu <span className="req">*</span>
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
              <label className="cyber-label">Tên định danh</label>
              <input
                type="text"
                placeholder="VietnamNet Daily"
                value={targetName}
                onChange={(e) => setTargetName(e.target.value)}
                className="cyber-input-field"
              />
            </div>

            <div className="form-group-cyber">
              <label className="cyber-label">Loại trang</label>
              <select
                value={targetType}
                onChange={(e) => setTargetType(e.target.value as TargetType)}
                className="cyber-select-field"
              >
                <option value="page">Fanpage</option>
                <option value="group">Facebook Group</option>
                <option value="profile">Profile Cá Nhân</option>
                <option value="reel">Viral Reel</option>
              </select>
            </div>
          </div>

          <div className="range-control-group">
            <div className="range-header">
              <span className="cyber-label">Số lượng bài cần quét:</span>
              <span className="range-badge">{maxPosts} bài</span>
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
                <span className="toggle-text-main">Cào Bình luận & Reactions</span>
              </div>
            </label>

            <label className="cyber-toggle-box">
              <input
                type="checkbox"
                checked={extractMedia}
                onChange={(e) => setExtractMedia(e.target.checked)}
              />
              <div className="toggle-content">
                <span className="toggle-text-main">Trích xuất Hình ảnh / Media</span>
              </div>
            </label>
          </div>

          <div className="modal-cyber-footer">
            <button type="button" className="btn-ghost-cancel" onClick={onClose}>
              Hủy bỏ
            </button>
            <button type="submit" className="btn-dispatch-submit">
              <Send size={15} />
              <span>Kích hoạt ngay</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
