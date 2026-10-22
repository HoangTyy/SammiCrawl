import { useState } from 'react';
import { Sidebar } from './components/Sidebar';
import { Header } from './components/Header';
import { MetricsOverview } from './components/MetricsOverview';
import { CrawlQuickBar } from './components/CrawlQuickBar';
import { TaskQueueSection } from './components/TaskQueueSection';
import { CrawlerEnginePanel } from './components/CrawlerEnginePanel';
import { NewTaskModal } from './components/NewTaskModal';
import type { CrawlTask, EngineStats, TargetType } from './types';
import './App.css';

export function App() {
  const [currentTab, setCurrentTab] = useState<string>('tasks-queue');
  const [isModalOpen, setIsModalOpen] = useState<boolean>(false);

  // Initial zero-state (NO sample data, as specifically requested by user)
  const [tasks, setTasks] = useState<CrawlTask[]>([]);
  
  const [stats, setStats] = useState<EngineStats>({
    totalTargets: 0,
    totalPostsCrawled: 0,
    pendingQueue: 0,
    runningCrawlers: 0,
    proxyPoolCount: 0,
    engineStatus: 'idle'
  });

  const [logs, setLogs] = useState<string[]>([
    '[15:35:00] SammiCrawl Engine v1.0.0 initialized.',
    '[15:35:01] ASP.NET Core 10 Web API Bridge connected: http://localhost:7142',
    '[15:35:02] Anti-detection fingerprint initialized (Chromium Stealth).',
    '[15:35:03] Task queue listener active. Ready for Facebook URLs.'
  ]);

  const addLog = (message: string) => {
    const timestamp = new Date().toLocaleTimeString();
    setLogs(prev => [...prev.slice(-49), `[${timestamp}] ${message}`]);
  };

  const handleQuickAdd = (
    url: string, 
    targetType: TargetType, 
    maxPosts: number, 
    includeComments: boolean
  ) => {
    const targetName = url.replace(/https?:\/\/(www\.)?facebook\.com\/?/, '').split('/')[0] || 'Mục tiêu mới';
    
    const newTask: CrawlTask = {
      id: `task_${Date.now()}`,
      url,
      targetName,
      targetType,
      status: 'running',
      maxPosts,
      crawledPosts: 0,
      includeComments,
      createdAt: 'Vừa xong',
      progress: 5
    };

    setTasks(prev => [newTask, ...prev]);
    setStats(prev => ({
      ...prev,
      totalTargets: prev.totalTargets + 1,
      pendingQueue: prev.pendingQueue + 1,
      runningCrawlers: Math.min(prev.runningCrawlers + 1, 4)
    }));

    addLog(`Đã tiếp nhận mục tiêu: ${targetName} (${url}). Khởi động Chromium Worker.`);
  };

  const handleModalAdd = (
    taskData: Omit<CrawlTask, 'id' | 'status' | 'crawledPosts' | 'createdAt' | 'progress'>
  ) => {
    const newTask: CrawlTask = {
      ...taskData,
      id: `task_${Date.now()}`,
      status: 'pending',
      crawledPosts: 0,
      createdAt: 'Vừa xong',
      progress: 0
    };

    setTasks(prev => [newTask, ...prev]);
    setStats(prev => ({
      ...prev,
      totalTargets: prev.totalTargets + 1,
      pendingQueue: prev.pendingQueue + 1
    }));

    addLog(`Tạo chiến dịch mới từ Modal: ${taskData.targetName}. Xếp hàng đợi.`);
  };

  const handleDeleteTask = (id: string) => {
    setTasks(prev => prev.filter(t => t.id !== id));
    addLog(`Đã xóa tác vụ ID: ${id}.`);
  };

  return (
    <div className="app-layout">
      {/* Sidebar matching user's design */}
      <Sidebar 
        currentTab={currentTab} 
        onTabChange={setCurrentTab} 
      />

      {/* Main Content Area */}
      <div className="main-content-wrapper">
        <Header 
          currentTab={currentTab} 
          onOpenNewTask={() => setIsModalOpen(true)} 
        />

        <main className="dashboard-main">
          {/* Quick Stats Metric Cards */}
          <MetricsOverview stats={stats} />

          {/* Instant Crawl Bar */}
          <CrawlQuickBar onQuickAdd={handleQuickAdd} />

          {/* Grid Layout: Tasks Queue (Main) & Crawler Engine Logs/Status (Side) */}
          <div className="dashboard-content-grid">
            <div className="main-grid-column">
              <TaskQueueSection 
                tasks={tasks}
                onOpenNewTask={() => setIsModalOpen(true)}
                onDeleteTask={handleDeleteTask}
              />
            </div>

            <div className="side-grid-column">
              <CrawlerEnginePanel logs={logs} />
            </div>
          </div>
        </main>
      </div>

      {/* Modal for detailed task setup */}
      <NewTaskModal 
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onAddTask={handleModalAdd}
      />
    </div>
  );
}

export default App;
