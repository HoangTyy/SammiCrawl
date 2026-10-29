import type { ReactNode } from 'react';
import { HelpTooltip } from './HelpTooltip';
import { useCallback, useEffect, useRef, useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { api } from '../api';
import { useLiveRevision } from '../liveUpdates';
type ChannelReport = { brandId: string; brandName: string; channelId: string; channelName: string; type: string; dailyTarget: number; awaitingReview: number; approvedPendingWorker: number; planned: number; published: number; missing: number };
type Report = { date: string; collected: number; recommended: number; awaitingReview: number; approvedPendingWorker: number; published: number; dailyTarget: number; channels: ChannelReport[]; calendar: { id: string; channelName: string; content: string; plannedAt: string; status: string }[] };
const nameKey = (value: string) => value.normalize('NFC').toLocaleLowerCase('vi-VN').replace(/[^\p{L}\p{N}]/gu, '');
export function ContentReport({ children }: { children?: ReactNode }) {
  const liveRevision = useLiveRevision();
  const [date, setDate] = useState(() => new Date(Date.now() + 7 * 3600000).toISOString().slice(0, 10));
  const requestId = useRef(0);
  const [report, setReport] = useState<Report>(); const [error, setError] = useState(''); const [loading, setLoading] = useState(false);
  const load = useCallback(async () => { const id = ++requestId.current; setLoading(true); setError(''); setReport(undefined); try { const result = await api<Report>(`/reports/content?date=${date}`); if (id === requestId.current) setReport(result); } catch (e) { if (id === requestId.current) setError((e as Error).message); } finally { if (id === requestId.current) setLoading(false); } }, [date]);
  useEffect(() => { void load(); return () => { requestId.current++; }; }, [load, liveRevision]);
  return <><section className="content-report">
    <div className="view-header-strip"><div><h2 className="view-title">Báo cáo nội dung<HelpTooltip text="Chỉ tiêu và lịch dự kiến theo kênh · Giờ Việt Nam (UTC+7)" /></h2></div><div className="preset-buttons-row"><input type="date" aria-label="Ngày báo cáo" required className="cyber-input-field" value={date} onChange={e => { if (e.target.value) setDate(e.target.value); }} /><button className="btn-preset" onClick={() => void load()} disabled={loading}><RefreshCw size={14} /> Làm mới</button></div></div>
    {error && <p role="alert" className="config-notice">{error}</p>}
    <div className="brand-summary-grid">{[['Bài nguồn đã thu thập', report?.collected], ['Clef đề xuất', report?.recommended], ['Bản nháp chờ duyệt', report?.awaitingReview], ['Đã duyệt · chờ worker', report?.approvedPendingWorker]].map(([label, count]) => <div key={String(label)} className="cyber-panel brand-summary"><span>{label}</span><strong>{loading ? '…' : count ?? '—'}</strong></div>)}</div>
    <div className="cyber-panel report-calendar"><h3>Lịch dự kiến · {report?.published ?? 0}/{report?.dailyTarget ?? 0} bài đã đăng trong ngày</h3>{report?.calendar.length ? report.calendar.map(item => <div className="report-calendar-row" key={item.id}><time>{new Date(item.plannedAt).toLocaleTimeString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit' })}</time><div><strong>{item.channelName}</strong><p>{item.content.slice(0, 120)}</p></div><span>{item.status === 'awaiting_review' ? 'Chưa duyệt' : 'Đã duyệt · chờ worker'}</span></div>) : <p className="cyber-hint">Chưa có bản nháp đặt giờ cho ngày này. Đặt giờ trong màn hình duyệt bài.</p>}</div>
  </section>
    {children}
    <section className="content-report channel-report" aria-label="Báo cáo theo thương hiệu và kênh">
    <div className="article-table-wrap"><table className="article-table"><thead><tr><th>Thương hiệu / kênh</th><th>Chỉ tiêu/ngày</th><th>Chờ duyệt</th><th>Đã duyệt</th><th>Dự kiến ngày chọn</th><th>Đã đăng thực tế</th><th>Còn thiếu</th></tr></thead><tbody>{report?.channels.map(channel => <tr key={`${channel.brandId}-${channel.channelId}`}><td><strong>{channel.channelName}</strong><small>{nameKey(channel.brandName) !== nameKey(channel.channelName) && <>{channel.brandName} · </>}{channel.type === 'group' ? 'Group' : 'Page'}</small></td><td>{channel.dailyTarget || 'Chưa đặt'}</td><td>{channel.awaitingReview}</td><td>{channel.approvedPendingWorker}</td><td>{channel.planned}</td><td>{channel.published}</td><td>{channel.dailyTarget ? channel.missing : '—'}</td></tr>)}</tbody></table>{!loading && !report?.channels.length && <p className="workspace-empty">Thêm kênh và chỉ tiêu trong hồ sơ thương hiệu để theo dõi.</p>}</div>
    <p className="cyber-hint">Số bài nguồn và bản nháp là tổng hiện có. Đã đăng và còn thiếu tính theo ngày đã chọn; chỉ ghi nhận khi có link Facebook và thời điểm đăng. Worker chưa triển khai nên duyệt hoặc đặt giờ chưa có nghĩa là đã đăng.</p>
    </section></>;
}
