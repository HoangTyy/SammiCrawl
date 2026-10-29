import { useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { csrfToken } from './api';

let revision = 0;
const listeners = new Set<() => void>();
function changed() { revision++; listeners.forEach(listener => listener()); }
export function useLiveRevision() {
  return useSyncExternalStore(listener => { listeners.add(listener); return () => { listeners.delete(listener); }; }, () => revision);
}

export function useLiveConnection() {
  const [status, setStatus] = useState('Đang kết nối đồng bộ…');
  useEffect(() => {
    let disposed = false;
    let retry: ReturnType<typeof setTimeout> | undefined;
    let connection: ReturnType<HubConnectionBuilder['build']> | undefined;
    const online = () => { if (!disposed) { setStatus('Đồng bộ trực tiếp'); changed(); } };
    const start = async () => {
      try {
        const token = await csrfToken();
        if (disposed) return;
        connection = new HubConnectionBuilder().withUrl('/api/live', { headers: { 'X-CSRF-TOKEN': token } })
          .withAutomaticReconnect().configureLogging(LogLevel.None).build();
        connection.on('DataChanged', changed);
        connection.onreconnecting(() => { if (!disposed) setStatus('Đang kết nối lại đồng bộ…'); });
        connection.onreconnected(online);
        connection.onclose(() => { if (!disposed) { setStatus('Mất kết nối đồng bộ · đang thử lại'); retry = setTimeout(start, 5000); } });
        await connection.start();
        if (disposed) { await connection.stop(); return; }
        online();
      } catch {
        if (!disposed) { setStatus('Mất kết nối đồng bộ · đang thử lại'); retry = setTimeout(start, 5000); }
      }
    };
    void start();
    const focus = () => { if (!disposed) changed(); };
    window.addEventListener('focus', focus);
    return () => { disposed = true; clearTimeout(retry); window.removeEventListener('focus', focus); void connection?.stop(); };
  }, []);
  return status;
}

// Mounted inside the editor: incoming refreshes never replace local form state.
export function LiveEditNotice() {
  const current = useLiveRevision();
  const openedAt = useRef(current);
  return current !== openedAt.current ? <p role="status" className="config-notice">Dữ liệu chung vừa được cập nhật. Nội dung bạn đang nhập được giữ nguyên; hãy đối chiếu trước khi lưu.</p> : null;
}
