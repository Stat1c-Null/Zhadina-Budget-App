"use client";

import { useEffect, useId, useRef, useState, type ReactNode } from "react";

// A "?" button that shows help text on hover, on keyboard focus, and on tap (touch screens have no hover).
// Escape or a tap elsewhere closes it, and the pointer can move onto the text without it closing.
// The text is positioned against the nearest `relative` ancestor, so the caller decides where it lines up:
// anchoring it to the label's left edge keeps it on screen at phone widths.
export function HelpTip({ label, children }: { label: string; children: ReactNode }) {
  const [hovered, setHovered] = useState(false);
  const [focused, setFocused] = useState(false);
  const [pinned, setPinned] = useState(false);
  const root = useRef<HTMLSpanElement>(null);
  const tipId = useId();
  const open = hovered || focused || pinned;

  useEffect(() => {
    if (!open) return;
    const close = () => {
      setHovered(false);
      setFocused(false);
      setPinned(false);
    };
    const onKeyDown = (event: KeyboardEvent) => event.key === "Escape" && close();
    const onPointerDown = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) close();
    };
    document.addEventListener("keydown", onKeyDown);
    document.addEventListener("pointerdown", onPointerDown);
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.removeEventListener("pointerdown", onPointerDown);
    };
  }, [open]);

  return (
    <span ref={root} onMouseEnter={() => setHovered(true)} onMouseLeave={() => setHovered(false)}>
      <button
        type="button"
        aria-label={label}
        aria-describedby={tipId}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        onClick={() => setPinned(true)}
        className="inline-flex size-6 cursor-help items-center justify-center rounded-full text-muted hover:text-text focus-visible:outline-2 focus-visible:outline-primary"
      >
        <span
          aria-hidden="true"
          className="flex size-5 items-center justify-center rounded-full border border-current text-xs font-semibold"
        >
          ?
        </span>
      </button>
      {/* Always rendered so aria-describedby can read it; only shown while open. Padding rather than a margin
          leaves no gap, so moving the pointer from the button onto the text keeps it open. The padding is
          transparent, so it's kept small enough that nothing below shows through it. -left-2 moves the rounded
          corner off whatever starts at the anchor's left edge (a checkbox, here), which showed through the curve. */}
      <span
        id={tipId}
        role="tooltip"
        className={`absolute top-full -left-2 z-10 w-80 max-w-[calc(100vw-6rem)] pt-1 ${open ? "" : "invisible"}`}
      >
        <span className="block space-y-2 rounded-xl border border-border bg-bg p-4 text-sm font-normal text-text shadow-lg">
          {children}
        </span>
      </span>
    </span>
  );
}
