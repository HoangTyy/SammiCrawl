let csrfRequest: Promise<string> | undefined;
function csrfToken(): Promise<string> {
  return csrfRequest ??= fetch('/api/auth/csrf', { cache: 'no-store', credentials: 'same-origin' })
    .then(async response => {
      if (!response.ok) throw new Error('Không lấy được phiên xác nhận. Tải lại trang rồi thử lại.');
      const result = await response.json();
      if (typeof result.token !== 'string' || !result.token) throw new Error('Phiên xác nhận không hợp lệ.');
      return result.token;
    }).catch(error => { csrfRequest = undefined; throw error; });
}

export async function api<T = unknown>(path: string, data?: unknown, method?: string): Promise<T> {
  const requestMethod = method ?? (data === undefined ? 'GET' : 'POST');
  const headers: Record<string, string> = data === undefined ? {} : { 'Content-Type': 'application/json' };
  if (!['GET', 'HEAD', 'OPTIONS'].includes(requestMethod.toUpperCase())) headers['X-CSRF-TOKEN'] = await csrfToken();
  const response = await fetch(`/api${path}`, { method: requestMethod, headers, body: data === undefined ? undefined : JSON.stringify(data) });
  const result = await response.json().catch(() => ({}));
  if (result.code === 'CSRF_TOKEN_INVALID' || (response.ok && ['/auth/login', '/auth/setup', '/auth/logout'].includes(path))) csrfRequest = undefined;
  if (!response.ok) throw new Error(result.error ?? (response.status === 401 ? 'Vui lòng đăng nhập lại.' : `API lỗi ${response.status}`));
  return result as T;
}
