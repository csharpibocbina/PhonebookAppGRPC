import { Link } from "react-router";
import { cn } from "@/lib/utils";

export function Logo({ className, to = "/" }: { className?: string; to?: string }) {
  return (
    <Link to={to} className={cn("inline-flex items-center gap-2.5 font-bold tracking-tight", className)}>
      <img src="/favicon.svg" alt="" className="size-8" />
      <span className="text-lg">Phonebook</span>
    </Link>
  );
}
