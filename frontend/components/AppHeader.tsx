"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useId, useRef } from "react";
import { Button } from "@/components/Button";
import type { User } from "@/lib/api";

const NAV_LINKS = [
  { href: "/dashboard", label: "Dashboard" },
  { href: "/accounts", label: "Accounts" },
];

// The native popover gives us Escape, click-outside and focus return; see .hud-* in globals.css for the look.
// `user` is null while the session check runs: the bar stays up so page changes don't flash.
export function AppHeader({ user, onSignOut }: { user: User | null; onSignOut: () => void }) {
  const pathname = usePathname();
  const menu = useRef<HTMLElement>(null);
  // Next keeps visited pages mounted but hidden, so several headers can exist at once and each needs its own id.
  const menuId = useId();

  return (
    <header className="hud sticky top-0 z-20 flex h-18 items-center justify-between gap-3 px-4 sm:px-6">
      <button
        type="button"
        popoverTarget={menuId}
        aria-label="Zhadina Budget menu"
        className="hud-cut hud-menu-button flex h-11 w-48 shrink-0 cursor-pointer items-center gap-3 bg-hull px-4 font-hud font-semibold text-hull-ink hover:bg-hull-hover"
      >
        <span aria-hidden="true" className="hud-burger">
          <span />
          <span />
          <span />
        </span>
        Zhadina Budget
      </button>

      {/* The top of this panel meets the button's bottom edge: h-18 header, h-11 button, centered. */}
      <nav
        id={menuId}
        ref={menu}
        popover="auto"
        aria-label="Main"
        className="hud-menu fixed top-[3.625rem] right-auto bottom-auto left-4 w-48 bg-hull pb-2 font-hud font-medium text-hull-ink sm:left-6"
      >
        {NAV_LINKS.map(({ href, label }) => (
          <Link
            key={href}
            href={href}
            aria-current={pathname === href ? "page" : undefined}
            onClick={() => menu.current?.hidePopover()}
            className="block px-4 py-2.5 transition-colors not-aria-[current=page]:hover:bg-hull-hover focus-visible:outline-2 focus-visible:-outline-offset-4 focus-visible:outline-current aria-[current=page]:bg-plasma aria-[current=page]:text-white aria-[current=page]:hover:bg-plasma-deep"
          >
            {label}
          </Link>
        ))}
      </nav>

      {user && (
        <div className="flex min-w-0 items-center gap-4">
          <span className="hidden max-w-64 truncate text-sm text-white sm:inline">{user.email}</span>
          <Button variant="hud" className="hud-cut shrink-0 font-hud" onClick={onSignOut}>
            Sign out
          </Button>
        </div>
      )}
    </header>
  );
}
