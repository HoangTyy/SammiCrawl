import { BonsaiProgress } from '../BonsaiProgress';
import { ClefProgress } from '../ClefProgress';
import type { ClefEvaluationProgress } from '../ClefProgress';
import { useLiveRevision, LiveEditNotice } from '../../liveUpdates';
import { useDialogKeyboard } from '../useDialogKeyboard';
import { HelpTooltip } from '../HelpTooltip';
import { useCallback, useEffect, useRef, useState } from 'react';
import { RefreshCw, Plus, X, Files, CalendarClock } from 'lucide-react';
import { api } from '../../api';
import { brandChannels, reviewFields } from '../../brandPolicy';
import type { Brand, BrandChannel } from '../../brandPolicy';

type Media = { url: string; type: string; status?: string };
type Article = { deletedAt?: string; id: string; brandId: string; title?: string; content: string; sourceUrl: string; createdAt: string; likes?: number; comments?: number; aiStatus?: string; aiScore?: number; aiReason?: string; aiTopic?: string; aiWarnings?: string[]; aiBrandUpdatedAt?: string; aiEvaluatedAt?: string; aiImagesAnalyzed?: number; media?: Media[] };
type Draft = { rewriteVisualContext?: string; rewriteVisualConfirmedAt?: string; rewriteImagesProvided?: number; rewriteVisionModel?: string; id: string; articleId: string; brandId: string; channelId?: string; channelSnapshot?: BrandChannel; branch: string; content: string; sourceContent?: string; sourceUrl?: string; media: Media[]; status: string; publishAccountId?: string; review?: Record<string, boolean>; plannedAt?: string; createdAt: string };
type Account = { id: string; name: string; role: string; enabled?: boolean; facebookStatus?: string };
const safeSource = (url?: string) => { try { const u = new URL(url ?? ''); return u.protocol === 'https:' && (u.hostname === 'facebook.com' || u.hostname.endsWith('.facebook.com')) ? url : undefined; } catch { return undefined; } };
const localMedia = (url: string) => /^\/media\/[a-zA-Z0-9_./-]+$/.test(url) && !url.includes('..');
const localPlan = (value?: string) => value && Number.isFinite(Date.parse(value)) ? new Date(Date.parse(value) + 7 * 3600000).toISOString().slice(0, 16) : '';
const displayDate = (value?: string) => value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : 'Chưa đặt giờ';
const draftStates = [['awaiting_review', 'Chờ duyệt'], ['approved_pending_worker', 'Đã duyệt · chờ worker']];
const tabs = [['all', 'Tất cả'], ['recommended', 'Clef đề xuất'], ['review', 'Cần xem lại'], ['rejected', 'Không phù hợp'], ['pending', 'Chờ Clef']];

export function DataVaultView({ onNavigateToStudio, initialBrandId = '' }: { onNavigateToStudio: () => void; initialBrandId?: string }) {
 const liveRevision = useLiveRevision();
  const [articles, setArticles] = useState<Article[]>([]); const [drafts, setDrafts] = useState<Draft[]>([]); const [brands, setBrands] = useState<Brand[]>([]); const [accounts, setAccounts] = useState<Account[]>([]);
  const [brandId, setBrandId] = useState(initialBrandId); const [filter, setFilter] = useState('all'); const [query, setQuery] = useState(''); const [view, setView] = useState('articles');
  const [draftStatus, setDraftStatus] = useState('all'); const [draftChannel, setDraftChannel] = useState('');
  const [articlePage, setArticlePage] = useState(1);
  const [articlePageSize, setArticlePageSize] = useState(10);
  const [message, setMessage] = useState(''); const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false);
  const [visual, setVisual] = useState<{ context: string; imageFingerprint: string; imageCount: number } | null>(null);
  const [visualConfirmed, setVisualConfirmed] = useState(false);
  const visualArticleId = useRef('');
  const unclearCount = visual?.context.match(/\[không rõ\]/gi)?.length ?? 0;
  const [readingImages, setReadingImages] = useState(false);
  const [imageReadProgress, setImageReadProgress] = useState<number | null>(null);
  const [aiWaitSeconds, setAiWaitSeconds] = useState(0);
  const [rewriteProgress, setRewriteProgress] = useState<number | null>(null);
  const [clefEvaluation, setClefEvaluation] = useState<ClefEvaluationProgress | null>(null);
  const clefRunning = clefEvaluation?.state === 'running';
  useEffect(() => {
    if (!clefRunning) return;
    const started = Date.now();
    const timer = window.setInterval(() => setClefEvaluation(current => !current || current.state !== 'running' ? current : {
      ...current, seconds: Math.floor((Date.now() - started) / 1000),
      percent: Math.min(90, current.percent + Math.max(1, Math.round((90 - current.percent) / 18)))
    }), 1000);
    return () => window.clearInterval(timer);
  }, [clefRunning]);
  useEffect(() => {
    if (rewriteProgress === null || rewriteProgress >= 100) return;
    const timer = window.setInterval(() => setRewriteProgress(value => value === null || value >= 100 ? value : Math.min(90, value + Math.max(1, Math.round((90 - value) / 18)))), 1000);
    return () => window.clearInterval(timer);
  }, [rewriteProgress !== null && rewriteProgress < 100]);
  const bonsaiWaiting = readingImages || (rewriteProgress !== null && rewriteProgress < 100);
  useEffect(() => {
    if (!bonsaiWaiting) return;
    const started = Date.now(); setAiWaitSeconds(0);
    const timer = window.setInterval(() => {
      setAiWaitSeconds(Math.floor((Date.now() - started) / 1000));
      if (readingImages) setImageReadProgress(value => value === null ? value : Math.min(90, value + Math.max(1, Math.round((90 - value) / 18))));
    }, 1000);
    return () => window.clearInterval(timer);
  }, [bonsaiWaiting, readingImages]);
  const [selected, setSelected] = useState<Article | null>(null); const [draft, setDraft] = useState<Draft | null>(null); const [channelId, setChannelId] = useState('');
  const load = useCallback(async () => {
    const [a, b, p, d] = await Promise.all([api<Article[]>('/records/articles'), api<Brand[]>('/records/brands'), api<Account[]>('/records/accounts'), api<Draft[]>('/records/drafts')]);
    setArticles(a); setBrands(b); setAccounts(p); setDrafts(d);
  }, []);
  useEffect(() => { void load().catch(error => setMessage(error.message)).finally(() => setLoading(false)); }, [load, liveRevision]);
  const stale = (article: Article) => Boolean(article.aiEvaluatedAt && (article.aiBrandUpdatedAt ?? '') !== (brands.find(b => b.id === article.brandId)?.updatedAt ?? ''));
  const status = (article: Article) => stale(article) ? 'review' : article.aiStatus ?? 'pending';
  const scoped = articles.filter(a => !brandId || a.brandId === brandId);
  const visible = scoped.filter(a => (filter === 'all' || status(a) === filter) && `${a.title ?? ''} ${a.content} ${a.aiTopic ?? ''}`.toLocaleLowerCase().includes(query.toLocaleLowerCase()));
  const articlePageCount = Math.max(1, Math.ceil(visible.length / articlePageSize));
  const currentArticlePage = Math.min(articlePage, articlePageCount);
  const articleOffset = (currentArticlePage - 1) * articlePageSize;
  const pagedArticles = visible.slice(articleOffset, articleOffset + articlePageSize);
  useEffect(() => { setArticlePage(1); }, [brandId, filter, query, articlePageSize]);
  useEffect(() => { setArticlePage(page => Math.min(page, articlePageCount)); }, [articlePageCount]);
  const scopedDrafts = drafts.filter(d => !brandId || d.brandId === brandId);
  const draftChannelOptions = Array.from(new Map(scopedDrafts.map(d => [`${d.brandId}:${d.channelId ?? 'legacy-group'}`, { name: d.channelSnapshot?.name ?? 'Kênh cũ', brand: brands.find(b => b.id === d.brandId)?.name ?? 'Thương hiệu đã ngưng' }])).entries());
  const visibleDrafts = scopedDrafts.filter(d => (draftStatus === 'all' || d.status === draftStatus) && (!draftChannel || `${d.brandId}:${d.channelId ?? 'legacy-group'}` === draftChannel) && d.content.toLocaleLowerCase().includes(query.toLocaleLowerCase()));
  const selectedBrand = brands.find(b => b.id === (draft?.brandId ?? selected?.brandId));
  const channels = brandChannels(selectedBrand).filter(channel => channel.active !== false);
  const selectedChannel = channels.find(channel => channel.id === (draft ? draft.channelId ?? "legacy-group" : channelId)) ?? draft?.channelSnapshot;
  const openArticle = (article: Article) => { if (busy) return; setImageReadProgress(null); setSelected(article); setDraft(null); if (visualArticleId.current !== article.id) { setVisual(null); visualArticleId.current = ''; } setVisualConfirmed(false); setRewriteProgress(null); setMessage(''); setChannelId(brandChannels(brands.find(b => b.id === article.brandId)).find(c => c.active !== false)?.id ?? ''); };
  const openDraft = (value: Draft) => { if (busy) return; setImageReadProgress(null); setDraft(structuredClone(value)); setVisual(null); setVisualConfirmed(false); setRewriteProgress(null); setMessage(''); setSelected(articles.find(a => a.id === value.articleId) ?? { id: value.articleId, brandId: value.brandId, content: value.sourceContent ?? '', sourceUrl: value.sourceUrl ?? '', createdAt: value.createdAt }); };
  const prepare = async (branch: string) => {
    if (!selected || busy) return;
    setClefEvaluation(null);
    if (branch === 'rewrite' && selected.media?.length && !visual) {
      setBusy(true); setReadingImages(true); setImageReadProgress(0); setAiWaitSeconds(0); setMessage(''); setRewriteProgress(null);
      try { setVisual(await api<{ context: string; imageFingerprint: string; imageCount: number }>(`/articles/${selected.id}/visual-context`, {})); visualArticleId.current = selected.id; setVisualConfirmed(false); setImageReadProgress(100); }
      catch(e) { setImageReadProgress(null); setMessage((e as Error).message); }
      finally { setBusy(false); setReadingImages(false); }
      return;
    }
    if (branch === 'rewrite' && visual && !visualConfirmed) { setMessage('Hãy đối chiếu, sửa và xác nhận ghi chú ảnh trước.'); return; }
    setBusy(true); setMessage(''); setImageReadProgress(null); setAiWaitSeconds(0); setRewriteProgress(branch === 'rewrite' ? 0 : null);
    try { const result = await api<Draft>(`/articles/${selected.id}/draft`, { branch, channelId, visualContext: visual?.context, imageFingerprint: visual?.imageFingerprint, visualContextConfirmed: visualConfirmed }); setDraft(result); if (branch === 'rewrite') setRewriteProgress(100); await load(); }
    catch (e) { setRewriteProgress(null); setMessage((e as Error).message); } finally { setBusy(false); }
  };
  const evaluate = async (article: Article) => {
    if (busy) return;
    setBusy(true); setMessage(''); setImageReadProgress(null); setRewriteProgress(null);
    setClefEvaluation({ articleId: article.id, title: article.title || article.content.slice(0, 90), percent: 0, seconds: 0, state: 'running' });
    try {
      const result = await api<Article>(`/articles/${article.id}/evaluate`, {});
      setArticles(items => result.deletedAt ? items.filter(a => a.id !== result.id) : items.map(a => a.id === result.id ? result : a));
      setSelected(current => current?.id === result.id ? result : current);
      const verdict = result.aiStatus === 'recommended' ? 'Phù hợp' : result.aiStatus === 'rejected' ? 'Không phù hợp · đã loại khỏi thư viện' : 'Cần xem lại';
      setClefEvaluation(current => current ? { ...current, state: 'done', percent: 100, result: `${verdict}${result.aiScore !== undefined ? ` · ${result.aiScore}/100` : ''}. ${result.aiReason ?? ''}` } : null);
    } catch (e) {
      const error = (e as Error).message;
      setClefEvaluation(current => current ? { ...current, state: 'error', result: error } : null);
      setMessage(error);
    } finally { setBusy(false); }
  };
  const saveDraft = async (approve: boolean) => {
    if (!draft) return; setBusy(true); setMessage('');
    try {
      const result = await api<Draft>(`/drafts/${draft.id}${approve ? '/approve' : ''}`, { content: draft.content, publishAccountId: draft.publishAccountId ?? '', review: draft.review ?? {}, plannedAt: draft.plannedAt || null }, approve ? 'POST' : 'PUT');
      setDraft(result); await load(); setMessage(approve ? 'Đã duyệt và lưu checklist. Bài chưa đăng Facebook, đang chờ worker.' : 'Đã lưu bản nháp và giờ dự kiến. Chưa đăng Facebook.');
    } catch (e) { setMessage((e as Error).message); } finally { setBusy(false); }
  };
  const canDismissAi = readingImages || clefRunning || (rewriteProgress !== null && rewriteProgress < 100);
  const closeDialog = () => { setSelected(null); setDraft(null); };
  const dialogRef = useDialogKeyboard(Boolean(selected), closeDialog, busy && !canDismissAi);
  const editable = draft?.status === 'awaiting_review';
  const wordCount = draft?.content.trim().split(/\s+/).filter(Boolean).length ?? 0;

  return <div className="view-container article-library">
    <div className="view-header-strip article-library-header"><div><h2 className="view-title">Bài viết & duyệt đăng<HelpTooltip text="Lọc theo thương hiệu → chọn kênh → Repost/Rewrite → duyệt và đặt giờ dự kiến" /></h2></div><button className="btn-launch-cyber" onClick={onNavigateToStudio}><Plus size={15} /> Cào bài mới</button></div>
    <nav className="article-library-tabs" aria-label="Danh mục bài viết"><button type="button" aria-pressed={view === 'articles'} className={`article-library-tab ${view === 'articles' ? 'is-active' : ''}`} onClick={() => setView('articles')}><Files size={18}/><span>Bài đã thu thập</span><span className="article-tab-count">{articles.length}</span></button><button type="button" aria-pressed={view === 'drafts'} className={`article-library-tab ${view === 'drafts' ? 'is-active' : ''}`} onClick={() => setView('drafts')}><CalendarClock size={18}/><span>Bản nháp & lịch đăng</span><span className="article-tab-count">{drafts.length}</span></button></nav>
    {view === 'articles' && <div className="brand-summary-grid article-filter-grid">{tabs.map(([id, label]) => <button key={id} className={`cyber-panel article-count ${filter === id ? 'selected' : ''}`} onClick={() => setFilter(id)}><span>{label}</span><strong>{loading ? '—' : scoped.filter(a => id === 'all' || status(a) === id).length}</strong></button>)}</div>}
    <div className="workspace-toolbar"><select aria-label="Lọc thương hiệu" className="cyber-select-field" value={brandId} onChange={e => { setBrandId(e.target.value); setDraftChannel(''); }}><option value="">Tất cả thương hiệu</option>{brands.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}</select><input aria-label="Tìm bài viết" className="cyber-input-field article-search" placeholder="Tìm nội dung, chủ đề…" value={query} onChange={e => setQuery(e.target.value)} /><button className="btn-preset" disabled={busy} onClick={() => void load().catch(e => setMessage(e.message))}><RefreshCw size={15} /> Làm mới</button></div>
    {view === 'drafts' && <div className="draft-filter-toolbar"><label className="setting-field">Trạng thái<select aria-label="Lọc trạng thái bản nháp" className="cyber-select-field" value={draftStatus} onChange={e => setDraftStatus(e.target.value)}><option value="all">Tất cả trạng thái</option>{draftStates.map(([value, label]) => <option key={value} value={value}>{label} ({scopedDrafts.filter(d => d.status === value).length})</option>)}</select></label><label className="setting-field">Kênh đích<select aria-label="Lọc kênh bản nháp" className="cyber-select-field" value={draftChannel} onChange={e => setDraftChannel(e.target.value)}><option value="">Tất cả kênh</option>{draftChannelOptions.map(([value, channel]) => <option key={value} value={value}>{channel.name} · {channel.brand}</option>)}</select></label><p className="draft-result-count" role="status">{loading ? 'Đang tải…' : `${visibleDrafts.length}/${scopedDrafts.length} bản nháp`}</p></div>}
    {!selected && <><ClefProgress evaluation={clefEvaluation} /><BonsaiProgress reading={readingImages} imageProgress={imageReadProgress} rewriteProgress={rewriteProgress} seconds={aiWaitSeconds} /></>}
    {message && <p role="status" className="config-notice">{message}</p>}
    <div className="article-table-wrap">{view === 'articles' ? <table className="article-table"><thead><tr><th>Bài viết / media</th><th>Thương hiệu / chủ đề</th><th>Thu thập / tương tác</th><th>Clef đánh giá</th><th>Thao tác</th></tr></thead><tbody>{pagedArticles.map(article => <tr key={article.id}>
      <td data-label="Bài viết / media"><button className="article-title" disabled={busy} onClick={() => openArticle(article)}>{article.title || article.content.slice(0, 90)}</button><a href={safeSource(article.sourceUrl)} target="_blank" rel="noreferrer">Xem bài gốc</a><small>{article.media?.filter(m => m.type === 'image').length ?? 0} ảnh · {article.media?.filter(m => m.type === 'video').length ?? 0} video</small></td>
      <td data-label="Thương hiệu / chủ đề">{brands.find(b => b.id === article.brandId)?.name ?? 'Thương hiệu đã ngưng'}<small>{article.aiTopic || 'Chưa phân loại chủ đề'}</small></td><td data-label="Thu thập / tương tác">{displayDate(article.createdAt)}<small>{article.likes ?? '—'} thích · {article.comments ?? '—'} bình luận</small></td>
      <td data-label="Clef đánh giá">{clefRunning && clefEvaluation?.articleId === article.id && <div className="clef-row-progress" role="status"><strong>Clef đang đánh giá…</strong><progress aria-label="Tiến độ Clef của bài đang đánh giá (ước tính)" max={100} value={clefEvaluation.percent} /><small>{clefEvaluation.percent}% · ước tính · đã chờ {clefEvaluation.seconds} giây</small></div>}{tabs.find(([id]) => id === status(article))?.[1] ?? status(article)}{article.aiScore !== undefined && <strong> · {article.aiScore}/100</strong>}<small>{article.aiReason || 'Chưa có đánh giá Clef.'}</small>{stale(article) && <small className="policy-warning">Hồ sơ thương hiệu đã đổi; cần chấm lại.</small>}{article.aiWarnings?.map((text, i) => <small className="policy-warning" key={i}>{text}</small>)}</td>
      <td data-label="Thao tác"><div className="preset-buttons-row"><button disabled={busy} className="btn-preset" onClick={() => openArticle(article)}>Repost / Rewrite</button><button disabled={busy} className="btn-preset" onClick={() => void evaluate(article)}>{clefRunning && clefEvaluation?.articleId === article.id ? 'Clef đang đánh giá…' : 'Clef đánh giá'}</button></div></td>
    </tr>)}</tbody></table> : <table className="article-table"><thead><tr><th>Bản nháp</th><th>Thương hiệu / kênh đích</th><th>Giờ dự kiến (VN)</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>{visibleDrafts.map(item => <tr key={item.id}><td data-label="Bản nháp">{item.content.slice(0, 100)}<small>{item.branch === 'repost' ? 'Repost' : 'Bonsai viết lại'}</small></td><td data-label="Thương hiệu / kênh đích">{brands.find(b => b.id === item.brandId)?.name}<small>{item.channelSnapshot?.name ?? 'Kênh cũ'}</small></td><td data-label="Giờ dự kiến (VN)">{displayDate(item.plannedAt)}</td><td data-label="Trạng thái"><span className="draft-status-tag" data-status={item.status}>{draftStates.find(([value]) => value === item.status)?.[1] ?? item.status}</span></td><td data-label="Thao tác"><button className="btn-preset" disabled={busy} onClick={() => openDraft(item)}>Mở bản nháp</button></td></tr>)}</tbody></table>}
      {loading ? <p className="workspace-empty">Đang tải…</p> : !(view === 'articles' ? visible.length : visibleDrafts.length) && <p className="workspace-empty">{view === 'articles' ? 'Chưa có bài khớp bộ lọc. Danh sách tự cập nhật khi worker lưu bài mới.' : 'Chưa có bản nháp khớp bộ lọc. Tạo Repost/Rewrite từ một bài đã thu thập.'}</p>}
    </div>
    {view === 'articles' && <nav className="article-pagination" aria-label="Phân trang bài viết">
      <label className="article-page-size">Số bài/trang<select className="cyber-select-field" aria-label="Số bài mỗi trang" disabled={loading} value={articlePageSize} onChange={e => setArticlePageSize(Number(e.target.value))}>{[10, 20, 50].map(size => <option key={size} value={size}>{size}</option>)}</select></label>
      <span role="status">{loading ? 'Đang tải…' : `Hiển thị ${visible.length ? articleOffset + 1 : 0}–${Math.min(articleOffset + articlePageSize, visible.length)}/${visible.length} bài`}</span>
      <div className="article-page-controls">
        <button type="button" className="btn-preset" aria-label="Trang đầu" disabled={loading || currentArticlePage === 1} onClick={() => setArticlePage(1)}>Đầu</button>
        <button type="button" className="btn-preset" aria-label="Trang trước" disabled={loading || currentArticlePage === 1} onClick={() => setArticlePage(currentArticlePage - 1)}>Trước</button>
        <span aria-live="polite">Trang {currentArticlePage}/{articlePageCount}</span>
        <button type="button" className="btn-preset" aria-label="Trang sau" disabled={loading || currentArticlePage === articlePageCount} onClick={() => setArticlePage(currentArticlePage + 1)}>Sau</button>
        <button type="button" className="btn-preset" aria-label="Trang cuối" disabled={loading || currentArticlePage === articlePageCount} onClick={() => setArticlePage(articlePageCount)}>Cuối</button>
      </div>
    </nav>}
    {selected && <div className="workspace-modal-overlay"><section ref={dialogRef} role="dialog" aria-modal="true" aria-label="Xem trước bài viết" className="cyber-panel workspace-editor article-dialog"><LiveEditNotice /><header><div><h3>{draft ? draft.branch === 'repost' ? 'Duyệt bản repost' : 'Duyệt bản Bonsai viết lại' : 'Chuẩn bị nội dung'}</h3><p>{selectedBrand?.name}</p></div><button className="workspace-icon-button" disabled={busy && !canDismissAi} aria-label="Đóng" onClick={closeDialog}><X size={20} /></button></header>
      {(clefEvaluation || imageReadProgress !== null || rewriteProgress !== null) && <div className="article-dialog-progress"><ClefProgress evaluation={clefEvaluation} /><BonsaiProgress reading={readingImages} imageProgress={imageReadProgress} rewriteProgress={rewriteProgress} seconds={aiWaitSeconds} /></div>}
      <div className="article-preview">
        {draft ? <div className="config-notice"><strong>Kênh đích: {selectedChannel?.name ?? 'Kênh đã ngưng'}</strong><p>{selectedChannel?.type === 'page' ? 'Page' : 'Group'} · {selectedChannel?.minWords}–{selectedChannel?.maxWords} từ · {selectedChannel?.url}</p><p>{selectedChannel?.imageRequirements}</p></div> : <label className="setting-field">Kênh đăng đích<select className="cyber-select-field" value={channelId} onChange={e => setChannelId(e.target.value)}><option value="">Chọn kênh trong hồ sơ thương hiệu</option>{channels.map(channel => <option key={channel.id} value={channel.id}>{channel.name} · {channel.type === 'group' ? 'Group' : 'Page'} · {channel.minWords}–{channel.maxWords} từ</option>)}</select></label>}
        {busy && !clefRunning && !bonsaiWaiting && <p role="status">Đang lưu dữ liệu…</p>}
        {draft?.branch === 'rewrite' && <p className="config-notice">{draft.rewriteVisualConfirmedAt ? `Đã đối chiếu ${draft.rewriteImagesProvided} ảnh. Bài được viết từ ghi chú ảnh đã xác nhận; vẫn cần duyệt nội dung.` : (draft.rewriteImagesProvided ?? 0) > 0 ? 'Bản nháp cũ chưa có bước xác nhận chữ trong ảnh. Nên tạo lại từ bài nguồn.' : 'Bản viết lại chỉ có phần chữ, không có ảnh.'}</p>}

        {draft ? <label className="setting-field">Nội dung đăng · {wordCount} từ<textarea rows={12} className="cyber-textarea-field" disabled={!editable || busy} value={draft.content} onChange={e => setDraft({ ...draft, content: e.target.value, review: {} })} /><small>Thay nội dung sẽ bỏ đánh dấu checklist để kiểm tra lại.</small></label> : <p className="article-content">{selected.content}</p>}
        <a href={safeSource(draft?.sourceUrl ?? selected.sourceUrl)} target="_blank" rel="noreferrer">Đối chiếu bài gốc</a>
        <div className="article-media-grid">{(draft?.media ?? selected.media ?? []).map((media, i) => <div key={i}>{localMedia(media.url) ? media.type === 'video' ? <video controls preload="metadata" src={media.url} /> : <img src={media.url} alt={`Ảnh gốc ${i + 1}`} /> : <p>Media #{i + 1} chưa có file local hợp lệ.</p>}</div>)}</div>
        {!draft && visual && <div className="config-notice visual-context-review"><h4>Đối chiếu chữ và ý trong ảnh</h4>{unclearCount >= 5 && <p role="alert" className="policy-warning">Kết quả đọc ảnh chưa đạt: có {unclearCount} đoạn [không rõ], có thể bị lặp. Không dùng ghi chú này để rewrite. Hãy đối chiếu và sửa theo ảnh, hoặc bỏ kết quả để đọc lại.</p>}<button className="btn-preset" type="button" disabled={busy} onClick={() => { setVisual(null); visualArticleId.current = ''; setVisualConfirmed(false); setImageReadProgress(null); setMessage('Đã bỏ ghi chú ảnh. Bấm Rewrite để đọc lại ảnh; chưa viết bài.'); }}>Bỏ kết quả để đọc lại ảnh</button><p>AI có thể đọc sai. Sửa ghi chú dưới đây theo ảnh gốc; chữ viết tắt chỉ giải thích khi bạn biết chắc. Không xác nhận nếu còn [không rõ].</p><label className="setting-field">Nội dung ảnh dùng để viết bài<textarea rows={7} className="cyber-textarea-field" maxLength={8000} disabled={busy} value={visual.context} onChange={e => { setVisual({ ...visual, context: e.target.value }); setVisualConfirmed(false); }} /><small>{visual.context.length}/8000 ký tự · tối đa 4 ảnh JPEG/PNG/WebP, 5 MB mỗi ảnh</small></label><label className="visual-confirm"><input type="checkbox" disabled={busy || !visual.context.trim() || unclearCount > 0} checked={visualConfirmed} onChange={e => setVisualConfirmed(e.target.checked)} />Tôi đã đối chiếu và sửa đúng chữ, số, tên và ý của ảnh</label></div>}
        {draft?.rewriteVisualContext && <details className="config-notice"><summary>Ghi chú ảnh đã xác nhận dùng cho bản nháp</summary><p className="article-content">{draft.rewriteVisualContext}</p></details>}
        {!(draft?.media ?? selected.media ?? []).length && <p className="config-notice">Chưa có ảnh/video gốc. Chưa thể duyệt đăng.</p>}
        {draft ? <>
          <div className="policy-checklist"><h4>Checklist trước khi duyệt</h4><p className="cyber-hint">Đây là xác nhận của người duyệt. Tải được media không đồng nghĩa có quyền sử dụng.</p>{reviewFields(selectedBrand, selectedChannel).map(([key, label]) => <label key={key}><input type="checkbox" disabled={!editable || busy} checked={draft.review?.[key] === true} onChange={e => setDraft({ ...draft, review: { ...draft.review, [key]: e.target.checked } })} />{label}</label>)}</div>
          <label className="setting-field">Tài khoản đăng<select className="cyber-select-field" disabled={!editable || busy} value={draft.publishAccountId ?? ''} onChange={e => setDraft({ ...draft, publishAccountId: e.target.value })}><option value="">Chọn tài khoản đăng đã xác minh</option>{accounts.filter(a => a.role === 'publish' && a.enabled !== false).map(account => <option key={account.id} value={account.id} disabled={account.facebookStatus !== 'active'}>{account.name}{account.facebookStatus !== 'active' ? ' · chưa xác minh hoạt động' : ''}</option>)}</select></label>
          <label className="setting-field">Giờ đăng dự kiến (Việt Nam, tùy chọn)<input className="cyber-input-field" type="datetime-local" disabled={!editable || busy} value={localPlan(draft.plannedAt)} onChange={e => setDraft({ ...draft, plannedAt: e.target.value ? (Number.isFinite(Date.parse(`${e.target.value}:00+07:00`)) ? new Date(`${e.target.value}:00+07:00`).toISOString() : undefined) : undefined })} /></label>
          <div className="preset-buttons-row"><button className="btn-preset" disabled={busy || !editable} onClick={() => void saveDraft(false)}>Lưu bản nháp</button><button className="btn-launch-cyber" disabled={busy || !editable || !draft.publishAccountId || reviewFields(selectedBrand, selectedChannel).some(([key]) => !draft.review?.[key])} onClick={() => void saveDraft(true)}>{editable ? 'Duyệt · chờ đăng' : 'Đã duyệt · chờ worker'}</button></div>
        </> : <div className="preset-buttons-row"><button className="btn-preset" disabled={busy || !channelId || Boolean(selected.deletedAt)} onClick={() => void prepare('repost')}>Repost nội dung gốc</button><button className="btn-launch-cyber" disabled={busy || !channelId || Boolean(selected.deletedAt) || Boolean(visual && !visualConfirmed)} onClick={() => void prepare('rewrite')}>Rewrite</button><button className="btn-preset" disabled={busy} onClick={() => void evaluate(selected)}>{clefRunning && clefEvaluation?.articleId === selected.id ? 'Clef đang đánh giá…' : 'Clef đánh giá'}</button></div>}
        {message && <p role="status" className="config-notice">{message}</p>}
        <p className="cyber-hint">Lưu giờ và duyệt không tự đăng Facebook. Worker đăng bài chưa triển khai.</p>
      </div>
    </section></div>}
  </div>;
}
