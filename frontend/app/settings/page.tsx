import type { Metadata } from "next";
import { Settings } from "./Settings";

export const metadata: Metadata = { title: "Settings · Zhadina Budget" };

export default function SettingsPage() {
  return <Settings />;
}
