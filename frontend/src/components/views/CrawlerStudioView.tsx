import React, { useState } from 'react';
import { 
  Sliders, 
  ShieldCheck, 
  Database, 
  Play, 
  Layers, 
  Image, 
  MessageSquare,
  CheckCircle2,
  Key
} from 'lucide-react';
import type { TargetType, CrawlTask } from '../../types';

interface CrawlerStudioViewProps {
  onDeployMission: (task: Omit<CrawlTask, 'id' | 'status' | 'crawledPosts' | 'createdAt' | 'progress' | 'workerNode' | 'ipAddress' | 'latencyMs'>) => void;
}

export const CrawlerStudioView: React.FC<CrawlerStudioViewProps> = ({ onDeployMission }) => {
  const [url, setUrl] = useState('');
  const [targetName, setTargetName] = useState('');
  const [targetType, setTargetType] = useState<TargetType>('page');
  const [maxPosts, setMaxPosts] = useState(30);
  const [includeComments, setIncludeComments] = useState(true);
  const [extractMedia, setExtractMedia] = useState(true);
  const [cookieData, setCookieData] = useState('');
  const [scrollDelay, setScrollDelay] = useState(4);
  const [successToast, setSuccessToast] = useState(false);

  const handleDeploy = (e: React.FormEvent) => {
    e.preventDefault();
    if (!url.trim()) return;

    const computedName = targetName.trim() || (
      url.replace(/https?:\/\/(www\.)?facebook\.com\/?/, '').split('/')[0] || 'FB Target'
    );

    onDeployMission({
      url,
      targetName: computedName,
      targetType,
      maxPosts,
      includeComments,
      extractMedia
    });

    setSuccessToast(true);
    setTimeout(() => setSuccessToast(false), 3000);
    setUrl('');
    setTargetName('');
    setCookieData('');
  };

  const applyPreset = (type: TargetType, posts: number, comments: boolean, media: boolean) => {
    setTargetType(type);
    setMaxPosts(posts);
    setIncludeComments(comments);
    setExtractMedia(media);
  };

  return (
    <div className="view-container studio-view">
      <div className="view-header-strip">
        <div>
          <h2 className="view-title">Facebook Crawler Studio</h2>
          <p className="view-subtitle">Không gian cấu hình chuyên sâu các thông số thu thập dữ liệu Facebook tự động</p>
        </div>

        {/* Preset Buttons */}
        <div className="preset-buttons-row">
          <span className="preset-label">Mẫu nhanh:</span>
          <button 
            type="button" 
            className="btn-preset" 
            onClick={() => applyPreset('page', 20, true, true)}
          >
            Quét Tin Fanpage
          </button>
          <button 
            type="button" 
            className="btn-preset" 
            onClick={() => applyPreset('group', 50, true, false)}
          >
            Quét Thảo Luận Group
          </button>
          <button 
            type="button" 
            className="btn-preset" 
            onClick={() => applyPreset('reel', 30, false, true)}
          >
            Quét Viral Reels
          </button>
        </div>
      </div>

      {successToast && (
        <div className="toast-cyber-success">
          <CheckCircle2 size={16} />
          <span>Mission đã được gửi tới Task Queue! Worker đang chuẩn bị phân giải DOM.</span>
        </div>
      )}

      <form onSubmit={handleDeploy} className="studio-form-grid">
        {/* Left Column: Target & Scope Configuration */}
        <div className="studio-left-card">
          <div className="card-section-header">
            <Sliders size={16} className="text-cyan" />
            <h3 className="section-title">1. Thông tin Mục tiêu & Định danh</h3>
          </div>

          <div className="form-group-cyber">
            <label className="cyber-label">
              Đường dẫn Facebook (URL) <span className="req">*</span>
            </label>
            <input
              type="url"
              placeholder="https://www.facebook.com/vietnamnet.vn"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              className="cyber-input-field"
              required
            />
            <span className="cyber-hint">Hỗ trợ Fanpage, Nhóm công khai, Bài viết đơn lẻ, Reels</span>
          </div>

          <div className="form-row-2">
            <div className="form-group-cyber">
              <label className="cyber-label">Tên gán nhãn gợi nhớ</label>
              <input
                type="text"
                placeholder="Tin tức Việt Nam Net"
                value={targetName}
                onChange={(e) => setTargetName(e.target.value)}
                className="cyber-input-field"
              />
            </div>

            <div className="form-group-cyber">
              <label className="cyber-label">Kiểu cấu trúc</label>
              <select
                value={targetType}
                onChange={(e) => setTargetType(e.target.value as TargetType)}
                className="cyber-select-field"
              >
                <option value="page">Fanpage Chính Thức</option>
                <option value="group">Facebook Group Công Khai</option>
                <option value="profile">Profile Cá Nhân</option>
                <option value="reel">Reels Video Feed</option>
              </select>
            </div>
          </div>

          <div className="card-section-header mt-4">
            <Layers size={16} className="text-emerald" />
            <h3 className="section-title">2. Độ sâu & Dữ liệu cần bóc tách</h3>
          </div>

          <div className="range-control-group">
            <div className="range-header">
              <span className="cyber-label">Số lượng bài viết cần cào:</span>
              <span className="range-badge">{maxPosts} bài</span>
            </div>
            <input
              type="range"
              min={5}
              max={200}
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
                <div className="toggle-title-line">
                  <MessageSquare size={15} />
                  <span>Cào Bình luận & Tương tác</span>
                </div>
                <span className="toggle-sub">Thu nạp top comments, tác giả và thời gian phản hồi</span>
              </div>
            </label>

            <label className="cyber-toggle-box">
              <input
                type="checkbox"
                checked={extractMedia}
                onChange={(e) => setExtractMedia(e.target.checked)}
              />
              <div className="toggle-content">
                <div className="toggle-title-line">
                  <Image size={15} />
                  <span>Trích xuất Media & CDN URL</span>
                </div>
                <span className="toggle-sub">Lưu trữ URL ảnh chất lượng cao và preview video</span>
              </div>
            </label>
          </div>
        </div>

        {/* Right Column: Stealth, Cookies & Launch Control */}
        <div className="studio-right-card">
          <div className="card-section-header">
            <ShieldCheck size={16} className="text-amber" />
            <h3 className="section-title">3. Cơ chế Chống Checkpoint (Stealth Shield)</h3>
          </div>

          <div className="range-control-group">
            <div className="range-header">
              <span className="cyber-label">Độ trễ cuộn trang mô phỏng người dùng:</span>
              <span className="range-badge">{scrollDelay}s / cuộn</span>
            </div>
            <input
              type="range"
              min={2}
              max={10}
              step={1}
              value={scrollDelay}
              onChange={(e) => setScrollDelay(Number(e.target.value))}
              className="cyber-range-slider amber"
            />
          </div>

          <div className="form-group-cyber">
            <label className="cyber-label">
              <Key size={14} className="inline-icon" /> Cookie tài khoản Facebook phụ (Tùy chọn)
            </label>
            <textarea
              rows={4}
              placeholder="c_user=...; xs=...; datr=... (Dành cho việc quét Group kín hoặc tránh bot challenge)"
              value={cookieData}
              onChange={(e) => setCookieData(e.target.value)}
              className="cyber-textarea-field"
            />
            <span className="cyber-hint">Để trống nếu muốn chạy chế độ Guest Stealth (Cào tự do không login).</span>
          </div>

          <div className="pipeline-destination-box">
            <Database size={15} className="text-cyan" />
            <div className="dest-text">
              <span className="dest-title">Đích lưu trữ tự động:</span>
              <span className="dest-desc">Tự động nạp vào Backend .NET 10 qua REST API (`/api/crawl/ingest`)</span>
            </div>
          </div>

          <button type="submit" className="btn-launch-full">
            <Play size={16} fill="currentColor" />
            <span>Kích hoạt Mission vào Hàng Đợi</span>
          </button>
        </div>
      </form>
    </div>
  );
};
