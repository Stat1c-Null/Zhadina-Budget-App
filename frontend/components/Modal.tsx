"use client";

import { useEffect, useId, useRef, useState, type ReactNode } from "react";

// A modal on the native <dialog>, opened with showModal(). The browser moves focus in, makes the page behind
// inert, closes it on Escape, draws it above the sticky header (the top layer needs no z-index), and returns
// focus to the button that opened it once it's closed.
// Mount it only while it should be open: it opens itself on mount. Close it with the `close` it hands to
// its children, not by unmounting, so the browser can return focus; onClose then runs, and the parent unmounts it.
// A click on the backdrop doesn't close it, so a misclick can't throw away a half-filled form.
export function Modal({
  title,
  onClose,
  children,
}: {
  title: string;
  onClose: () => void;
  children: (close: () => void) => ReactNode;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  // `close` only asks; the effect below closes the dialog, so the ref is never read while rendering.
  const [closeRequested, setCloseRequested] = useState(false);

  useEffect(() => {
    // Checked because React's strict mode runs this twice in development.
    // No cleanup that closes it: removing the element from the page already takes it out of the top layer,
    // while close() in a cleanup would fire `close` and make the parent drop a modal that just opened.
    if (!dialog.current?.open) dialog.current?.showModal();
  }, []);

  useEffect(() => {
    if (closeRequested) dialog.current?.close();
  }, [closeRequested]);

  return (
    <dialog
      ref={dialog}
      aria-labelledby={titleId}
      onClose={onClose}
      className="m-auto max-h-[calc(100dvh-2rem)] w-[calc(100%-2rem)] max-w-lg overflow-y-auto rounded-2xl border border-border bg-surface p-6 text-text backdrop:bg-bg/80"
    >
      <h2 id={titleId} className="mb-4 text-xl font-semibold">
        {title}
      </h2>
      {children(() => setCloseRequested(true))}
    </dialog>
  );
}
