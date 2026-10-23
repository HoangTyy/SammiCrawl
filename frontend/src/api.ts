export async function api<T = unknown>(path: string, data?: unknown, method?: string): Promise<T> {
  const response = await fetch(`/api${path}`, { method: method ?? (data === undefined ? 'GET' : 'POST'), headers: data === undefined ? {} : { 'Content-Type': 'application/json' }, body: data === undefined ? undefined : JSON.stringify(data) });
  const result = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(result.error ?? (response.status === 401 ? 'Vui lòng đăng nhập lại.' : `API lỗi ${response.status}`));
  return result as T;
}
