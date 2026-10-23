import { HelpTooltip } from '../HelpTooltip';
import { useCallback, useEffect, useState } from 'react';
import { RefreshCw, Plus, X, Files, CalendarClock } from 'lucide-react';
import { api } from '../../api';
import { brandChannels, reviewFields } from '../../brandPolicy';
import type { Brand, BrandChannel } from '../../brandPolicy';

type Media = { url: string; type: string; status?: string };
type Article = { id: string; brandId: string; title?: string; content: string; sourceUrl: string; createdAt: string; likes?: number; comments?: number; aiStatus?: string; aiScore?: number; aiReason?: string; aiTopic?: string; aiWarnings?: string[]; aiBrandUpdatedAt?: string; aiEvaluatedAt?: string; aiImagesAnalyzed?: number; media?: Media[] };
type Draft = { id: string; articleId: string; brandId: string; channelId?: string; channelSnapshot?: BrandChannel; branch: string; content: string; sourceContent?: string; sourceUrl?: string; media: Media[]; status: string; publishAccountId?: string; review?: Record<string, boolean>; plannedAt?: string; createdAt: string };
type Account = { id: string; name: string; role: string; enabled?: boolean; facebookStatus?: string };
const safeSource = (url?: string) => { try { const u = new URL(url ?? ''); return u.protocol === 'https:' && (u.hostname === 'facebook.com' || u.hostname.endsWith('.facebook.com')) ? url : undefined; } catch { return undefined; } };
const localMedia = (url: string) => /^\/media\/[a-zA-Z0-9_./-]+$/.test(url) && !url.includes('..');
const localPlan = (value?: string) => value && Number.isFinite(Date.parse(value)) ? new Date(Date.parse(value) + 7 * 3600000).toISOString().slice(0, 16) : '';
const displayDate = (value?: string) => value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : 'Chưa đặt giờ';
const tabs = [['all', 'Tất cả'], ['recommended', 'Clef đề xuất'], ['review', 'Cần xem lại'], ['rejected', 'Không phù hợp'], ['pending', 'Chờ Clef']];

export function DataVaultView({ onNavigateToStudio, initialBrandId = '' }: { onNavigateToStudio: () => void; initialBrandId?: string }) {
  const [articles, setArticles] = useState<Article[]>([]); const [drafts, setDrafts] = useState<Draft[]>([]); const [brands, setBrands] = useState<Brand[]>([]); const [accounts, setAccounts] = useState<Account[]>([]);
  const [brandId, setBrandId] = useState(initialBrandId); const [filter, setFilter] = useState('all'); const [query, setQuery] = useState(''); const [view, setView] = useState('articles');
  const [message, setMessage] = useState(''); const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false);
  const [selected, setSelected] = useState<Article | null>(null); const [draft, setDraft] = useState<Draft | null>(null); const [channelId, setChannelId] = useState('');
  const load = useCallback(async () => {
    const [a, b, p, d] = await Promise.all([api<Article[]>('/records/articles'), api<Brand[]>('/records/brands'), api<Account[]>('/records/accounts'), api<Draft[]>('/records/drafts')]);
    setArticles(a); setBrands(b); setAccounts(p); setDrafts(d);
  }, []);
  useEffect(() => { void load().catch(error => setMessage(error.message)).finally(() => setLoading(false)); }, [load]);
  const stale = (article: Article) => Boolean(article.aiEvaluatedAt && (article.aiBrandUpdatedAt ?? '') !== (brands.find(b => b.id === article.brandId)?.updatedAt ?? ''));
  const status = (article: Article) => stale(article) ? 'review' : article.aiStatus ?? 'pending';
  const scoped = articles.filter(a => !brandId || a.brandId === brandId);
  const visible = scoped.filter(a => (filter === 'all' || status(a) === filter) && `${a.title ?? ''} ${a.content} ${a.aiTopic ?? ''}`.toLocaleLowerCase().includes(query.toLocaleLowerCase()));
  const visibleDrafts = drafts.filter(d => (!brandId || d.brandId === brandId) && d.content.toLocaleLowerCase().includes(query.toLocaleLowerCase()));
  const selectedBrand = brands.find(b => b.id === (draft?.brandId ?? selected?.brandId));
  const channels = brandChannels(selectedBrand).filter(channel => channel.active !== false);
  const selectedChannel = channels.find(channel => channel.id === (draft ? draft.channelId ?? "legacy-group" : channelId)) ?? draft?.channelSnapshot;
  const openArticle = (article: Article) => { setSelected(article); setDraft(null); setMessage(''); setChannelId(brandChannels(brands.find(b => b.id === article.brandId)).find(c => c.active !== false)?.id ?? ''); };
  const openDraft = (value: Draft) => { setDraft(structuredClone(value)); setMessage(''); setSelected(articles.find(a => a.id === value.articleId) ?? { id: value.articleId, brandId: value.brandId, content: value.sourceContent ?? '', sourceUrl: value.sourceUrl ?? '', createdAt: value.createdAt }); };
  const prepare = async (branch: string) => {
    if (!selected) return; setBusy(true); setMessage('');
    try { const result = await api<Draft>(`/articles/${selected.id}/draft`, { branch, channelId }); setDraft(result); await load(); }
    catch (e) { setMessage((e as Error).message); } finally { setBusy(false); }
  };
  const evaluate = async (article: Article) => {
    setBusy(true); setMessage('');
    try { const result = await api<Article>(`/articles/${article.id}/evaluate`, {}); setArticles(items => items.map(a => a.id === result.id ? result : a)); if (selected?.id === result.id) setSelected(result); }
    catch (e) { setMessage((e as Error).message); } finally { setBusy(false); }
  };
  const saveDraft = async (approve: boolean) => {
    if (!draft) return; setBusy(true); setMessage('');
    try {
      const result = await api<Draft>(`/drafts/${draft.id}${approve ? '/approve' : ''}`, { content: draft.content, publishAccountId: draft.publishAccountId ?? '', review: draft.review ?? {}, plannedAt: draft.plannedAt || null }, approve ? 'POST' : 'PUT');
      setDraft(result); await load(); setMessage(approve ? 'Đã duyệt và lưu checklist. Bài chưa đăng Facebook, đang chờ worker.' : 'Đã lưu bản nháp và giờ dự kiến. Chưa đăng Facebook.');
    } catch (e) { setMessage((e as Error).message); } finally { setBusy(false); }
  };
  const editable = draft?.status === 'awaiting_review';
  const wordCount = draft?.content.trim().split(/\s+/).filter(Boolean).length ?? 0;

  return <div className="view-container article-library">
    <div className="view-header-strip article-library-header"><div><h2 className="view-title">Bài viết & duyệt đăng<HelpTooltip text="Lọc theo thương hiệu → chọn kênh → Repost/Rewrite → duyệt và đặt giờ dự kiến" /></h2></div><button className="btn-launch-cyber" onClick={onNavigateToStudio}><Plus size={15} /> Cào bài mới</button></div>
    <nav className="article-library-tabs" aria-label="Danh mục bài viết"><button type="button" aria-pressed={view === 'articles'} className={`article-library-tab ${view === 'articles' ? 'is-active' : ''}`} onClick={() => setView('articles')}><Files size={18}/><span>Bài đã thu thập</span><span className="article-tab-count">{articles.length}</span></button><button type="button" aria-pressed={view === 'drafts'} className={`article-library-tab ${view === 'drafts' ? 'is-active' : ''}`} onClick={() => setView('drafts')}><CalendarClock size={18}/><span>Bản nháp & lịch đăng</span><span className="article-tab-count">{drafts.length}</span></button></nav>
    {view === 'articles' && <div className="brand-summary-grid article-filter-grid">{tabs.map(([id, label]) => <button key={id} className={`cyber-panel article-count ${filter === id ? 'selected' : ''}`} onClick={() => setFilter(id)}><span>{label}</span><strong>{loading ? '—' : scoped.filter(a => id === 'all' || status(a) === id).length}</strong></button>)}</div>}
    <div className="workspace-toolbar"><select aria-label="Lọc thương hiệu" className="cyber-select-field" value={brandId} onChange={e => setBrandId(e.target.value)}><option value="">Tất cả thương hiệu</option>{brands.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}</select><input aria-label="Tìm bài viết" className="cyber-input-field article-search" placeholder="Tìm nội dung, chủ đề…" value={query} onChange={e => setQuery(e.target.value)} /><button className="btn-preset" disabled={busy} onClick={() => void load().catch(e => setMessage(e.message))}><RefreshCw size={15} /> Làm mới</button></div>
    {message && <p role="status" className="config-notice">{message}</p>}
    <div className="article-table-wrap">{view === 'articles' ? <table className="article-table"><thead><tr><th>Bài viết / media</th><th>Thương hiệu / chủ đề</th><th>Thu thập / tương tác</th><th>Clef đánh giá</th><th>Thao tác</th></tr></thead><tbody>{visible.map(article => <tr key={article.id}>
      <td><button className="article-title" onClick={() => openArticle(article)}>{article.title || article.content.slice(0, 90)}</button><a href={safeSource(article.sourceUrl)} target="_blank" rel="noreferrer">Xem bài gốc</a><small>{article.media?.filter(m => m.type === 'image').length ?? 0} ảnh · {article.media?.filter(m => m.type === 'video').length ?? 0} video</small></td>
      <td>{brands.find(b => b.id === article.brandId)?.name ?? 'Thương hiệu đã ngưng'}<small>{article.aiTopic || 'Chưa phân loại chủ đề'}</small></td><td>{displayDate(article.createdAt)}<small>{article.likes ?? 0} thích · {article.comments ?? 0} bình luận</small></td>
      <td>{tabs.find(([id]) => id === status(article))?.[1] ?? status(article)}{article.aiScore !== undefined && <strong> · {article.aiScore}/100</strong>}<small>{article.aiReason || 'Chưa có đánh giá Clef.'}</small>{stale(article) && <small className="policy-warning">Hồ sơ thương hiệu đã đổi; cần chấm lại.</small>}{article.aiWarnings?.map((text, i) => <small className="policy-warning" key={i}>{text}</small>)}</td>
      <td><div className="preset-buttons-row"><button disabled={busy} className="btn-preset" onClick={() => openArticle(article)}>Repost / Rewrite</button><button disabled={busy} className="btn-preset" onClick={() => void evaluate(article)}>Clef đánh giá</button></div></td>
    </tr>)}</tbody></table> : <table className="article-table"><thead><tr><th>Bản nháp</th><th>Thương hiệu / kênh đích</th><th>Giờ dự kiến (VN)</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>{visibleDrafts.map(item => <tr key={item.id}><td>{item.content.slice(0, 100)}<small>{item.branch === 'repost' ? 'Repost' : 'Bonsai viết lại'}</small></td><td>{brands.find(b => b.id === item.brandId)?.name}<small>{item.channelSnapshot?.name ?? 'Kênh cũ'}</small></td><td>{displayDate(item.plannedAt)}</td><td>{item.status === 'awaiting_review' ? 'Chờ duyệt' : item.status === 'approved_pending_worker' ? 'Đã duyệt · chờ worker' : item.status}</td><td><button className="btn-preset" onClick={() => openDraft(item)}>Mở bản nháp</button></td></tr>)}</tbody></table>}
      {loading ? <p className="workspace-empty">Đang tải…</p> : !(view === 'articles' ? visible.length : visibleDrafts.length) && <p className="workspace-empty">{view === 'articles' ? 'Chưa có bài khớp bộ lọc. Worker cào nền chưa triển khai; không có bài thu thập giả.' : 'Chưa có bản nháp khớp bộ lọc. Tạo Repost/Rewrite từ một bài đã thu thập.'}</p>}
    </div>
    {selected && <div className="workspace-modal-overlay"><section role="dialog" aria-modal="true" aria-label="Xem trước bài viết" className="cyber-panel workspace-editor"><header><div><h3>{draft ? draft.branch === 'repost' ? 'Duyệt bản repost' : 'Duyệt bản Bonsai viết lại' : 'Chuẩn bị nội dung'}</h3><p>{selectedBrand?.name}</p></div><button className="workspace-icon-button" disabled={busy} aria-label="Đóng" onClick={() => { setSelected(null); setDraft(null); }}><X size={20} /></button></header>
      <div className="article-preview">
        {draft ? <div className="config-notice"><strong>Kênh đích: {selectedChannel?.name ?? 'Kênh đã ngưng'}</strong><p>{selectedChannel?.type === 'page' ? 'Page' : 'Group'} · {selectedChannel?.minWords}–{selectedChannel?.maxWords} từ · {selectedChannel?.url}</p><p>{selectedChannel?.imageRequirements}</p></div> : <label className="setting-field">Kênh đăng đích<select className="cyber-select-field" value={channelId} onChange={e => setChannelId(e.target.value)}><option value="">Chọn kênh trong hồ sơ thương hiệu</option>{channels.map(channel => <option key={channel.id} value={channel.id}>{channel.name} · {channel.type === 'group' ? 'Group' : 'Page'} · {channel.minWords}–{channel.maxWords} từ</option>)}</select></label>}
        {busy && <p role="status">Đang xử lý… Chỉ một model AI chạy mỗi lần.</p>}
        {draft ? <label className="setting-field">Nội dung đăng · {wordCount} từ<textarea rows={12} className="cyber-textarea-field" disabled={!editable || busy} value={draft.content} onChange={e => setDraft({ ...draft, content: e.target.value, review: {} })} /><small>Thay nội dung sẽ bỏ đánh dấu checklist để kiểm tra lại.</small></label> : <p className="article-content">{selected.content}</p>}
        <a href={safeSource(draft?.sourceUrl ?? selected.sourceUrl)} target="_blank" rel="noreferrer">Đối chiếu bài gốc</a>
        <div className="article-media-grid">{(draft?.media ?? selected.media ?? []).map((media, i) => <div key={i}>{localMedia(media.url) ? media.type === 'video' ? <video controls preload="metadata" src={media.url} /> : <img src={media.url} alt={`Ảnh gốc ${i + 1}`} /> : <p>Media #{i + 1} chưa có file local hợp lệ.</p>}</div>)}</div>
        {!(draft?.media ?? selected.media ?? []).length && <p className="config-notice">Chưa có ảnh/video gốc. Chưa thể duyệt đăng.</p>}
        {draft ? <>
          <div className="policy-checklist"><h4>Checklist trước khi duyệt</h4><p className="cyber-hint">Đây là xác nhận của người duyệt. Tải được media không đồng nghĩa có quyền sử dụng.</p>{reviewFields(selectedBrand, selectedChannel).map(([key, label]) => <label key={key}><input type="checkbox" disabled={!editable || busy} checked={draft.review?.[key] === true} onChange={e => setDraft({ ...draft, review: { ...draft.review, [key]: e.target.checked } })} />{label}</label>)}</div>
          <label className="setting-field">Tài khoản đăng<select className="cyber-select-field" disabled={!editable || busy} value={draft.publishAccountId ?? ''} onChange={e => setDraft({ ...draft, publishAccountId: e.target.value })}><option value="">Chọn tài khoản đăng đã xác minh</option>{accounts.filter(a => a.role === 'publish' && a.enabled !== false).map(account => <option key={account.id} value={account.id} disabled={account.facebookStatus !== 'active'}>{account.name}{account.facebookStatus !== 'active' ? ' · chưa xác minh hoạt động' : ''}</option>)}</select></label>
          <label className="setting-field">Giờ đăng dự kiến (Việt Nam, tùy chọn)<input className="cyber-input-field" type="datetime-local" disabled={!editable || busy} value={localPlan(draft.plannedAt)} onChange={e => setDraft({ ...draft, plannedAt: e.target.value ? (Number.isFinite(Date.parse(`${e.target.value}:00+07:00`)) ? new Date(`${e.target.value}:00+07:00`).toISOString() : undefined) : undefined })} /></label>
          <div className="preset-buttons-row"><button className="btn-preset" disabled={busy || !editable} onClick={() => void saveDraft(false)}>Lưu bản nháp</button><button className="btn-launch-cyber" disabled={busy || !editable || !draft.publishAccountId || reviewFields(selectedBrand, selectedChannel).some(([key]) => !draft.review?.[key])} onClick={() => void saveDraft(true)}>{editable ? 'Duyệt · chờ đăng' : 'Đã duyệt · chờ worker'}</button></div>
        </> : <div className="preset-buttons-row"><button className="btn-preset" disabled={busy || !channelId} onClick={() => void prepare('repost')}>Repost nội dung gốc</button><button className="btn-launch-cyber" disabled={busy || !channelId} onClick={() => void prepare('rewrite')}>Rewrite bằng Bonsai</button><button className="btn-preset" disabled={busy} onClick={() => void evaluate(selected)}>Clef đánh giá</button></div>}
        {message && <p role="status" className="config-notice">{message}</p>}
        <p className="cyber-hint">Lưu giờ và duyệt không tự đăng Facebook. Worker cào nền và worker đăng bài chưa triển khai.</p>
      </div>
    </section></div>}
  </div>;
}
