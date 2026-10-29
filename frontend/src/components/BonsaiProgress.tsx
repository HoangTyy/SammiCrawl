export function BonsaiProgress({ reading, imageProgress, rewriteProgress, seconds }: {
  reading: boolean; imageProgress: number | null; rewriteProgress: number | null; seconds: number;
}) {
  const progress = rewriteProgress ?? imageProgress;
  if (progress === null) return null;
  const rewriting = rewriteProgress !== null;
  const running = reading || progress < 100;
  return <div className="rewrite-progress" role="status" aria-live="polite" aria-busy={running}>
    <div className="rewrite-progress-heading"><strong>{running ? rewriting ? 'Bonsai đang viết lại…' : 'Bonsai đang đọc chữ và mô tả ảnh…' : rewriting ? 'Đã viết lại và lưu bản nháp' : 'Đã nhận ghi chú ảnh · cần đối chiếu và xác nhận'}</strong><span>{progress}%{running ? ' · ước tính' : ''}</span></div>
    <progress aria-label={rewriting ? 'Tiến độ viết lại ước tính' : 'Tiến độ đọc ảnh ước tính'} max={100} value={progress} />
    <small>{running ? `Đã chờ ${seconds} giây. ${progress >= 90 ? 'Vẫn đang chờ máy AI trả kết quả.' : 'Đây là tiến độ ước tính, không phải tiến độ suy luận thực.'} Bạn có thể đóng hộp thoại; yêu cầu đã gửi vẫn tiếp tục, không cần bấm lại.` : rewriting ? 'Có thể mở lại trong Bản nháp & lịch đăng.' : 'Chưa rewrite. Mở lại bài, kiểm tra ghi chú ảnh và tự xác nhận trước khi viết lại.'}</small>
  </div>;
}
