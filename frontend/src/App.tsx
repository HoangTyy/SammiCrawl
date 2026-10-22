import { useState } from 'react';
import { Navbar } from './components/Navbar';
import { DashboardView } from './components/views/DashboardView';
import { CrawlerStudioView } from './components/views/CrawlerStudioView';
import { TaskQueueView } from './components/views/TaskQueueView';
import { DataVaultView } from './components/views/DataVaultView';
import { AiRewriterView } from './components/views/AiRewriterView';
import { ProxySettingsView } from './components/views/ProxySettingsView';
import { MissionModal } from './components/MissionModal';
import type { NavigationTab, CrawlTask, EngineTelemetry, TargetType } from './types';
import './App.css';

export function App() {
  const [currentTab, setCurrentTab] = useState<NavigationTab>('dashboard');
  const [isModalOpen, setIsModalOpen] = useState(false);

  // Initial Zero-Data state (NO sample/mock records)
  const [tasks, setTasks] = useState<CrawlTask[]>([]);

  const [telemetry, setTelemetry] = useState<EngineTelemetry>({
    activeTargets: 0,
    postsCaptured: 0,
    queueDepth: 0,
    systemHealth: 100,
    apiLatencyMs: 14,
    proxyPoolReady: 0,
    workerNodesOnline: 4
  });

  const [logs, setLogs] = useState<string[]>([
    '[15:45:00] SammiCrawl Cyber Core v1.0 online.',
    '[15:45:01] ASP.NET Core 10 Web API listener ready at :7142.',
    '[15:45:02] Chromium stealth profile loaded (Fingerprint randomized).',
    '[15:45:03] Ready to ingest Facebook URLs. 0 jobs in queue.'
  ]);

  const addLog = (msg: string) => {
    const time = new Date().toLocaleTimeString();
    setLogs(prev => [...prev.slice(-49), `[${time}] ${msg}`]);
  };

  const handleLaunchTarget = (url: string, targetType: TargetType, maxPosts: number) => {
    const targetName = url.replace(/https?:\/\/(www\.)?facebook\.com\/?/, '').split('/')[0] || 'FB Target';
    const newTask: CrawlTask = {
      id: `task_${Date.now()}`,
      url,
      targetName,
      targetType,
      status: 'running',
      maxPosts,
      crawledPosts: 0,
      includeComments: true,
      extractMedia: true,
      createdAt: 'Vừa kích hoạt',
      progress: 10,
      workerNode: 'Worker-Alpha',
      ipAddress: '127.0.0.1 (Direct)',
      latencyMs: 18
    };

    setTasks(prev => [newTask, ...prev]);
    setTelemetry(prev => ({
      ...prev,
      activeTargets: prev.activeTargets + 1,
      queueDepth: prev.queueDepth + 1
    }));

    addLog(`Đã khởi chạy: ${targetName} (${url}). Điều phối tới Worker-Alpha.`);
  };

  const handleDeployFull = (
    taskData: Omit<CrawlTask, 'id' | 'status' | 'crawledPosts' | 'createdAt' | 'progress' | 'workerNode' | 'ipAddress' | 'latencyMs'>
  ) => {
    const newTask: CrawlTask = {
      ...taskData,
      id: `task_${Date.now()}`,
      status: 'pending',
      crawledPosts: 0,
      createdAt: 'Vừa lên lịch',
      progress: 0,
      workerNode: 'Worker-Beta',
      ipAddress: '127.0.0.1 (Direct)',
      latencyMs: 22
    };

    setTasks(prev => [newTask, ...prev]);
    setTelemetry(prev => ({
      ...prev,
      activeTargets: prev.activeTargets + 1,
      queueDepth: prev.queueDepth + 1
    }));

    addLog(`Lên lịch chiến dịch: ${taskData.targetName}. Xếp vào hàng đợi.`);
  };

  const handleDeleteTask = (id: string) => {
    setTasks(prev => prev.filter(t => t.id !== id));
    setTelemetry(prev => ({
      ...prev,
      activeTargets: Math.max(0, prev.activeTargets - 1),
      queueDepth: Math.max(0, prev.queueDepth - 1)
    }));
    addLog(`Hủy tác vụ ID: ${id}. Giải phóng tài nguyên.`);
  };

  return (
    <div className="cyber-app-shell">
      {/* Top Cyber Command Bar */}
      <Navbar
        currentTab={currentTab}
        onSelectTab={setCurrentTab}
        telemetry={telemetry}
        onOpenMissionModal={() => setIsModalOpen(true)}
      />

      {/* Dynamic View Content Area */}
      <main className="cyber-main-canvas">
        {currentTab === 'dashboard' && (
          <DashboardView
            telemetry={telemetry}
            logs={logs}
            onLaunchTarget={handleLaunchTarget}
            onNavigateToStudio={() => setCurrentTab('crawler-studio')}
          />
        )}

        {currentTab === 'crawler-studio' && (
          <CrawlerStudioView onDeployMission={handleDeployFull} />
        )}

        {currentTab === 'queue' && (
          <TaskQueueView
            tasks={tasks}
            onDeleteTask={handleDeleteTask}
            onNavigateToStudio={() => setCurrentTab('crawler-studio')}
          />
        )}

        {currentTab === 'data-vault' && (
          <DataVaultView onNavigateToStudio={() => setCurrentTab('crawler-studio')} />
        )}

        {currentTab === 'ai-rewriter' && (
          <AiRewriterView />
        )}

        {currentTab === 'proxy-stealth' && (
          <ProxySettingsView />
        )}
      </main>

      {/* Global Mission Modal */}
      <MissionModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onDeploy={handleDeployFull}
      />
    </div>
  );
}

export default App;
