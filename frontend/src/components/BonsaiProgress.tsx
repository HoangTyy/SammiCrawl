export function BonsaiProgress({ progress, withImages, seconds }: {
  progress: number | null; withImages: boolean; seconds: number;
}) {
  if (progress === null) return null;
  const running = progress < 100;
  return <div className="rewrite-progress" role="status" aria-live="polite" aria-busy={running}>
    <div className="rewrite-progress-heading"><strong>{running ? withImages ? 'Bonsai đang xem ảnh để hiểu ngữ cảnh và viết lại phần chữ…' : 'Bonsai đang viết lại phần chữ…' : 'Đã viết lại và lưu bản nháp'}</strong><span>{progress}%{running ? ' · ước tính' : ''}</span></div>
    <progress aria-label="Tiến độ viết lại ước tính" max={100} value={progress} />
    <small>{running ? `Đã chờ ${seconds} giây. ${progress >= 90 ? 'Vẫn đang chờ máy AI trả kết quả.' : 'Đây là tiến độ ước tính, không phải tiến độ suy luận thực.'} Bạn có thể đóng hộp thoại; yêu cầu đã gửi vẫn tiếp tục, không cần bấm lại.` : 'Có thể mở lại trong Bản nháp & lịch đăng.'}</small>
  </div>;
}
