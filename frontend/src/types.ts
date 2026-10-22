export type TargetType = 'page' | 'group' | 'profile' | 'reel' | 'post';
export type TaskStatus = 'pending' | 'running' | 'completed' | 'failed' | 'paused';
export type NavigationTab = 'dashboard' | 'crawler-studio' | 'queue' | 'data-vault' | 'ai-rewriter' | 'proxy-stealth';

export interface CrawlTask {
  id: string;
  url: string;
  targetName: string;
  targetType: TargetType;
  status: TaskStatus;
  maxPosts: number;
  crawledPosts: number;
  includeComments: boolean;
  extractMedia: boolean;
  createdAt: string;
  progress: number;
  workerNode: string;
  ipAddress: string;
  latencyMs: number;
}

export interface EngineTelemetry {
  activeTargets: number;
  postsCaptured: number;
  queueDepth: number;
  systemHealth: number; // percentage, e.g. 100
  apiLatencyMs: number;
  proxyPoolReady: number;
  workerNodesOnline: number;
}

export interface EngineStats {
  totalTargets: number;
  totalPostsCrawled: number;
  pendingQueue: number;
  runningCrawlers: number;
  proxyPoolCount: number;
  engineStatus: 'idle' | 'running' | 'paused';
}
