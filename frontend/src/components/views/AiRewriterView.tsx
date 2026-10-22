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

export const AiRewriterView: React.FC = () => {
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
        result = `🚀 [BẢN VIẾT LẠI TỐI ƯU]\n\n${inputText}\n\n👉 Điểm nhấn: Nội dung đã được tinh chỉnh giọng văn ${tone === 'engaging' ? 'cuốn hút' : tone === 'viral' ? 'viral bùng nổ' : 'chuyên nghiệp'}, rút ngắn câu chữ và bổ sung call-to-action mạnh mẽ.`;
      } else if (mode === 'summary') {
        result = `📌 TÓM TẮT Ý CHÍNH TỪ BÀI VIẾT:\n1. Bối cảnh chính: ${inputText.slice(0, 80)}...\n2. Điểm cốt lõi: Tập trung vào giải pháp và kết nối độc giả.\n3. Nhận định: Tiềm năng tương tác cao.`;
      } else {
        result = `🎬 KỊCH BẢN SHORT VIDEO / REELS (30s):\n[0:00 - 0:03] Hook giật gân: "Bạn đã biết tin này chưa?"\n[0:03 - 0:15] Nội dung chính: ${inputText.slice(0, 100)}...\n[0:15 - 0:25] Bài học / Cú twist.\n[0:25 - 0:30] CTA: Thả tim và follow để nhận thêm tin mới!`;
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
          <h2 className="view-title">AI Rewriter & Content Intelligence</h2>
          <p className="view-subtitle">Chuyển đổi bài viết Facebook đã cào thành nội dung mới, tóm tắt ý chính hoặc kịch bản video</p>
        </div>

        {/* Mode selector */}
        <div className="ai-mode-tabs">
          <button 
            type="button" 
            className={`ai-mode-btn ${mode === 'rewrite' ? 'active' : ''}`}
            onClick={() => setMode('rewrite')}
          >
            <Sparkles size={14} />
            <span>Viết lại Nội Dung</span>
          </button>
          <button 
            type="button" 
            className={`ai-mode-btn ${mode === 'summary' ? 'active' : ''}`}
            onClick={() => setMode('summary')}
          >
            <FileText size={14} />
            <span>Tóm tắt Ý Chính</span>
          </button>
          <button 
            type="button" 
            className={`ai-mode-btn ${mode === 'video' ? 'active' : ''}`}
            onClick={() => setMode('video')}
          >
            <Flame size={14} />
            <span>Kịch bản Reels/TikTok</span>
          </button>
        </div>
      </div>

      <div className="rewriter-work-grid">
        {/* Input Panel */}
        <div className="cyber-panel input-panel">
          <div className="panel-header-line">
            <span className="panel-title-text">Nội dung Facebook Gốc (Source Text)</span>
            <span className="char-count">{inputText.length} ký tự</span>
          </div>

          <textarea
            rows={12}
            placeholder="Dán nội dung bài viết Facebook vừa cào vào đây hoặc chọn từ Kho dữ liệu..."
            value={inputText}
            onChange={(e) => setInputText(e.target.value)}
            className="rewriter-textarea"
          />

          <div className="input-toolbar">
            <div className="tone-picker">
              <span className="tool-label">Giọng văn:</span>
              <select 
                value={tone} 
                onChange={(e) => setTone(e.target.value)}
                className="tool-select"
              >
                <option value="engaging">Cuốn hút & Tương tác cao</option>
                <option value="viral">Viral & Bắt Trend</option>
                <option value="professional">Chuyên nghiệp & Tin cậy</option>
                <option value="story">Kể chuyện (Storytelling)</option>
              </select>
            </div>

            <button
              type="button"
              className="btn-run-ai"
              onClick={handleRewrite}
              disabled={!inputText.trim() || isProcessing}
            >
              {isProcessing ? <RefreshCw size={15} className="spin-fast" /> : <Wand2 size={15} />}
              <span>{isProcessing ? 'Đang phân tích...' : 'Thực thi biến đổi AI'}</span>
            </button>
          </div>
        </div>

        {/* Output Panel */}
        <div className="cyber-panel output-panel">
          <div className="panel-header-line">
            <span className="panel-title-text">Kết quả AI Tạo mới (Output Result)</span>
            {outputText && (
              <button 
                type="button" 
                className="btn-copy-terminal"
                onClick={handleCopy}
              >
                {copied ? <Check size={12} /> : <Copy size={12} />}
                <span>{copied ? 'Đã sao chép' : 'Sao chép'}</span>
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
                Nhập nội dung ở khung bên trái và bấm <strong>"Thực thi biến đổi AI"</strong> để xem kết quả được tối ưu hóa.
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
