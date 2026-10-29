export type ClefEvaluationProgress = {
  articleId: string;
  title: string;
  percent: number;
  seconds: number;
  state: 'running' | 'done' | 'error';
  result?: string;
};

export function ClefProgress({ evaluation }: { evaluation: ClefEvaluationProgress | null }) {
  if (!evaluation) return null;
  const running = evaluation.state === 'running';
  return <div className="rewrite-progress" role="status" aria-live="polite" aria-busy={running}>
    <div className="rewrite-progress-heading"><strong>{running ? 'Clef đang đánh giá bài và ảnh…' : evaluation.state === 'done' ? 'Clef đã đánh giá xong' : 'Clef chưa đánh giá xong'}</strong>
      <span>{evaluation.state !== 'error' && <>{evaluation.percent}%{running ? ' · ước tính' : ''}</>}</span></div>
    <small>{evaluation.title}</small>
    {evaluation.state !== 'error' && <progress aria-label="Tiến độ đánh giá Clef ước tính" max={100} value={evaluation.percent} />}
    <small>{running ? `Đã chờ ${evaluation.seconds} giây. ${evaluation.percent >= 90 ? 'Vẫn đang chờ máy AI trả kết quả. Chưa hoàn tất đánh giá.' : 'Tiến độ ước tính, chưa phải tiến độ suy luận thực.'} Chỉ đạt 100% khi nhận kết quả thành công; không cần bấm lại.` : evaluation.result}</small>
  </div>;
}
