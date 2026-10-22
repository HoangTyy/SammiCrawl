import React, { useState } from 'react';
import { 
  Link2, 
  Play, 
  MessageSquare, 
  Sparkles
} from 'lucide-react';
import type { TargetType } from '../types';

interface CrawlQuickBarProps {
  onQuickAdd: (url: string, targetType: TargetType, maxPosts: number, includeComments: boolean) => void;
}

export const CrawlQuickBar: React.FC<CrawlQuickBarProps> = ({ onQuickAdd }) => {
  const [url, setUrl] = useState('');
  const [targetType, setTargetType] = useState<TargetType>('page');
  const [maxPosts, setMaxPosts] = useState(20);
  const [includeComments, setIncludeComments] = useState(true);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!url.trim()) return;
    onQuickAdd(url, targetType, maxPosts, includeComments);
    setUrl('');
  };

  return (
    <div className="quick-bar-container">
      <div className="quick-bar-header">
        <div className="quick-bar-title-wrap">
          <Sparkles className="spark-icon" size={16} />
          <h2 className="quick-bar-heading">Khởi chạy tác vụ Cào nhanh Facebook</h2>
        </div>
        <span className="quick-bar-tip">Hỗ trợ Fanpage công khai, Group mở và Profile có chế độ xem tự do</span>
      </div>

      <form onSubmit={handleSubmit} className="quick-bar-form">
        <div className="input-group-url">
          <Link2 className="input-icon" size={18} />
          <input
            type="url"
            placeholder="Dán link Facebook (Ví dụ: https://www.facebook.com/vietnamnet.vn)..."
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            className="url-input"
            required
          />
        </div>

        <div className="form-controls-row">
          {/* Target Type */}
          <div className="control-field">
            <label className="field-label">Phân loại:</label>
            <select 
              value={targetType} 
              onChange={(e) => setTargetType(e.target.value as TargetType)}
              className="select-control"
            >
              <option value="page">Fanpage Facebook</option>
              <option value="group">Facebook Group</option>
              <option value="profile">Profile / User Page</option>
            </select>
          </div>

          {/* Max Posts */}
          <div className="control-field">
            <label className="field-label">Giới hạn:</label>
            <select 
              value={maxPosts} 
              onChange={(e) => setMaxPosts(Number(e.target.value))}
              className="select-control"
            >
              <option value={10}>10 bài mới nhất</option>
              <option value={20}>20 bài mới nhất</option>
              <option value={50}>50 bài mới nhất</option>
              <option value={100}>100 bài</option>
            </select>
          </div>

          {/* Include Comments Checkbox */}
          <label className="checkbox-control">
            <input
              type="checkbox"
              checked={includeComments}
              onChange={(e) => setIncludeComments(e.target.checked)}
            />
            <MessageSquare size={14} />
            <span>Kèm bình luận</span>
          </label>

          {/* Submit Button */}
          <button type="submit" className="btn-submit-quick">
            <Play size={15} fill="currentColor" />
            <span>Thêm vào Queue</span>
          </button>
        </div>
      </form>
    </div>
  );
};
