"use client";

import { useEffect, useId, useRef, useState, type ReactNode } from "react";

// A "?" button that shows help text on hover, on keyboard focus, and on tap (touch screens have no hover).
// Escape, Tab, or a click or tap anywhere but the button closes it, and the pointer can move onto the text
// without it closing.
// The text opens above the anchor, so it never covers the controls a label introduces (the checkboxes under
// "Mark as"): those are what the user reaches for next. It's positioned against the nearest `relative`
// ancestor, so the caller decides where it lines up: the label's left edge keeps it on screen at phone widths.
export function HelpTip({ label, children }: { label: string; children: ReactNode }) {
  const [hovered, setHovered] = useState(false);
  const [focused, setFocused] = useState(false);
  const [pinned, setPinned] = useState(false);
  const button = useRef<HTMLButtonElement>(null);
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
    // pointerup rather than pointerdown: a finger that scrolls the page ends in pointercancel instead, so scrolling
    // to read the text doesn't close it. A tap on the text itself closes it too, so it never has to be tapped around.
    const onPointerUp = (event: PointerEvent) => {
      if (!button.current?.contains(event.target as Node)) close();
    };
    document.addEventListener("keydown", onKeyDown);
    document.addEventListener("pointerup", onPointerUp);
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.removeEventListener("pointerup", onPointerUp);
    };
  }, [open]);

  return (
    <span onMouseEnter={() => setHovered(true)} onMouseLeave={() => setHovered(false)}>
      <button
        ref={button}
        type="button"
        aria-label={label}
        aria-describedby={tipId}
        onFocus={() => setFocused(true)}
        onBlur={() => {
          setFocused(false);
          setPinned(false);
        }}
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
          leaves no gap, so moving the pointer from the button up onto the text keeps it open. z-30 puts it over
          the sticky header (z-20) when the label is near the top of the screen, rather than under it. */}
      <span
        id={tipId}
        role="tooltip"
        className={`absolute bottom-full left-0 z-30 w-80 max-w-[calc(100vw-6rem)] pb-1 ${open ? "" : "invisible"}`}
      >
        <span className="block space-y-2 rounded-xl border border-border bg-bg p-4 text-sm font-normal text-text shadow-lg">
          {children}
        </span>
      </span>
    </span>
  );
}
