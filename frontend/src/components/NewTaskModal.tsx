import React, { useState } from 'react';
import { 
  X, 
  Link2 
} from 'lucide-react';
import { FacebookIcon } from './FacebookIcon';
import type { CrawlTask, TargetType } from '../types';

interface NewTaskModalProps {
  isOpen: boolean;
  onClose: () => void;
  onAddTask: (task: Omit<CrawlTask, 'id' | 'status' | 'crawledPosts' | 'createdAt' | 'progress'>) => void;
}

export const NewTaskModal: React.FC<NewTaskModalProps> = ({ 
  isOpen, 
  onClose, 
  onAddTask 
}) => {
  const [url, setUrl] = useState('');
  const [targetName, setTargetName] = useState('');
  const [targetType, setTargetType] = useState<TargetType>('page');
  const [maxPosts, setMaxPosts] = useState(30);
  const [includeComments, setIncludeComments] = useState(true);
  const [cookieString, setCookieString] = useState('');
  const [keywordFilter, setKeywordFilter] = useState('');

  if (!isOpen) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!url.trim()) return;

    const computedName = targetName.trim() || (
      url.split('facebook.com/')[1]?.split('/')[0]?.split('?')[0] || 'Mục tiêu Facebook'
    );

    onAddTask({
      url,
      targetName: computedName,
      targetType,
      maxPosts,
      includeComments,
      extractMedia: true,
      workerNode: 'Worker-Alpha',
      ipAddress: '127.0.0.1',
      latencyMs: 20
    });

    setUrl('');
    setTargetName('');
    setCookieString('');
    setKeywordFilter('');
    onClose();
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <div className="modal-title-wrap">
            <div className="modal-icon-badge">
              <FacebookIcon size={18} className="fb-icon" />
            </div>
            <div>
              <h3 className="modal-title">Khởi tạo Chiến dịch Cào dữ liệu Facebook</h3>
              <p className="modal-subtitle">Thiết lập cấu hình thu thập dữ liệu Fanpage, Group hoặc Profile</p>
            </div>
          </div>
          <button type="button" className="btn-modal-close" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="modal-form">
          <div className="form-group">
            <label className="form-label">
              Đường dẫn Facebook mục tiêu <span className="text-danger">*</span>
            </label>
            <div className="input-with-icon">
              <Link2 size={16} className="field-icon" />
              <input
                type="url"
                placeholder="https://www.facebook.com/ten-fanpage-hoac-group"
                value={url}
                onChange={(e) => setUrl(e.target.value)}
                required
                className="form-input"
              />
            </div>
            <span className="field-help">Ví dụ: https://www.facebook.com/vnexpress.net</span>
          </div>

          <div className="form-row">
            <div className="form-group flex-1">
              <label className="form-label">Tên định danh (Tùy chọn)</label>
              <input
                type="text"
                placeholder="VnExpress Tin tức"
                value={targetName}
                onChange={(e) => setTargetName(e.target.value)}
                className="form-input"
              />
            </div>

            <div className="form-group flex-1">
              <label className="form-label">Phân loại trang</label>
              <select
                value={targetType}
                onChange={(e) => setTargetType(e.target.value as TargetType)}
                className="form-select"
              >
                <option value="page">Fanpage công khai</option>
                <option value="group">Facebook Group</option>
                <option value="profile">Profile cá nhân</option>
              </select>
            </div>
          </div>

          <div className="form-row">
            <div className="form-group flex-1">
              <label className="form-label">Giới hạn số lượng bài viết</label>
              <select
                value={maxPosts}
                onChange={(e) => setMaxPosts(Number(e.target.value))}
                className="form-select"
              >
                <option value={10}>10 bài viết mới nhất</option>
                <option value={20}>20 bài viết mới nhất</option>
                <option value={50}>50 bài viết mới nhất</option>
                <option value={100}>100 bài viết</option>
                <option value={300}>300 bài viết (Yêu cầu Proxy)</option>
              </select>
            </div>

            <div className="form-group flex-1">
              <label className="form-label">Lọc từ khóa (Tùy chọn)</label>
              <input
                type="text"
                placeholder="Ví dụ: công nghệ, khuyến mãi..."
                value={keywordFilter}
                onChange={(e) => setKeywordFilter(e.target.value)}
                className="form-input"
              />
            </div>
          </div>

          <div className="options-checkbox-group">
            <label className="modal-checkbox-row">
              <input
                type="checkbox"
                checked={includeComments}
                onChange={(e) => setIncludeComments(e.target.checked)}
              />
              <div className="checkbox-info">
                <span className="checkbox-title">Thu thập bình luận (Comments)</span>
                <span className="checkbox-desc">Trích xuất nội dung bình luận hàng đầu, tác giả và lượt tương tác</span>
              </div>
            </label>
          </div>

          <div className="form-group">
            <label className="form-label">
              Cookie tài khoản phụ (Tùy chọn - Tăng độ tin cậy)
            </label>
            <textarea
              placeholder="c_user=...; xs=...; (Để trống nếu dùng cào ẩn danh không đăng nhập)"
              rows={2}
              value={cookieString}
              onChange={(e) => setCookieString(e.target.value)}
              className="form-textarea"
            />
          </div>

          <div className="modal-footer">
            <button 
              type="button" 
              className="btn-modal-cancel" 
              onClick={onClose}
            >
              Hủy bỏ
            </button>
            <button 
              type="submit" 
              className="btn-modal-submit"
            >
              Bắt đầu cào ngay
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
