import { useEffect, useState } from 'react';
import { Building2, Bookmark, Users } from 'lucide-react';
import { WorkspaceView } from './WorkspaceView';
import { api } from '../../api';
export function TrashView() {
 const [section,setSection]=useState<'brands'|'presets'|'accounts'|'articles'>('brands');
 return <div className="trash-page"><nav className="workspace-navigation trash-categories" aria-label="Danh mục thùng rác"><div className="workspace-nav-group">{([{id:'brands',label:'Thương hiệu',icon:Building2},{id:'presets',label:'Mẫu cấu hình cào bài',icon:Bookmark},{id:'accounts',label:'Tài khoản Facebook',icon:Users},{id:'articles',label:'Bài viết',icon:Bookmark}] as const).map(({id,label,icon:Icon})=><button key={id} type="button" className={`workspace-nav-button ${section===id?'is-active':''}`} aria-pressed={section===id} onClick={()=>setSection(id)}><Icon size={17}/><span>{label}</span></button>)}</div></nav>{section==='articles'?<ArticleTrash/>:<WorkspaceView key={section} section={section} trash />}</div>;
}

function ArticleTrash(){
 const [items,setItems]=useState<{id:string;title?:string;content?:string}[]>([]);const [error,setError]=useState('');const [busy,setBusy]=useState(false);
 const load=()=>api<typeof items>('/records/articles?trash=true').then(setItems).catch(e=>setError(e.message));
 useEffect(()=>{void load();},[]);
 return <section className="cyber-panel"><h2>Bài viết đã xóa</h2>{error&&<p role="alert">{error}</p>}{!items.length&&<p>Không có bài viết trong thùng rác.</p>}{items.map(item=><article className="brand-profile-card" key={item.id}><p>{(item.title||item.content||'Bài viết').slice(0,220)}</p><button className="btn-preset" disabled={busy} onClick={async()=>{setBusy(true);try{await api(`/records/articles/${item.id}/restore`,{});await load();}catch(e){setError((e as Error).message);}finally{setBusy(false);}}}>Khôi phục</button></article>)}</section>;
}
