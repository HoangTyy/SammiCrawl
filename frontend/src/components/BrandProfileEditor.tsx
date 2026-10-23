import { Plus, Trash2 } from 'lucide-react';
import { brandChannels } from '../brandPolicy';
import type { Brand, BrandChannel } from '../brandPolicy';

const fields = [
  ['name', 'Tên thương hiệu'], ['description', 'Mô tả và sứ mệnh'], ['language', 'Ngôn ngữ'],
  ['audience', 'Đối tượng mục tiêu'], ['tone', 'Giọng điệu'], ['writingStyle', 'Cấu trúc / phong cách viết'],
  ['allowedTopics', 'Chủ đề được phép (mỗi dòng một chủ đề)'], ['forbiddenContent', 'Nội dung không được đăng'],
  ['callToAction', 'Kêu gọi hành động (CTA)'], ['requiredKeywords', 'Từ khóa bắt buộc'], ['forbiddenKeywords', 'Từ khóa cấm'], ['rules', 'Quy tắc bổ sung'],
];
export function BrandProfileEditor({ value, onChange }: { value: Brand; onChange: (brand: Brand) => void }) {
  const channels = brandChannels(value);
  const update = (index: number, patch: Partial<BrandChannel>) => onChange({ ...value, channels: channels.map((channel, i) => i === index ? { ...channel, ...patch } : channel) });
  const applyTemplate = () => onChange({ ...value,
    tone: value.tone || 'Gần gũi, chân thành, không phán xét, không giáo điều; người bạn đồng hành của học sinh – sinh viên.',
    writingStyle: 'Hook mở đầu → nội dung cụ thể, có ví dụ → kết bài và CTA rõ ràng. Dễ đọc trên điện thoại.',
    forbiddenContent: 'Thông tin sai sự thật; xúc phạm cá nhân/tổ chức; chủ đề nhạy cảm chính trị, tôn giáo, vùng miền, giới tính; bạo lực và nội dung người lớn.',
    rules: 'Không bịa trải nghiệm. Ghi rõ nguồn trích dẫn. Media phải có quyền sử dụng; không dùng logo/watermark bên khác khi chưa được phép. Bài chuyển từ Group lên Page cần sự đồng ý của người chia sẻ gốc.',
    verifyInformation: true, requireMediaRights: true, requireSponsorLabel: true,
  });
  return <>
    <div className="policy-section-heading"><div><h4>Hồ sơ và quy chuẩn nội dung</h4><p>Dùng chung khi Clef đánh giá, Bonsai viết lại và duyệt bài.</p></div><button className="btn-preset" type="button" onClick={applyTemplate}>Áp dụng quy chuẩn chung StuXP</button></div>
    {fields.map(([key, label]) => <label className="setting-field" key={key}>{label}{['name', 'language'].includes(key)
      ? <input className="cyber-input-field" required={key === 'name'} maxLength={key === 'language' ? 12 : 200} value={String(value[key] ?? '')} onChange={e => onChange({ ...value, [key]: e.target.value })} />
      : <textarea rows={3} maxLength={10000} className="cyber-textarea-field" value={String(value[key] ?? '')} onChange={e => onChange({ ...value, [key]: e.target.value })} />}</label>)}
    <div className="policy-checklist">{[['verifyInformation', 'Bắt buộc kiểm chứng nguồn, số liệu và thời hạn'], ['requireMediaRights', 'Bắt buộc xác nhận quyền sử dụng nội dung/media'], ['requireSponsorLabel', 'Kiểm tra nhãn tài trợ/hợp tác trước khi đăng']].map(([key, label]) => <label key={key}><input type="checkbox" checked={value[key] !== false} onChange={e => onChange({ ...value, [key]: e.target.checked })} />{label}</label>)}</div>
    <div className="policy-section-heading"><div><h4>Kênh đăng của thương hiệu</h4><p>Một thương hiệu có thể có Group và Page, mỗi kênh một quy chuẩn.</p></div><button type="button" className="btn-preset" onClick={() => onChange({ ...value, channels: [...channels, { id: crypto.randomUUID(), name: '', type: 'group', url: '', dailyTarget: 0, minWords: 80, maxWords: 250, active: true, requireBranding: false }] })}><Plus size={14} /> Thêm kênh</button></div>
    {!channels.length && <p className="config-notice">Chưa có kênh đăng. Bạn có thể lưu hồ sơ trước, bổ sung kênh khi chuẩn bị bài.</p>}
    {channels.map((channel, index) => <section className="policy-channel" key={channel.id}>
      <div className="policy-section-heading"><strong>Kênh {index + 1}</strong><button type="button" className="workspace-icon-button" aria-label={`Xóa kênh ${index + 1}`} onClick={() => onChange({ ...value, channels: channels.filter((_, i) => i !== index) })}><Trash2 size={16} /></button></div>
      <div className="form-row-2"><label className="setting-field">Tên kênh<input className="cyber-input-field" required maxLength={200} value={channel.name} onChange={e => update(index, { name: e.target.value })} /></label><label className="setting-field">Loại kênh đăng<select className="cyber-select-field" value={channel.type} onChange={e => { const type = e.target.value as 'group' | 'page'; update(index, { type, minWords: type === 'group' ? 80 : 50, maxWords: type === 'group' ? 250 : 150, requireBranding: type === 'page' }); }}><option value="group">Facebook Group</option><option value="page">Facebook Page</option></select></label></div>
      <label className="setting-field">URL kênh đích<input className="cyber-input-field" type="url" required value={channel.url} onChange={e => update(index, { url: e.target.value })} placeholder={channel.type === 'group' ? 'https://www.facebook.com/groups/…' : 'https://www.facebook.com/…'} /></label>
      <div className="form-row-2">{([['dailyTarget', 'Chỉ tiêu bài/ngày', 0, 1000], ['minWords', 'Độ dài tối thiểu (từ)', 1, 3000], ['maxWords', 'Độ dài tối đa (từ)', 1, 3000]] as const).map(([key, label, min, max]) => <label className="setting-field" key={key}>{label}<input className="cyber-input-field" type="number" required min={min} max={max} step={1} value={channel[key]} onChange={e => update(index, { [key]: Number(e.target.value) })} /></label>)}</div>
      {([['audience', 'Đối tượng riêng của kênh'], ['tone', 'Tone riêng của kênh'], ['contentGuidelines', 'Yêu cầu nội dung riêng của kênh']] as const).map(([key, label]) => <label className="setting-field" key={key}>{label}<textarea rows={3} maxLength={5000} className="cyber-textarea-field" value={channel[key] ?? ''} onChange={e => update(index, { [key]: e.target.value })} /></label>)}
      <label className="setting-field">Yêu cầu ảnh/video<textarea className="cyber-textarea-field" rows={2} maxLength={5000} value={channel.imageRequirements ?? ''} onChange={e => update(index, { imageRequirements: e.target.value })} placeholder="Ảnh thật, số góc chụp, tỉ lệ ảnh, logo/watermark…" /></label>
      <div className="policy-checklist"><label><input type="checkbox" checked={channel.requireBranding ?? channel.type === 'page'} onChange={e => update(index, { requireBranding: e.target.checked })} />Yêu cầu nhận diện thương hiệu trên media</label><label><input type="checkbox" checked={channel.active !== false} onChange={e => update(index, { active: e.target.checked })} />Kênh đang hoạt động</label></div>
    </section>)}
  </>;
}
