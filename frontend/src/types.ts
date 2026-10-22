export type TargetType = 'page' | 'group' | 'profile';
export type TaskStatus = 'pending' | 'running' | 'completed' | 'failed';

export interface CrawlTask {
  id: string;
  url: string;
  targetName: string;
  targetType: TargetType;
  status: TaskStatus;
  maxPosts: number;
  crawledPosts: number;
  includeComments: boolean;
  createdAt: string;
  progress: number; // 0 - 100
  errorMessage?: string;
}

export interface EngineStats {
  totalTargets: number;
  totalPostsCrawled: number;
  pendingQueue: number;
  runningCrawlers: number;
  proxyPoolCount: number;
  engineStatus: 'idle' | 'running' | 'paused';
}
