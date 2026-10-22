import React, { useState } from 'react';
import { 
  Sliders, 
  ShieldCheck, 
  Database, 
  Play, 
  Layers, 
  Image, 
  MessageSquare,
  CheckCircle2,
  Key
} from 'lucide-react';
import type { TargetType, CrawlTask } from '../../types';
import { useApp } from '../../context/AppContext';

interface CrawlerStudioViewProps {
  onDeployMission: (task: Omit<CrawlTask, 'id' | 'status' | 'crawledPosts' | 'createdAt' | 'progress' | 'workerNode' | 'ipAddress' | 'latencyMs'>) => void;
}

export const CrawlerStudioView: React.FC<CrawlerStudioViewProps> = ({ onDeployMission }) => {
  const { t } = useApp();
  const [url, setUrl] = useState('');
  const [targetName, setTargetName] = useState('');
  const [targetType, setTargetType] = useState<TargetType>('page');
  const [maxPosts, setMaxPosts] = useState(30);
  const [includeComments, setIncludeComments] = useState(true);
  const [extractMedia, setExtractMedia] = useState(true);
  const [cookieData, setCookieData] = useState('');
  const [scrollDelay, setScrollDelay] = useState(4);
  const [successToast, setSuccessToast] = useState(false);

  const handleDeploy = (e: React.FormEvent) => {
    e.preventDefault();
    if (!url.trim()) return;

    const computedName = targetName.trim() || (
      url.replace(/https?:\/\/(www\.)?facebook\.com\/?/, '').split('/')[0] || 'FB Target'
    );

    onDeployMission({
      url,
      targetName: computedName,
      targetType,
      maxPosts,
      includeComments,
      extractMedia
    });

    setSuccessToast(true);
    setTimeout(() => setSuccessToast(false), 3000);
    setUrl('');
    setTargetName('');
    setCookieData('');
  };

  const applyPreset = (type: TargetType, posts: number, comments: boolean, media: boolean) => {
    setTargetType(type);
    setMaxPosts(posts);
    setIncludeComments(comments);
    setExtractMedia(media);
  };

  return (
    <div className="view-container studio-view">
      <div className="view-header-strip">
        <div>
          <h2 className="view-title">{t.studio.title}</h2>
          <p className="view-subtitle">{t.studio.subtitle}</p>
        </div>

        {/* Preset Buttons */}
        <div className="preset-buttons-row">
          <span className="preset-label">{t.studio.presets}</span>
          <button 
            type="button" 
            className="btn-preset" 
            onClick={() => applyPreset('page', 20, true, true)}
          >
            {t.studio.presetPage}
          </button>
          <button 
            type="button" 
            className="btn-preset" 
            onClick={() => applyPreset('group', 50, true, false)}
          >
            {t.studio.presetGroup}
          </button>
          <button 
            type="button" 
            className="btn-preset" 
            onClick={() => applyPreset('reel', 30, false, true)}
          >
            {t.studio.presetReels}
          </button>
        </div>
      </div>

      {successToast && (
        <div className="toast-cyber-success">
          <CheckCircle2 size={16} />
          <span>{t.studio.toastSuccess}</span>
        </div>
      )}

      <form onSubmit={handleDeploy} className="studio-form-grid">
        {/* Left Column: Target & Scope Configuration */}
        <div className="studio-left-card">
          <div className="card-section-header">
            <Sliders size={16} className="text-cyan" />
            <h3 className="section-title">{t.studio.sec1Title}</h3>
          </div>

          <div className="form-group-cyber">
            <label className="cyber-label">
              {t.studio.urlLabel} <span className="req">*</span>
            </label>
            <input
              type="url"
              placeholder="https://www.facebook.com/vietnamnet.vn"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              className="cyber-input-field"
              required
            />
            <span className="cyber-hint">{t.studio.urlHint}</span>
          </div>

          <div className="form-row-2">
            <div className="form-group-cyber">
              <label className="cyber-label">{t.studio.targetNameLabel}</label>
              <input
                type="text"
                placeholder={t.studio.targetNamePlaceholder}
                value={targetName}
                onChange={(e) => setTargetName(e.target.value)}
                className="cyber-input-field"
              />
            </div>

            <div className="form-group-cyber">
              <label className="cyber-label">{t.studio.typeLabel}</label>
              <select
                value={targetType}
                onChange={(e) => setTargetType(e.target.value as TargetType)}
                className="cyber-select-field"
              >
                <option value="page">{t.studio.typePage}</option>
                <option value="group">{t.studio.typeGroup}</option>
                <option value="profile">{t.studio.typeProfile}</option>
                <option value="reel">{t.studio.typeReel}</option>
              </select>
            </div>
          </div>

          <div className="card-section-header mt-4">
            <Layers size={16} className="text-emerald" />
            <h3 className="section-title">{t.studio.sec2Title}</h3>
          </div>

          <div className="range-control-group">
            <div className="range-header">
              <span className="cyber-label">{t.studio.postsLimitLabel}</span>
              <span className="range-badge">{maxPosts} {t.studio.postsUnit}</span>
            </div>
            <input
              type="range"
              min={5}
              max={200}
              step={5}
              value={maxPosts}
              onChange={(e) => setMaxPosts(Number(e.target.value))}
              className="cyber-range-slider"
            />
          </div>

          <div className="toggles-grid">
            <label className="cyber-toggle-box">
              <input
                type="checkbox"
                checked={includeComments}
                onChange={(e) => setIncludeComments(e.target.checked)}
              />
              <div className="toggle-content">
                <div className="toggle-title-line">
                  <MessageSquare size={15} />
                  <span>{t.studio.toggleComments}</span>
                </div>
                <span className="toggle-sub">{t.studio.toggleCommentsSub}</span>
              </div>
            </label>

            <label className="cyber-toggle-box">
              <input
                type="checkbox"
                checked={extractMedia}
                onChange={(e) => setExtractMedia(e.target.checked)}
              />
              <div className="toggle-content">
                <div className="toggle-title-line">
                  <Image size={15} />
                  <span>{t.studio.toggleMedia}</span>
                </div>
                <span className="toggle-sub">{t.studio.toggleMediaSub}</span>
              </div>
            </label>
          </div>
        </div>

        {/* Right Column: Stealth, Cookies & Launch Control */}
        <div className="studio-right-card">
          <div className="card-section-header">
            <ShieldCheck size={16} className="text-amber" />
            <h3 className="section-title">{t.studio.sec3Title}</h3>
          </div>

          <div className="range-control-group">
            <div className="range-header">
              <span className="cyber-label">{t.studio.scrollDelayLabel}</span>
              <span className="range-badge">{scrollDelay} {t.studio.delayUnit}</span>
            </div>
            <input
              type="range"
              min={2}
              max={10}
              step={1}
              value={scrollDelay}
              onChange={(e) => setScrollDelay(Number(e.target.value))}
              className="cyber-range-slider amber"
            />
          </div>

          <div className="form-group-cyber">
            <label className="cyber-label">
              <Key size={14} className="inline-icon" /> {t.studio.cookieLabel}
            </label>
            <textarea
              rows={4}
              placeholder={t.studio.cookiePlaceholder}
              value={cookieData}
              onChange={(e) => setCookieData(e.target.value)}
              className="cyber-textarea-field"
            />
            <span className="cyber-hint">{t.studio.cookieHint}</span>
          </div>

          <div className="pipeline-destination-box">
            <Database size={15} className="text-cyan" />
            <div className="dest-text">
              <span className="dest-title">{t.studio.destTitle}</span>
              <span className="dest-desc">{t.studio.destDesc}</span>
            </div>
          </div>

          <button type="submit" className="btn-launch-full">
            <Play size={16} fill="currentColor" />
            <span>{t.studio.btnDeployFull}</span>
          </button>
        </div>
      </form>
    </div>
  );
};
