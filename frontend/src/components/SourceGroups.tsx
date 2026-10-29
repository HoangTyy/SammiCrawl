import { useEffect, useState } from 'react';
import { api } from '../api';
import { useLiveRevision } from '../liveUpdates';

type SourceGroup = { id: string; brandId: string; name: string; url: string; searchKeyword?: string; collectedPosts: number; acceptedPosts: number; reviewPosts: number; rejectedPosts: number; pendingPosts: number; channels: { channelId: string; name: string; acceptedPosts: number }[] };
export function SourceGroups({ brandId }: { brandId: string }) {
  const revision = useLiveRevision();
  const [groups, setGroups] = useState<SourceGroup[]>([]);
  const [error, setError] = useState('');
  useEffect(() => { let active=true; void api<SourceGroup[]>('/source-groups').then(rows=>{if(active){setGroups(rows);setError('');}}).catch(e=>{if(active)setError(e.message);}); return()=>{active=false;}; }, [revision]);
  const visible=groups.filter(group=>!brandId||group.brandId===brandId);
  return <section className="source-groups"><h3>Nhóm nguồn ({visible.length})</h3>{error&&<p role="alert">{error}</p>}
    <div className="article-table-wrap"><table className="article-table"><thead><tr><th>Nhóm / từ khóa</th><th>Đã thu thập</th><th>Clef chấp nhận</th><th>Xem lại / loại / chờ</th><th>Kênh phù hợp</th></tr></thead><tbody>{visible.map(group=><tr key={group.id}>
      <td data-label="Nhóm / từ khóa"><strong>{group.name}</strong>{/^https:\/\/www\.facebook\.com\/groups\/[A-Za-z0-9._-]+\/$/.test(group.url)&&<a href={group.url} target="_blank" rel="noreferrer">{group.url}</a>}<small>{group.searchKeyword}</small></td>
      <td data-label="Đã thu thập">{group.collectedPosts}</td><td data-label="Clef chấp nhận">{group.acceptedPosts}</td>
      <td data-label="Xem lại / loại / chờ">{group.reviewPosts} / {group.rejectedPosts} / {group.pendingPosts}</td>
      <td data-label="Kênh phù hợp">{group.channels.length?group.channels.map(channel=><small key={channel.channelId}>{channel.name} · {channel.acceptedPosts} bài</small>):'Chưa có bài được chấp nhận cho kênh'}</td>
    </tr>)}</tbody></table>{!visible.length&&<p className="workspace-empty">Nhóm sẽ được lưu khi tài khoản tìm thấy trong lượt cào. Không tự tham gia nhóm.</p>}</div>
    <p className="cyber-hint">Clef chấp nhận chỉ tính bài phù hợp đang được giữ; bài cần xem lại không tính vào số này. Kênh phù hợp là gợi ý theo các bài đã đánh giá, chưa phải quyền đăng.</p>
  </section>;
}
