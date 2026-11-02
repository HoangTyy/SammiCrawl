import { BonsaiProgress } from '../BonsaiProgress';
import { ArticleMetadata } from '../ArticleMetadata';
import type { ArticleMetadataFields } from '../ArticleMetadata';
import { ClefProgress } from '../ClefProgress';
import type { ClefEvaluationProgress } from '../ClefProgress';
import { useLiveRevision, LiveEditNotice } from '../../liveUpdates';
import { useDialogKeyboard } from '../useDialogKeyboard';
import { HelpTooltip } from '../HelpTooltip';
import { useCallback, useEffect, useRef, useState } from 'react';
import { RefreshCw, Plus, X, Files, CalendarClock, FilePenLine, Trash2, BadgeCheck, Sparkles, LoaderCircle } from 'lucide-react';
import { api } from '../../api';
import { brandChannels, reviewFields } from '../../brandPolicy';
import type { Brand, BrandChannel } from '../../brandPolicy';

type Media = { url: string; type: string; status?: string };
type Article = ArticleMetadataFields & { sourceGroupName?: string; sourceGroupUrl?: string; aiChannelMatches?: {channelId:string;name:string;status:string;score:number}[]; deletedAt?: string; id: string; brandId: string; title?: string; content: string; sourceUrl: string; createdAt: string; likes?: number; comments?: number; aiStatus?: string; aiScore?: number; aiReason?: string; aiTopic?: string; aiWarnings?: string[]; aiBrandUpdatedAt?: string; aiEvaluatedAt?: string; aiImagesAnalyzed?: number; media?: Media[] };
type Draft = { shareCode?:string; shareMessage?:string; sourceAuthor?:string; publishedUrl?:string; publishedAt?:string; publicationMethod?:string; updatedAt?:string; rewriteInputMode?: string; rewriteVisualContext?: string; rewriteVisualConfirmedAt?: string; rewriteImagesProvided?: number; rewriteVisionModel?: string; id: string; articleId: string; brandId: string; channelId?: string; channelSnapshot?: BrandChannel; branch: string; content: string; sourceContent?: string; sourceUrl?: string; media: Media[]; status: string; publishAccountId?: string; review?: Record<string, boolean>; plannedAt?: string; createdAt: string };
type RewriteJob = { id:string; articleId:string; channelId:string; state:string; queuePosition?:number; createdAt:string; result?:Draft; failure?:{error:string;code:string} };
type Account = { id: string; name: string; role: string; enabled?: boolean; facebookStatus?: string };
const safeSource = (url?: string) => { try { const u = new URL(url ?? ''); return u.protocol === 'https:' && (u.hostname === 'facebook.com' || u.hostname.endsWith('.facebook.com')) ? url : undefined; } catch { return undefined; } };
const localMedia = (url: string) => /^\/media\/[a-zA-Z0-9_./-]+$/.test(url) && !url.includes('..');
const localPlan = (value?: string) => value && Number.isFinite(Date.parse(value)) ? new Date(Date.parse(value) + 7 * 3600000).toISOString().slice(0, 16) : '';
const displayDate = (value?: string) => value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : 'Chưa đặt giờ';
const draftStates = [['awaiting_review', 'Chờ duyệt'], ['approved_pending_worker', 'Đã duyệt · chờ worker'], ['published','Đã đăng'],['share_queued','Chờ chia sẻ'],['share_running','Đang chuẩn bị chia sẻ'],['share_sending','Đang gửi chia sẻ'],['share_pending_approval','Chờ nhóm duyệt'],['share_submitted','Đã gửi · chưa có link'],['share_unknown','Chưa rõ kết quả'],['share_failed','Chia sẻ lỗi'],['share_cancelled','Đã hủy chia sẻ']];
const tabs = [['all', 'Tất cả'], ['recommended', 'Clef đề xuất'], ['review', 'Cần xem lại'], ['rejected', 'Không phù hợp'], ['pending', 'Chờ Clef']];

function SourceMedia({media,index}:{media:Media;index:number}){
  const [failed,setFailed]=useState(false);
  if(!localMedia(media.url))return <p>Media #{index+1} chưa có file local hợp lệ.</p>;
  if(failed)return <p role="status" className="config-notice">Không tìm thấy hoặc không đọc được media #{index+1} trên máy đang mở web. File có thể đang nằm trên máy cào khác; cần đồng bộ kho media.</p>;
  return media.type==='video'?<video controls preload="metadata" src={media.url} onError={()=>setFailed(true)}/>:<img src={media.url} alt={`Ảnh gốc ${index+1}`} onError={()=>setFailed(true)}/>;
}
export function DataVaultView({ onNavigateToStudio, initialBrandId = '' }: { onNavigateToStudio: () => void; initialBrandId?: string }) {
 const liveRevision = useLiveRevision();
  const [articles, setArticles] = useState<Article[]>([]); const [drafts, setDrafts] = useState<Draft[]>([]); const [brands, setBrands] = useState<Brand[]>([]); const [accounts, setAccounts] = useState<Account[]>([]);
  const [brandId, setBrandId] = useState(initialBrandId); const [filter, setFilter] = useState('all'); const [query, setQuery] = useState(''); const [view, setView] = useState('articles');
  const [draftStatus, setDraftStatus] = useState('all'); const [draftChannel, setDraftChannel] = useState('');
  const [articlePage, setArticlePage] = useState(1);
  const [articlePageSize, setArticlePageSize] = useState(10);
  const [message, setMessage] = useState(''); const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false);
  const [rewriteJobs,setRewriteJobs]=useState<RewriteJob[]>([]);
  const [rewriteQueueError,setRewriteQueueError]=useState('');
  const refreshRewriteJobs=useCallback(async()=>{try{setRewriteJobs(await api<RewriteJob[]>('/rewrite-jobs'));setRewriteQueueError('');}catch(e){setRewriteQueueError((e as Error).message);}},[]);
  useEffect(()=>{void refreshRewriteJobs();const timer=window.setInterval(()=>void refreshRewriteJobs(),3000);return()=>window.clearInterval(timer);},[refreshRewriteJobs]);
  const [aiWaitSeconds, setAiWaitSeconds] = useState(0);
  const [rewriteProgress, setRewriteProgress] = useState<number | null>(null);
  const [rewriteWithImages, setRewriteWithImages] = useState(false);
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
  const bonsaiWaiting = rewriteProgress !== null && rewriteProgress < 100;
  useEffect(() => {
    if (!bonsaiWaiting) return;
    const started = Date.now(); setAiWaitSeconds(0);
    const timer = window.setInterval(() => {
      setAiWaitSeconds(Math.floor((Date.now() - started) / 1000));
    }, 1000);
    return () => window.clearInterval(timer);
  }, [bonsaiWaiting]);
  const [publishedUrl,setPublishedUrl]=useState('');
  const [publishedWhen,setPublishedWhen]=useState('');
  const [publicationConfirmed,setPublicationConfirmed]=useState(false);
  const publicationRef=useRef<HTMLElement>(null);
  const [jumpToPublish,setJumpToPublish]=useState(false);
  const [sourcePublication,setSourcePublication]=useState(false);
  const [sourceShare,setSourceShare]=useState(false);
  const [deletingArticle,setDeletingArticle]=useState<Article|null>(null);
  const [shareAccount,setShareAccount]=useState('');
  const [shareConfirmed,setShareConfirmed]=useState(false);
  const [selected, setSelected] = useState<Article | null>(null); const [draft, setDraft] = useState<Draft | null>(null); const [channelId, setChannelId] = useState('');
  useEffect(()=>{if(jumpToPublish&&publicationRef.current){publicationRef.current.scrollIntoView({block:'start'});setJumpToPublish(false);}},[draft?.id,selected?.id,jumpToPublish,sourcePublication]);
  useEffect(()=>{setPublishedUrl(draft?.publishedUrl??'');setPublishedWhen(localPlan(draft?.publishedAt??new Date().toISOString()));setPublicationConfirmed(false);},[draft?.id,selected?.id]);
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
  useEffect(()=>{if(draft?.branch==='share'){const current=drafts.find(d=>d.id===draft.id);if(current)setDraft(current);}},[drafts,draft?.id,draft?.branch]);
  const scopedDrafts = drafts.filter(d => !brandId || d.brandId === brandId);
  const draftChannelOptions = Array.from(new Map(scopedDrafts.map(d => [`${d.brandId}:${d.channelId ?? 'legacy-group'}`, { name: d.channelSnapshot?.name ?? 'Kênh cũ', brand: brands.find(b => b.id === d.brandId)?.name ?? 'Thương hiệu đã ngưng' }])).entries());
  const visibleDrafts = scopedDrafts.filter(d => (draftStatus === 'all' || d.status === draftStatus) && (!draftChannel || `${d.brandId}:${d.channelId ?? 'legacy-group'}` === draftChannel) && d.content.toLocaleLowerCase().includes(query.toLocaleLowerCase()));
  const selectedBrand = brands.find(b => b.id === (draft?.brandId ?? selected?.brandId));
  const channels = brandChannels(selectedBrand).filter(channel => channel.active !== false);
  const selectedChannel = channels.find(channel => channel.id === (draft ? draft.channelId ?? "legacy-group" : channelId)) ?? draft?.channelSnapshot;
  useEffect(()=>{setShareAccount(selectedChannel?.publishAccountId??'');setShareConfirmed(false);},[selected?.id,channelId,selectedChannel?.publishAccountId]);
  const openArticle = (article: Article) => { if (busy) return; setSourcePublication(false);setSourceShare(false);setShareConfirmed(false); setSelected(article); setDraft(null); setRewriteProgress(null); setMessage(''); setChannelId(brandChannels(brands.find(b => b.id === article.brandId)).find(c => c.active !== false)?.id ?? ''); };
  const openDraft = (value: Draft) => { if (busy) return; setSourcePublication(false);setSourceShare(false);setShareConfirmed(false); setDraft(structuredClone(value)); setRewriteProgress(null); setMessage(''); setSelected(articles.find(a => a.id === value.articleId) ?? { id: value.articleId, brandId: value.brandId, content: value.sourceContent ?? '', sourceUrl: value.sourceUrl ?? '', createdAt: value.createdAt }); };
  const prepare = async (branch: string) => {
    if (!selected || busy) return;
    if (branch === 'rewrite') {
      setBusy(true); setMessage('');
      try {
        await api<RewriteJob>('/rewrite-jobs', {id:crypto.randomUUID(),articleId:selected.id,channelId});
        await refreshRewriteJobs(); setMessage('Đã thêm vào hàng đợi rewrite. Bạn có thể đóng và chọn bài khác.');
      } catch(e) { setMessage((e as Error).message); }
      finally {setBusy(false);}
      return;
    }
    setClefEvaluation(null);
    setRewriteWithImages(Boolean(selected.media?.some(media => media.type === 'image')));
    setBusy(true); setMessage(''); setAiWaitSeconds(0); setRewriteProgress(branch === 'rewrite' ? 0 : null);
    try { const result = await api<Draft>(`/articles/${selected.id}/draft`, { branch, channelId }); setDraft(result); if (branch === 'rewrite') setRewriteProgress(100); await load(); }
    catch (e) { setRewriteProgress(null); setMessage((e as Error).message); } finally { setBusy(false); }
  };
  const evaluate = async (article: Article) => {
    if (busy) return;
    setBusy(true); setMessage(''); setRewriteProgress(null);
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
  const markPublished=async()=>{
    if(!selected||busy)return;setBusy(true);setMessage('');
    try{
      const at=new Date(`${publishedWhen}:00+07:00`);if(!Number.isFinite(at.getTime()))throw new Error('Chọn thời điểm đã đăng hợp lệ.');
      const result=await api<Draft>(`${draft?'/drafts/'+draft.id:'/articles/'+selected.id}/mark-published`,{publishedUrl,publishedAt:at.toISOString(),confirmed:publicationConfirmed,channelId:!draft?channelId:undefined,expectedVersion:draft?(draft.updatedAt??draft.createdAt):undefined});
      setDraft(result);await load();setMessage('Đã ghi nhận bài đăng thủ công và cập nhật báo cáo. Hệ thống không đăng hoặc kiểm chứng bài trên Facebook.');
    }catch(e){setMessage((e as Error).message);}finally{setBusy(false);}
  };
  const submitShare=async()=>{
    if(!selected||busy)return;setBusy(true);setMessage('');
    try{const result=await api<Draft>(`/articles/${selected.id}/share`,{channelId,publishAccountId:shareAccount,confirmed:shareConfirmed});setDraft(result);await load();setMessage('Đã xếp hàng chia sẻ bài gốc. Theo dõi trạng thái trong Bản nháp & lịch đăng.');}
    catch(e){setMessage((e as Error).message);}finally{setBusy(false);}
  };
  const cancelShare=async(item:Draft)=>{setBusy(true);try{await api(`/share-jobs/${item.id}/cancel`,{});await load();if(draft?.id===item.id)setDraft({...item,status:'share_cancelled'});}catch(e){setMessage((e as Error).message);}finally{setBusy(false);}};
  const canDismissAi = clefRunning || bonsaiWaiting;
  const closeDialog = () => { setSelected(null); setDraft(null); };
  const deleteArticleRef=useDialogKeyboard(Boolean(deletingArticle),()=>setDeletingArticle(null),busy);
  const dialogRef = useDialogKeyboard(Boolean(selected), closeDialog, busy && !canDismissAi);
  const editable = draft?.status === 'awaiting_review';
  const wordCount = draft?.content.trim().split(/\s+/).filter(Boolean).length ?? 0;

  const publicationPanel=<section ref={publicationRef} className="manual-publication-panel"><h4>Đánh dấu đã đăng</h4><p className="cyber-hint">Chỉ dùng khi bạn đã đăng thủ công. Ghi nhận nội dung đã lưu, không gửi bài lên Facebook.</p><label className="setting-field">Link bài đã đăng (không bắt buộc)<input className="cyber-input-field" type="text" maxLength={2048} value={publishedUrl} onChange={e=>setPublishedUrl(e.target.value)} placeholder="https://www.facebook.com/groups/.../posts/..."/></label><label className="setting-field">Thời điểm đã đăng (giờ Việt Nam)<input className="cyber-input-field" type="datetime-local" value={publishedWhen} onChange={e=>setPublishedWhen(e.target.value)}/></label><label className="manual-publication-confirm"><input type="checkbox" checked={publicationConfirmed} onChange={e=>setPublicationConfirmed(e.target.checked)}/> Tôi xác nhận đã đăng bài này thủ công.</label>{editable&&drafts.find(d=>d.id===draft?.id)?.content!==draft?.content&&<p className="policy-warning">Lưu nội dung vừa chỉnh sửa trước khi đánh dấu.</p>}<button className="btn-preset" disabled={busy||(!draft&&!channelId)||!publicationConfirmed||!publishedWhen||(editable&&drafts.find(d=>d.id===draft?.id)?.content!==draft?.content)} onClick={()=>void markPublished()}>Đánh dấu đã đăng</button></section>;

  return <div className="view-container article-library">
    <div className="view-header-strip article-library-header"><div><h2 className="view-title">Bài viết & duyệt đăng<HelpTooltip text="Lọc theo thương hiệu → chọn kênh → Repost/Rewrite → duyệt và đặt giờ dự kiến" /></h2></div><button className="btn-launch-cyber" onClick={onNavigateToStudio}><Plus size={15} /> Cào bài mới</button></div>
    <nav className="article-library-tabs" aria-label="Danh mục bài viết"><button type="button" aria-pressed={view === 'articles'} className={`article-library-tab ${view === 'articles' ? 'is-active' : ''}`} onClick={() => setView('articles')}><Files size={18}/><span>Bài đã thu thập</span><span className="article-tab-count">{articles.length}</span></button><button type="button" aria-pressed={view === 'drafts'} className={`article-library-tab ${view === 'drafts' ? 'is-active' : ''}`} onClick={() => setView('drafts')}><CalendarClock size={18}/><span>Bản nháp & lịch đăng</span><span className="article-tab-count">{drafts.length}</span></button></nav>
    <section className="cyber-panel rewrite-job-panel" aria-label="Hàng đợi rewrite"><div className="view-header-strip"><h3>Hàng đợi rewrite <HelpTooltip text="Chờ lần lượt để dùng chung AI với Clef. Đóng trang không hủy yêu cầu. Chỉ báo hoàn tất sau khi lưu bản nháp; không tự đăng Facebook."/></h3><button className="btn-preset" onClick={()=>void refreshRewriteJobs()}>Làm mới</button></div>
      {rewriteQueueError&&<p role="alert" className="config-notice">{rewriteQueueError}</p>}
      {!rewriteJobs.length?<p className="cyber-hint">Chưa có yêu cầu. Chọn bài và kênh đích rồi bấm Rewrite để thêm.</p>:<div className="rewrite-job-list">{[...rewriteJobs].sort((a,b)=>a.createdAt.localeCompare(b.createdAt)).map(job=><div className="rewrite-job-row" key={job.id}><div><strong>{articles.find(a=>a.id===job.articleId)?.title||articles.find(a=>a.id===job.articleId)?.content.slice(0,80)||'Bài nguồn'}</strong><small>{brandChannels(brands.find(b=>b.id===articles.find(a=>a.id===job.articleId)?.brandId)).find(c=>c.id===job.channelId)?.name||job.channelId}</small></div><span role="status">{job.state==='queued'?`Đang chờ · vị trí ${job.queuePosition??'—'}`:job.state==='running'?'Đang xử lý · chờ AI/viết lại':job.state==='completed'?'Đã tạo bản nháp':job.state==='cancelled'?'Đã hủy':job.failure?.error||'Lỗi viết lại'}</span>{job.state==='queued'&&<button className="btn-preset" disabled={busy} onClick={async()=>{try{await api(`/rewrite-jobs/${job.id}`,undefined,'DELETE');await refreshRewriteJobs();}catch(e){setMessage((e as Error).message);}}}>Hủy chờ</button>}{job.state==='completed'&&job.result&&<button className="btn-preset" disabled={busy} onClick={()=>{openDraft(drafts.find(d=>d.id===job.result!.id)??job.result!);void load();}}>Mở bản nháp</button>}</div>)}</div>}
    </section>
    {view === 'articles' && <div className="brand-summary-grid article-filter-grid">{tabs.map(([id, label]) => <button key={id} className={`cyber-panel article-count ${filter === id ? 'selected' : ''}`} onClick={() => setFilter(id)}><span>{label}</span><strong>{loading ? '—' : scoped.filter(a => id === 'all' || status(a) === id).length}</strong></button>)}</div>}
    <div className="workspace-toolbar"><select aria-label="Lọc thương hiệu" className="cyber-select-field" value={brandId} onChange={e => { setBrandId(e.target.value); setDraftChannel(''); }}><option value="">Tất cả thương hiệu</option>{brands.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}</select><input aria-label="Tìm bài viết" className="cyber-input-field article-search" placeholder="Tìm nội dung, chủ đề…" value={query} onChange={e => setQuery(e.target.value)} /><button className="btn-preset" disabled={busy} onClick={() => void load().catch(e => setMessage(e.message))}><RefreshCw size={15} /> Làm mới</button></div>
    {view === 'drafts' && <div className="draft-filter-toolbar"><label className="setting-field">Trạng thái<select aria-label="Lọc trạng thái bản nháp" className="cyber-select-field" value={draftStatus} onChange={e => setDraftStatus(e.target.value)}><option value="all">Tất cả trạng thái</option>{draftStates.map(([value, label]) => <option key={value} value={value}>{label} ({scopedDrafts.filter(d => d.status === value).length})</option>)}</select></label><label className="setting-field">Kênh đích<select aria-label="Lọc kênh bản nháp" className="cyber-select-field" value={draftChannel} onChange={e => setDraftChannel(e.target.value)}><option value="">Tất cả kênh</option>{draftChannelOptions.map(([value, channel]) => <option key={value} value={value}>{channel.name} · {channel.brand}</option>)}</select></label><p className="draft-result-count" role="status">{loading ? 'Đang tải…' : `${visibleDrafts.length}/${scopedDrafts.length} bản nháp`}</p></div>}
    {!selected && <><ClefProgress evaluation={clefEvaluation} /><BonsaiProgress progress={rewriteProgress} withImages={rewriteWithImages} seconds={aiWaitSeconds} /></>}
    {message && <p role="status" className="config-notice">{message}</p>}
    <div className="article-table-wrap">{view === 'articles' ? <table className="article-table"><thead><tr><th>Bài viết / media</th><th>Thương hiệu / chủ đề</th><th>Thu thập / tương tác</th><th>Clef đánh giá</th><th>Thao tác</th></tr></thead><tbody>{pagedArticles.map(article => <tr key={article.id}>
      <td data-label="Bài viết / media"><button className="article-title" disabled={busy} onClick={() => openArticle(article)}>{article.title || article.content.slice(0, 90)}</button><a href={safeSource(article.sourceUrl)} target="_blank" rel="noreferrer">Xem bài gốc</a><small>{article.media?.filter(m => m.type === 'image').length ?? 0} ảnh · {article.media?.filter(m => m.type === 'video').length ?? 0} video</small>{drafts.some(d=>d.articleId===article.id&&d.status==='published')&&<small className="draft-status-tag" data-status="published">Đã có bản đăng</small>}</td>
      <td data-label="Thương hiệu / chủ đề">{brands.find(b => b.id === article.brandId)?.name ?? 'Thương hiệu đã ngưng'}<small>{article.aiTopic || 'Chưa phân loại chủ đề'}</small>{article.sourceGroupName&&<small>Nguồn nhóm: {article.sourceGroupName}</small>}{article.aiChannelMatches?.filter(c=>c.status==='recommended').map(c=><small key={c.channelId}>Phù hợp: {c.name} · {c.score}/100</small>)}</td><td data-label="Thu thập / tương tác"><ArticleMetadata article={article} /></td>
      <td data-label="Clef đánh giá">{clefRunning && clefEvaluation?.articleId === article.id && <div className="clef-row-progress" role="status"><strong>Clef đang đánh giá…</strong><progress aria-label="Tiến độ Clef của bài đang đánh giá (ước tính)" max={100} value={clefEvaluation.percent} /><small>{clefEvaluation.percent}% · ước tính · đã chờ {clefEvaluation.seconds} giây</small></div>}{tabs.find(([id]) => id === status(article))?.[1] ?? status(article)}{article.aiScore !== undefined && <strong> · {article.aiScore}/100</strong>}<small>{article.aiReason || 'Chưa có đánh giá Clef.'}</small>{stale(article) && <small className="policy-warning">Hồ sơ thương hiệu đã đổi; cần chấm lại.</small>}{article.aiWarnings?.map((text, i) => <small className="policy-warning" key={i}>{text}</small>)}</td>
      <td data-label="Thao tác"><div className="article-icon-actions"><button disabled={busy} className="btn-preset article-action-icon" title="Repost / Rewrite" aria-label="Repost / Rewrite" onClick={() => openArticle(article)}><FilePenLine size={18} aria-hidden="true"/></button><button disabled={busy} className="btn-preset article-action-icon danger-button" title="Xóa nội dung" aria-label="Xóa nội dung" onClick={()=>setDeletingArticle(article)}><Trash2 size={18} aria-hidden="true"/></button><button disabled={busy} className="btn-preset article-action-icon" title="Đánh dấu đã đăng" aria-label="Đánh dấu đã đăng" onClick={()=>{openArticle(article);setSourcePublication(true);setJumpToPublish(true);}}><BadgeCheck size={18} aria-hidden="true"/></button><button disabled={busy} className="btn-preset article-action-icon" title={clefRunning && clefEvaluation?.articleId===article.id?"Clef đang đánh giá…":"Clef đánh giá"} aria-label={clefRunning && clefEvaluation?.articleId===article.id?"Clef đang đánh giá…":"Clef đánh giá"} onClick={() => void evaluate(article)}>{clefRunning && clefEvaluation?.articleId===article.id?<LoaderCircle size={18} className="article-action-spinning" aria-hidden="true"/>:<Sparkles size={18} aria-hidden="true"/>}</button></div></td>
    </tr>)}</tbody></table> : <table className="article-table"><thead><tr><th>Bản nháp</th><th>Thương hiệu / kênh đích</th><th>Thời gian đăng (VN)</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>{visibleDrafts.map(item => <tr key={item.id}><td data-label="Bản nháp">{item.content.slice(0, 100)}<small>{item.branch==='share'?'Chia sẻ bài gốc':item.branch === 'repost' ? 'Repost' : 'Bonsai viết lại'}</small></td><td data-label="Thương hiệu / kênh đích">{brands.find(b => b.id === item.brandId)?.name}<small>{item.channelSnapshot?.name ?? 'Kênh cũ'}</small></td><td data-label="Thời gian đăng (VN)">{displayDate(item.status==='published'?item.publishedAt:item.plannedAt)}<small>{item.status==='published'?(item.branch==='share'?'Đã chia sẻ':'Đã đăng thủ công'):'Dự kiến'}</small></td><td data-label="Trạng thái"><span className="draft-status-tag" data-status={item.status}>{draftStates.find(([value]) => value === item.status)?.[1] ?? item.status}</span></td><td data-label="Thao tác"><div className="preset-buttons-row"><button className="btn-preset" disabled={busy} onClick={() => openDraft(item)}>{item.branch==='share'?'Xem chia sẻ':'Mở bản nháp'}</button>{item.status==='share_queued'&&<button className="btn-preset" disabled={busy} onClick={()=>void cancelShare(item)}>Hủy chia sẻ</button>}{item.branch!=='share'&&item.status!=='published'&&<button className="btn-preset" disabled={busy} onClick={()=>{openDraft(item);setJumpToPublish(true);}}>Đánh dấu đã đăng</button>}</div></td></tr>)}</tbody></table>}
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
    {deletingArticle&&<div className="workspace-modal-overlay"><section ref={deleteArticleRef} role="dialog" aria-modal="true" aria-label="Xóa nội dung" className="cyber-panel workspace-delete"><h3>Xóa nội dung này?</h3><p>{(deletingArticle.title||deletingArticle.content).slice(0,180)}</p><p>Bài nguồn sẽ chuyển vào Thùng rác → Bài viết. Bản nháp và lịch sử đăng đã có được giữ nguyên. Không xóa bài trên Facebook.</p><div className="brand-card-actions"><button className="btn-preset" disabled={busy} onClick={()=>setDeletingArticle(null)}>Hủy</button><button className="btn-preset danger-button" disabled={busy} onClick={async()=>{setBusy(true);try{await api(`/records/articles/${deletingArticle.id}`,undefined,'DELETE');setArticles(items=>items.filter(a=>a.id!==deletingArticle.id));setDeletingArticle(null);setMessage('Đã chuyển nội dung vào thùng rác.');}catch(e){setMessage((e as Error).message);}finally{setBusy(false);}}}>{busy?'Đang xóa…':'Xóa nội dung'}</button></div></section></div>}
    {selected && <div className="workspace-modal-overlay"><section ref={dialogRef} role="dialog" aria-modal="true" aria-label="Xem trước bài viết" className="cyber-panel workspace-editor article-dialog"><LiveEditNotice /><header><div><h3>{draft ? draft.branch==='share'?'Chia sẻ bài gốc':draft.status==='published'?'Bài đã đăng · ghi nhận thủ công':draft.branch === 'repost' ? 'Duyệt bản repost' : 'Duyệt bản Bonsai viết lại' :sourceShare?'Chia sẻ bài gốc vào nhóm':sourcePublication?'Đánh dấu bài đã đăng':'Chuẩn bị nội dung'}</h3><p>{selectedBrand?.name}</p></div><button className="workspace-icon-button" disabled={busy && !canDismissAi} aria-label="Đóng" onClick={closeDialog}><X size={20} /></button></header>
      {(clefEvaluation || rewriteProgress !== null) && <div className="article-dialog-progress"><ClefProgress evaluation={clefEvaluation} /><BonsaiProgress progress={rewriteProgress} withImages={rewriteWithImages} seconds={aiWaitSeconds} /></div>}
      <div className="article-preview">
        {draft ? <div className="config-notice"><strong>Kênh đích: {selectedChannel?.name ?? 'Kênh đã ngưng'}</strong><p>{selectedChannel?.type === 'page' ? 'Page' : 'Group'} · {selectedChannel?.minWords}–{selectedChannel?.maxWords} từ · {selectedChannel?.url}</p><p>{selectedChannel?.imageRequirements}</p></div> : <label className="setting-field">Kênh đăng đích<select className="cyber-select-field" value={channelId} onChange={e => setChannelId(e.target.value)}><option value="">Chọn kênh trong hồ sơ thương hiệu</option>{channels.map(channel => <option key={channel.id} value={channel.id}>{channel.name} · {channel.type === 'group' ? 'Group' : 'Page'} · {channel.minWords}–{channel.maxWords} từ</option>)}</select></label>}
        {busy && !clefRunning && !bonsaiWaiting && <p role="status">Đang lưu dữ liệu…</p>}
        {draft?.branch === 'rewrite' && <p className="config-notice">{draft.rewriteInputMode === 'caption-with-image-context' ? (draft.rewriteImagesProvided ?? 0) > 0 ? `Đã gửi ${draft.rewriteImagesProvided} ảnh để hiểu ngữ cảnh và viết lại phần chữ. Giữ nguyên media gốc; cần duyệt lại nội dung.` : 'Đã viết lại phần chữ bài gốc.' : draft.rewriteVisualConfirmedAt ? `Bản nháp cũ viết từ ghi chú đã đối chiếu ${draft.rewriteImagesProvided} ảnh.` : (draft.rewriteImagesProvided ?? 0) > 0 ? 'Bản nháp đã gửi ảnh cho AI; cần duyệt lại nội dung.' : 'Bản nháp viết lại từ phần chữ; chưa gửi ảnh cho AI.'}</p>}

        {!draft && <ArticleMetadata article={selected} />}
        {draft&&draft.branch!=='share' ? <label className="setting-field">Nội dung đăng · {wordCount} từ<textarea rows={12} className="cyber-textarea-field" disabled={!editable || busy} value={draft.content} onChange={e => setDraft({ ...draft, content: e.target.value, review: {} })} /><small>Thay nội dung sẽ bỏ đánh dấu checklist để kiểm tra lại.</small></label> : <p className="article-content">{selected.content}</p>}
        <a href={safeSource(draft?.sourceUrl ?? selected.sourceUrl)} target="_blank" rel="noreferrer">Đối chiếu bài gốc</a>
        <div className="article-media-grid">{(draft?.media ?? selected.media ?? []).map((media, i) => <div key={i}><SourceMedia key={media.url} media={media} index={i}/></div>)}</div>
        {draft?.rewriteVisualContext && <details className="config-notice"><summary>Ghi chú ảnh đã xác nhận dùng cho bản nháp</summary><p className="article-content">{draft.rewriteVisualContext}</p></details>}
        {!sourceShare&&draft?.branch!=='share'&&!(draft?.media ?? selected.media ?? []).length && <p className="config-notice">Chưa có ảnh/video gốc trên máy này.{draft?.status!=='published'&&' Chưa thể duyệt tự đăng.'}</p>}
        {draft?.branch==='share'?<section className="manual-publication-panel"><h4>{draftStates.find(([key])=>key===draft.status)?.[1]??draft.status}</h4><p>{draft.shareMessage??'Chia sẻ nguyên bài Facebook, giữ tác giả gốc. Không tải lại hoặc viết lại nội dung.'}</p>{draft.sourceAuthor&&<p>Người đăng gốc: {draft.sourceAuthor}</p>}{safeSource(draft.publishedUrl)&&<a href={safeSource(draft.publishedUrl)} target="_blank" rel="noreferrer">Mở bài chia sẻ</a>}{draft.status==='share_queued'&&<button className="btn-preset" disabled={busy} onClick={()=>void cancelShare(draft)}>Hủy lượt đang chờ</button>}</section>:draft ? <>
          <div className="policy-checklist"><h4>Checklist trước khi duyệt</h4><p className="cyber-hint">Đây là xác nhận của người duyệt. Tải được media không đồng nghĩa có quyền sử dụng.</p>{reviewFields(selectedBrand, selectedChannel).map(([key, label]) => <label key={key}><input type="checkbox" disabled={!editable || busy} checked={draft.review?.[key] === true} onChange={e => setDraft({ ...draft, review: { ...draft.review, [key]: e.target.checked } })} />{label}</label>)}</div>
          <label className="setting-field">Tài khoản đăng<select className="cyber-select-field" disabled={!editable || busy} value={draft.publishAccountId ?? ''} onChange={e => setDraft({ ...draft, publishAccountId: e.target.value })}><option value="">Chọn tài khoản đăng đã xác minh</option>{accounts.filter(a => a.role === 'publish' && a.enabled !== false).map(account => <option key={account.id} value={account.id} disabled={account.facebookStatus !== 'active'}>{account.name}{account.facebookStatus !== 'active' ? ' · chưa xác minh hoạt động' : ''}</option>)}</select></label>
          <label className="setting-field">Giờ đăng dự kiến (Việt Nam, tùy chọn)<input className="cyber-input-field" type="datetime-local" disabled={!editable || busy} value={localPlan(draft.plannedAt)} onChange={e => setDraft({ ...draft, plannedAt: e.target.value ? (Number.isFinite(Date.parse(`${e.target.value}:00+07:00`)) ? new Date(`${e.target.value}:00+07:00`).toISOString() : undefined) : undefined })} /></label>
          <div className="preset-buttons-row"><button className="btn-preset" disabled={busy || !editable} onClick={() => void saveDraft(false)}>Lưu bản nháp</button><button className="btn-launch-cyber" disabled={busy || !editable || !draft.publishAccountId || reviewFields(selectedBrand, selectedChannel).some(([key]) => !draft.review?.[key])} onClick={() => void saveDraft(true)}>{draft.status==='published'?'Đã đăng':editable ? 'Duyệt · chờ đăng' : 'Đã duyệt · chờ worker'}</button></div>
          {draft.status==='published'?<p className="config-notice"><strong>Đã đăng thủ công · {displayDate(draft.publishedAt)}</strong><br/>{safeSource(draft.publishedUrl)?<a href={safeSource(draft.publishedUrl)} target="_blank" rel="noreferrer">Mở bài đã đăng</a>:<span>Không lưu link bài đăng.</span>}<br/>Do người dùng ghi nhận; chưa kiểm chứng trực tiếp với Facebook.</p>:publicationPanel}
        </> : sourceShare?null:<div className="preset-buttons-row"><button className="btn-preset" disabled={busy || !channelId || Boolean(selected.deletedAt)} onClick={() => void prepare('repost')}>Repost nội dung gốc</button><button className="btn-launch-cyber" disabled={busy || !channelId || Boolean(selected.deletedAt) || rewriteJobs.some(j=>j.articleId===selected.id&&j.channelId===channelId&&['queued','running'].includes(j.state))} onClick={() => void prepare('rewrite')}>Rewrite</button><button className="btn-preset" disabled={busy} onClick={() => void evaluate(selected)}>{clefRunning && clefEvaluation?.articleId === selected.id ? 'Clef đang đánh giá…' : 'Clef đánh giá'}</button></div>}
        {!draft&&sourceShare&&<section className="manual-publication-panel"><h4>Chia sẻ bài gốc vào Group</h4><p className="cyber-hint">Dùng chức năng Chia sẻ của Facebook để giữ người đăng và bài gốc. Chỉ chạy sau khi bạn xác nhận bên dưới.</p>{selectedChannel?.type!=='group'&&<p className="policy-warning">Chọn một kênh Group.</p>}<label className="setting-field">Tài khoản chia sẻ<select className="cyber-select-field" value={shareAccount} onChange={e=>{setShareAccount(e.target.value);setShareConfirmed(false);}}><option value="">Chọn tài khoản đăng</option>{accounts.filter(a=>a.role==='publish'&&a.enabled!==false&&a.facebookStatus==='active').map(a=><option key={a.id} value={a.id}>{a.name}</option>)}</select></label><label className="manual-publication-confirm"><input type="checkbox" checked={shareConfirmed} onChange={e=>setShareConfirmed(e.target.checked)}/> Tôi xác nhận chia sẻ thật bài gốc vào {selectedChannel?.name??'nhóm đã chọn'}.</label><button className="btn-launch-cyber" disabled={busy||!shareConfirmed||!shareAccount||selectedChannel?.type!=='group'} onClick={()=>void submitShare()}>Xếp hàng chia sẻ thật</button></section>}
        {!draft&&sourcePublication&&publicationPanel}
        {message && <p role="status" className="config-notice">{message}</p>}
        <p className="cyber-hint">Lưu giờ và duyệt bản repost/rewrite chưa tự đăng. Chia sẻ bài gốc chỉ chạy khi bấm xác nhận riêng.</p>
      </div>
    </section></div>}
  </div>;
}
