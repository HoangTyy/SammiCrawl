import React, { useState } from 'react';
import { 
  Wand2, 
  Sparkles, 
  Copy, 
  Check, 
  FileText, 
  RefreshCw, 
  Bot,
  Flame 
} from 'lucide-react';
import { useApp } from '../../context/AppContext';

export const AiRewriterView: React.FC = () => {
  const { t } = useApp();
  const [inputText, setInputText] = useState('');
  const [outputText, setOutputText] = useState('');
  const [tone, setTone] = useState('engaging');
  const [mode, setMode] = useState('rewrite');
  const [isProcessing, setIsProcessing] = useState(false);
  const [copied, setCopied] = useState(false);

  const handleRewrite = () => {
    if (!inputText.trim()) return;
    setIsProcessing(true);
    
    setTimeout(() => {
      let result = '';
      if (mode === 'rewrite') {
        result = `🚀 [REWRITTEN CONTENT]\n\n${inputText}\n\n👉 Key Highlights: Optimized for maximum reach, concise sentence structure, and clear engagement drivers.`;
      } else if (mode === 'summary') {
        result = `📌 EXECUTIVE SUMMARY:\n1. Context: ${inputText.slice(0, 80)}...\n2. Core value: Reader engagement and solution clarity.\n3. Sentiment: High viral potential.`;
      } else {
        result = `🎬 SHORT VIDEO SCRIPT / REELS (30s):\n[0:00 - 0:03] Hook: "Did you catch this trend today?"\n[0:03 - 0:15] Core story: ${inputText.slice(0, 100)}...\n[0:15 - 0:25] The Twist / Lesson.\n[0:25 - 0:30] CTA: Like & Follow for more updates!`;
      }
      setOutputText(result);
      setIsProcessing(false);
    }, 700);
  };

  const handleCopy = () => {
    navigator.clipboard.writeText(outputText);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="view-container rewriter-view">
      <div className="view-header-strip">
        <div>
          <h2 className="view-title">{t.rewriter.title}</h2>
          <p className="view-subtitle">{t.rewriter.subtitle}</p>
        </div>

        {/* Mode selector */}
        <div className="ai-mode-tabs">
          <button 
            type="button" 
            className={`ai-mode-btn ${mode === 'rewrite' ? 'active' : ''}`}
            onClick={() => setMode('rewrite')}
          >
            <Sparkles size={14} />
            <span>{t.rewriter.modeRewrite}</span>
          </button>
          <button 
            type="button" 
            className={`ai-mode-btn ${mode === 'summary' ? 'active' : ''}`}
            onClick={() => setMode('summary')}
          >
            <FileText size={14} />
            <span>{t.rewriter.modeSummary}</span>
          </button>
          <button 
            type="button" 
            className={`ai-mode-btn ${mode === 'video' ? 'active' : ''}`}
            onClick={() => setMode('video')}
          >
            <Flame size={14} />
            <span>{t.rewriter.modeVideo}</span>
          </button>
        </div>
      </div>

      <div className="rewriter-work-grid">
        {/* Input Panel */}
        <div className="cyber-panel input-panel">
          <div className="panel-header-line">
            <span className="panel-title-text">{t.rewriter.sourcePanelTitle}</span>
            <span className="char-count">{t.rewriter.charCount.replace('{count}', inputText.length.toString())}</span>
          </div>

          <textarea
            rows={12}
            placeholder={t.rewriter.sourcePlaceholder}
            value={inputText}
            onChange={(e) => setInputText(e.target.value)}
            className="rewriter-textarea"
          />

          <div className="input-toolbar">
            <div className="tone-picker">
              <span className="tool-label">{t.rewriter.toneLabel}</span>
              <select 
                value={tone} 
                onChange={(e) => setTone(e.target.value)}
                className="tool-select"
              >
                <option value="engaging">{t.rewriter.toneEngaging}</option>
                <option value="viral">{t.rewriter.toneViral}</option>
                <option value="professional">{t.rewriter.toneProfessional}</option>
                <option value="story">{t.rewriter.toneStory}</option>
              </select>
            </div>

            <button
              type="button"
              className="btn-run-ai"
              onClick={handleRewrite}
              disabled={!inputText.trim() || isProcessing}
            >
              {isProcessing ? <RefreshCw size={15} className="spin-fast" /> : <Wand2 size={15} />}
              <span>{isProcessing ? t.rewriter.btnProcessing : t.rewriter.btnRun}</span>
            </button>
          </div>
        </div>

        {/* Output Panel */}
        <div className="cyber-panel output-panel">
          <div className="panel-header-line">
            <span className="panel-title-text">{t.rewriter.outputPanelTitle}</span>
            {outputText && (
              <button 
                type="button" 
                className="btn-copy-terminal"
                onClick={handleCopy}
              >
                {copied ? <Check size={12} /> : <Copy size={12} />}
                <span>{copied ? t.dashboard.copied : t.dashboard.copy}</span>
              </button>
            )}
          </div>

          {outputText ? (
            <div className="output-content-box">
              <pre className="output-pre">{outputText}</pre>
            </div>
          ) : (
            <div className="output-empty-box">
              <Bot size={36} className="text-dim mb-2" />
              <p className="empty-sub">
                {t.rewriter.outputEmpty}
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
