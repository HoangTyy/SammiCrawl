export type BrandChannel = { id: string; name: string; type: 'group' | 'page'; url: string; dailyTarget: number; minWords: number; maxWords: number; audience?: string; tone?: string; contentGuidelines?: string; imageRequirements?: string; publishAccountId?: string; requireBranding?: boolean; active?: boolean };
export type Brand = { id: string; name?: string; facebookGroupUrl?: string; channels?: BrandChannel[]; updatedAt?: string; [key: string]: unknown };
export function brandChannels(brand?: Brand): BrandChannel[] {
  if (!brand) return [];
  if (Array.isArray(brand.channels)) return brand.channels;
  return brand.facebookGroupUrl ? [{ id: 'legacy-group', name: 'Nhóm Facebook', type: 'group', url: brand.facebookGroupUrl, dailyTarget: 0, minWords: 80, maxWords: 250, active: true, requireBranding: false }] : [];
}
export const reviewFields = (brand?: Brand, channel?: BrandChannel): [string, string][] => [
  ['topicReviewed', 'Nội dung đúng chủ đề, đối tượng và giọng thương hiệu'],
  ...(brand?.verifyInformation !== false ? [['sourceVerified', 'Đã kiểm chứng nguồn, số liệu, ưu đãi và thời hạn'] as [string, string]] : []),
  ...(brand?.requireMediaRights !== false ? [['mediaRightsConfirmed', 'Có quyền sử dụng nội dung, ảnh và video gốc'] as [string, string]] : []),
  ...(brand?.requireSponsorLabel !== false ? [['sponsorshipReviewed', 'Đã kiểm tra và gắn nhãn tài trợ/hợp tác nếu có'] as [string, string]] : []),
  ...(channel?.requireBranding ?? channel?.type === 'page' ? [['brandingConfirmed', 'Media đáp ứng nhận diện và yêu cầu hình ảnh của kênh'] as [string, string]] : []),
];
