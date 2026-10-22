import React from 'react';
import { 
  Globe2, 
  FileCheck2, 
  Layers3, 
  Radio 
} from 'lucide-react';
import type { EngineStats } from '../types';

interface MetricsOverviewProps {
  stats: EngineStats;
}

export const MetricsOverview: React.FC<MetricsOverviewProps> = ({ stats }) => {
  const cards = [
    {
      id: 'targets',
      title: 'Mục tiêu theo dõi',
      value: stats.totalTargets,
      subtext: 'Fanpage, Group, Profile',
      icon: Globe2,
      tag: '0 mới tuần này',
      color: 'purple'
    },
    {
      id: 'posts',
      title: 'Bài viết thu thập',
      value: stats.totalPostsCrawled,
      subtext: 'Bài đăng & Bình luận',
      icon: FileCheck2,
      tag: 'Chưa có dữ liệu',
      color: 'blue'
    },
    {
      id: 'queue',
      title: 'Hàng đợi xử lý',
      value: stats.pendingQueue,
      subtext: 'Tasks đang chờ điều phối',
      icon: Layers3,
      tag: 'Trống (Idle)',
      color: 'amber'
    },
    {
      id: 'crawlers',
      title: 'Crawler Worker',
      value: `${stats.runningCrawlers} / 4`,
      subtext: 'Tiến trình Headless Chrome',
      icon: Radio,
      tag: 'Đang lắng nghe',
      color: 'emerald'
    }
  ];

  return (
    <div className="metrics-grid">
      {cards.map((card) => {
        const IconComponent = card.icon;
        return (
          <div key={card.id} className={`metric-card metric-${card.color}`}>
            <div className="metric-header">
              <span className="metric-title">{card.title}</span>
              <div className={`metric-icon-wrap icon-wrap-${card.color}`}>
                <IconComponent size={18} />
              </div>
            </div>

            <div className="metric-body">
              <span className="metric-value">{card.value}</span>
              <span className="metric-tag">{card.tag}</span>
            </div>

            <div className="metric-footer">
              <span className="metric-subtext">{card.subtext}</span>
            </div>
          </div>
        );
      })}
    </div>
  );
};
