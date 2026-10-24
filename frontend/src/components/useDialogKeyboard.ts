import { useEffect, useEffectEvent, useRef } from 'react';

// Each modal owns its keyboard boundary and restores the invoking control on close.
export function useDialogKeyboard(open: boolean, onClose: () => void, busy = false) {
  const ref = useRef<HTMLElement>(null);
  const dismiss = useEffectEvent(() => { if (!busy) onClose(); });
  useEffect(() => {
    if (!open || !ref.current) return;
    const dialog = ref.current;
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const overflow = document.body.style.overflow;
    const inertNodes = new Map<HTMLElement, boolean>();
    for (let current: HTMLElement | null = dialog; current?.parentElement; current = current.parentElement) {
      for (const sibling of current.parentElement.children) {
        if (sibling !== current && sibling instanceof HTMLElement) { inertNodes.set(sibling, sibling.inert); sibling.inert = true; }
      }
    }
    document.body.style.overflow = 'hidden';
    const controls = () => Array.from(dialog.querySelectorAll<HTMLElement>('button, input, select, textarea, a[href], summary, video[controls], [tabindex]')).filter(element => !element.matches(':disabled') && element.tabIndex >= 0 && element.getClientRects().length > 0);
    dialog.tabIndex = -1;
    (controls()[0] ?? dialog).focus();
    const keyboard = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); dismiss(); }
      if (event.key !== 'Tab') return;
      const items = controls();
      const first = items[0]; const last = items.at(-1);
      if (!first || !last) { event.preventDefault(); dialog.focus(); return; }
      if (!dialog.contains(document.activeElement) || document.activeElement === dialog) { event.preventDefault(); (event.shiftKey ? last : first).focus(); }
      else if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    };
    document.addEventListener('keydown', keyboard, true);
    return () => {
      document.removeEventListener('keydown', keyboard, true);
      inertNodes.forEach((value, node) => { node.inert = value; });
      document.body.style.overflow = overflow;
      if (previous?.isConnected) previous.focus();
    };
  }, [open]);
  return ref;
}
