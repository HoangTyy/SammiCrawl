import { api } from '../../api';
import React, { useEffect, useState } from 'react';
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
  const [error, setError] = useState('');
  const [brands, setBrands] = useState<{id: string; name: string; tone: string; audience: string; rules: string; description?: string; writingStyle?: string; callToAction?: string; requiredKeywords?: string; forbiddenKeywords?: string}[]>([]);
  useEffect(() => { api<typeof brands>('/records/brands').then(setBrands).catch(e => setError(e.message)); }, []);
  const [tone, setTone] = useState('engaging');
  const [mode, setMode] = useState('rewrite');
  const [isProcessing, setIsProcessing] = useState(false);
  const [copied, setCopied] = useState(false);


  const [instructions, setInstructions] = useState(() => localStorage.getItem('sammicrawl_ai_instructions') ?? '');
  const handleRewrite = async () => {
    if (!inputText.trim()) return;
    setIsProcessing(true); setError(''); setOutputText('');
    localStorage.setItem('sammicrawl_ai_instructions', instructions);
    try { const result = await api<{text: string}>('/ai/rewrite', { text: inputText, tone, mode, instructions }); setOutputText(result.text); }
    catch(e) { setError((e as Error).message); }
    finally { setIsProcessing(false); }
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

      <p className="view-subtitle">Bonsai 2 27B · Chỉ chạy một model mỗi lần · Kết quả lưu vào PostgreSQL</p>
      {error && <p role="alert">{error}</p>}
      <label>Chọn thương hiệu<select className="cyber-select-field" defaultValue="" onChange={e => { const b = brands.find(b => b.id === e.target.value); if(b) setInstructions(`Thương hiệu: ${b.name}. Giọng văn: ${b.tone}. Đối tượng: ${b.audience}. Quy tắc: ${b.rules ?? ''}. Mô tả: ${b.description ?? ''}. Phong cách: ${b.writingStyle ?? ''}. CTA: ${b.callToAction ?? ''}. Từ khóa bắt buộc: ${b.requiredKeywords ?? ''}. Từ khóa cấm: ${b.forbiddenKeywords ?? ''}`); }}><option value="">Tự nhập chỉ dẫn</option>{brands.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}</select></label><label>Chỉ dẫn thương hiệu<textarea className="cyber-textarea-field" value={instructions} onChange={e => setInstructions(e.target.value)} placeholder="Giọng văn, đối tượng, CTA, từ khóa cần có hoặc cần tránh…" /></label>
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
