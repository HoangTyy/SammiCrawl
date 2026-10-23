import { useState } from 'react';
import { Building2, Bookmark } from 'lucide-react';
import { WorkspaceView } from './WorkspaceView';
export function TrashView() {
 const [section,setSection]=useState<'brands'|'presets'>('brands');
 return <div className="trash-page"><nav className="workspace-navigation trash-categories" aria-label="Danh mục thùng rác"><div className="workspace-nav-group">{([{id:'brands',label:'Thương hiệu',icon:Building2},{id:'presets',label:'Mẫu cấu hình cào bài',icon:Bookmark}] as const).map(({id,label,icon:Icon})=><button key={id} type="button" className={`workspace-nav-button ${section===id?'is-active':''}`} aria-pressed={section===id} onClick={()=>setSection(id)}><Icon size={17}/><span>{label}</span></button>)}</div></nav><WorkspaceView key={section} section={section} trash /></div>;
}
