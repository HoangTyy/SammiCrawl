export type TargetType = 'page' | 'group' | 'profile' | 'reel' | 'post';
export type TaskStatus = 'pending' | 'running' | 'completed' | 'failed' | 'paused';
export type NavigationTab = 'dashboard' | 'crawler-studio' | 'queue' | 'data-vault' | 'proxy-stealth' | 'facebook-accounts' | 'channels' | 'crawl-presets' | 'trash';

export interface CrawlTask {
  id: string;
  url: string;
  targetName: string;
  targetType: TargetType;
  status: TaskStatus;
  maxPosts: number;
  crawledPosts: number;
  errorReason?: string;
  errorCode?: string;
  duplicatesSkipped?: number;
  scrollDelay?: number;
  maxScrolls?: number;
  delayMinMs?: number;
  delayMaxMs?: number;
  minLikes?: number;
  minComments?: number;
  keywords?: string;
  topicDescription?: string;
  crawlAccountIds?: string[];
  publishAccountId?: string;
  brandId?: string;
  branch?: string;
  minAiScore?: number;
  topPosts?: number;
  rewriteInstructions?: string;
  rewriteTemperature?: number;
  requireApproval?: boolean;
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
