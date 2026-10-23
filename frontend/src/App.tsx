import { FacebookAccountsView } from './components/views/FacebookAccountsView';
import { useEffect, useState } from 'react';
import { AppProvider } from './context/AppContext';
import { Navbar } from './components/Navbar';
import { DashboardView } from './components/views/DashboardView';
import { CrawlerStudioView } from './components/views/CrawlerStudioView';
import { TaskQueueView } from './components/views/TaskQueueView';
import { DataVaultView } from './components/views/DataVaultView';
import { WorkspaceView } from './components/views/WorkspaceView';

import { LoginGate } from './components/LoginGate';
import { api } from './api';
import type { NavigationTab, CrawlTask, EngineTelemetry, TargetType } from './types';
import './App.css';
function MainAppShell() {
  const [articleBrandId,setArticleBrandId] = useState('');
  const [currentTab, setCurrentTab] = useState<NavigationTab>('dashboard');

  const [tasks, setTasks] = useState<CrawlTask[]>([]);
  const [logs, setLogs] = useState<string[]>([]);
  const [connection, setConnection] = useState('Đang kết nối backend…');
  const [telemetry, setTelemetry] = useState<EngineTelemetry>({ activeTargets: 0, postsCaptured: 0, queueDepth: 0, systemHealth: 0, apiLatencyMs: 0, proxyPoolReady: 0, workerNodesOnline: 0 });
  const refresh = async () => {
    const start = performance.now();
    const [items, messages] = await Promise.all([api<CrawlTask[]>('/records/tasks'), api<string[]>('/logs')]);
    setTasks(items); setLogs(messages.reverse()); setConnection('Đã kết nối PostgreSQL · Worker Facebook chưa triển khai');
    setTelemetry({ activeTargets: items.filter(t => t.status === 'running').length, postsCaptured: items.reduce((n,t) => n + t.crawledPosts,0), queueDepth: items.filter(t => t.status === 'pending').length, systemHealth: 100, apiLatencyMs: Math.round(performance.now()-start), proxyPoolReady: 0, workerNodesOnline: 0 });
  };
  useEffect(() => { const update = () => { refresh().catch(e => { setConnection(e.message); setTelemetry(t => ({...t, systemHealth: 0, workerNodesOnline: 0})); }); }; update(); const timer = setInterval(update, 10000); return () => clearInterval(timer); }, []);
  const handleLaunchTarget = (url: string, _targetType: TargetType, maxPosts: number) => {
    let previous = {}; try {previous=JSON.parse(localStorage.getItem('sammicrawl_crawl')??'{}');}catch { /* Start with an empty draft. */ }
    localStorage.setItem('sammicrawl_crawl',JSON.stringify({...previous,url,maxPosts,targetType:'page'}));setCurrentTab('crawler-studio');
  };
  const handleDeleteTask = (id: string) => { void api(`/records/tasks/${id}`, undefined, 'DELETE').then(refresh).catch(e => setConnection(e.message)); };
  return <div className="cyber-app-shell"><Navbar currentTab={currentTab} onSelectTab={setCurrentTab} telemetry={telemetry} onOpenMissionModal={() => setCurrentTab('crawler-studio')} />
  <div role="status" style={{ padding: '8px 24px', color: 'var(--text-secondary)', fontSize: 13 }}>{connection} <span style={{ marginLeft: 12 }}>Chế độ local</span></div>
  <main className="cyber-main-canvas">
  {currentTab === 'dashboard' && <DashboardView telemetry={telemetry} logs={logs} onLaunchTarget={handleLaunchTarget} onNavigateToStudio={() => setCurrentTab('crawler-studio')} />}
  {currentTab === 'crawler-studio' && <CrawlerStudioView onManageAccounts={() => setCurrentTab('facebook-accounts')} onViewArticles={brandId => {setArticleBrandId(brandId);setCurrentTab('data-vault');}} />}
  {currentTab === 'queue' && <TaskQueueView tasks={tasks} onDeleteTask={handleDeleteTask} onNavigateToStudio={() => setCurrentTab('crawler-studio')} />}
  {currentTab === 'data-vault' && <DataVaultView initialBrandId={articleBrandId} onNavigateToStudio={() => setCurrentTab('crawler-studio')} />}
  {currentTab === 'facebook-accounts' && <FacebookAccountsView />}
  {currentTab === 'proxy-stealth' && <WorkspaceView />}
  </main></div>;
}
export function App() { return <AppProvider><LoginGate><MainAppShell /></LoginGate></AppProvider>; }
export default App;
