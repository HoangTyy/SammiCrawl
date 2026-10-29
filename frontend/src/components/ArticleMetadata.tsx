export type ArticleMetadataFields = {
  createdAt: string; sourcePublishedAt?: string; sourcePublishedText?: string;
  reactions?: number | null; likes?: number | null; comments?: number | null;
  reactionCountText?: string; reactionCountApproximate?: boolean;
  likeCountText?: string; likeCountApproximate?: boolean;
  commentCountText?: string; commentCountApproximate?: boolean;
  interactionCapturedAt?: string;
};

const dateText = (value?: string) => value && Number.isFinite(Date.parse(value))
  ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : undefined;
const validCount = (value?: number | null): value is number => typeof value === 'number' && Number.isSafeInteger(value) && value >= 0;
const counter = (value: number, approximate?: boolean, raw?: string) => `${approximate ? '≈ ' : ''}${approximate && raw ? raw : value.toLocaleString('vi-VN')}`;

export function ArticleMetadata({ article }: { article: ArticleMetadataFields }) {
  const reactions = validCount(article.reactions) ? `${counter(article.reactions, article.reactionCountApproximate, article.reactionCountText)} cảm xúc`
    : validCount(article.likes) ? `${counter(article.likes, article.likeCountApproximate, article.likeCountText)} thích` : 'Chưa đọc được cảm xúc';
  const comments = validCount(article.comments) ? `${counter(article.comments, article.commentCountApproximate, article.commentCountText)} bình luận` : 'Chưa đọc được bình luận';
  return <div className="article-metadata">
    <span>Thu thập: {dateText(article.createdAt) ?? 'Chưa có thời gian'}</span>
    <small>{dateText(article.sourcePublishedAt) ? `Đăng gốc: ${dateText(article.sourcePublishedAt)}` : article.sourcePublishedText ? `Facebook lúc cào: ${article.sourcePublishedText}` : 'Đăng gốc: chưa đọc được'}</small>
    <small title={article.interactionCapturedAt ? `Số liệu ghi nhận lúc ${dateText(article.interactionCapturedAt) ?? article.interactionCapturedAt}; có thể thay đổi sau đó.` : undefined}>{reactions} · {comments}</small>
  </div>;
}
