import React, { useState } from 'react';
import { 
  Database, 
  Search, 
  Filter, 
  FileSpreadsheet, 
  FileCode, 
  Server, 
  Sparkles
} from 'lucide-react';

interface DataVaultViewProps {
  onNavigateToStudio: () => void;
}

export const DataVaultView: React.FC<DataVaultViewProps> = ({ onNavigateToStudio }) => {
  const [searchQuery, setSearchQuery] = useState('');
  const [filterType, setFilterType] = useState('all');

  return (
    <div className="view-container vault-view">
      {/* Top Header & Export Action */}
      <div className="view-header-strip">
        <div>
          <h2 className="view-title">Kho Dữ liệu Bài viết & Media (Data Vault)</h2>
          <p className="view-subtitle">Kho chứa dữ liệu thô và cấu trúc đã được trích xuất từ các trang Facebook mục tiêu</p>
        </div>

        {/* Export Toolbar */}
        <div className="vault-actions-row">
          <button type="button" className="btn-export-ghost" disabled>
            <FileCode size={14} />
            <span>Xuất JSON</span>
          </button>
          <button type="button" className="btn-export-ghost" disabled>
            <FileSpreadsheet size={14} />
            <span>Xuất Excel/CSV</span>
          </button>
          <button type="button" className="btn-export-solid" disabled>
            <Server size={14} />
            <span>Đồng bộ .NET EF Database</span>
          </button>
        </div>
      </div>

      {/* Vault Telemetry Metrics */}
      <div className="vault-metrics-grid">
        <div className="vault-metric-tile">
          <span className="v-tile-label">Tổng bài viết (Posts)</span>
          <span className="v-tile-val">0</span>
          <span className="v-tile-sub">Chưa có record</span>
        </div>
        <div className="vault-metric-tile">
          <span className="v-tile-label">Bình luận bóc tách</span>
          <span className="v-tile-val">0</span>
          <span className="v-tile-sub">0 tác giả thảo luận</span>
        </div>
        <div className="vault-metric-tile">
          <span className="v-tile-label">Hình ảnh & Media</span>
          <span className="v-tile-val">0</span>
          <span className="v-tile-sub">0 MB dung lượng</span>
        </div>
        <div className="vault-metric-tile">
          <span className="v-tile-label">Bản quyền & Metadata</span>
          <span className="v-tile-val">0</span>
          <span className="v-tile-sub">Đồng bộ tự động</span>
        </div>
      </div>

      {/* Main Filter & Empty Vault Panel */}
      <div className="cyber-panel vault-panel">
        <div className="vault-filter-toolbar">
          <div className="search-box-cyber">
            <Search size={15} className="search-icon" />
            <input
              type="text"
              placeholder="Tìm kiếm nội dung bài viết, tác giả, hashtag..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="search-input-cyber"
            />
          </div>

          <div className="vault-filters-right">
            <select
              value={filterType}
              onChange={(e) => setFilterType(e.target.value)}
              className="vault-select"
            >
              <option value="all">Tất cả nguồn bài</option>
              <option value="page">Chỉ Fanpage</option>
              <option value="group">Chỉ Group</option>
              <option value="reel">Chỉ Reels</option>
            </select>

            <button type="button" className="btn-filter-icon">
              <Filter size={15} />
              <span>Bộ lọc nâng cao</span>
            </button>
          </div>
        </div>

        {/* Empty State */}
        <div className="cyber-empty-state">
          <div className="radar-empty-graphic">
            <div className="radar-circle ring-1"></div>
            <div className="radar-circle ring-2"></div>
            <Database size={32} className="radar-icon-center text-cyan" />
          </div>

          <h4 className="empty-heading">Kho lưu trữ chưa có bài viết nào (0 Records)</h4>
          <p className="empty-sub">
            Sau khi các tác vụ trong Hàng đợi chạy và trích xuất thành công nội dung Facebook,
            toàn bộ bài đăng, lượt like/share/comment, hình ảnh và metadata thời gian sẽ tự động xuất hiện tại đây.
          </p>

          <button 
            type="button" 
            className="btn-launch-cyber"
            onClick={onNavigateToStudio}
          >
            <Sparkles size={15} />
            <span>Kích hoạt cào bài viết đầu tiên</span>
          </button>
        </div>
      </div>
    </div>
  );
};
