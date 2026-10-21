import * as DialogPrimitive from "@radix-ui/react-dialog";
import { Trash2, X } from "lucide-react";
import { inspector, useCallLog } from "@/api/inspector";
import { cn } from "@/lib/utils";
import { Button } from "./ui/button";

/**
 * A learning aid: lists every gRPC call the browser made, with its service,
 * method, status code and duration. Open your browser's Network tab next to
 * it to see the matching gRPC-Web requests.
 */
export function GrpcInspector({ open, onOpenChange }: { open: boolean; onOpenChange(open: boolean): void }) {
  const calls = useCallLog();
  return (
    <DialogPrimitive.Root open={open} onOpenChange={onOpenChange}>
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="fixed inset-0 z-40 bg-black/30" />
        <DialogPrimitive.Content className="fixed inset-y-0 right-0 z-50 flex w-full max-w-md flex-col border-l bg-card shadow-2xl focus:outline-none">
          <div className="flex items-center gap-2 border-b p-4">
            <div className="flex-1">
              <DialogPrimitive.Title className="font-semibold">gRPC inspector</DialogPrimitive.Title>
              <DialogPrimitive.Description className="text-xs text-muted-foreground">
                Every call is a POST to /api/&lt;package.Service&gt;/&lt;Method&gt; using gRPC-Web.
              </DialogPrimitive.Description>
            </div>
            <Button variant="ghost" size="icon" onClick={() => inspector.clear()} aria-label="Clear log">
              <Trash2 className="size-4" />
            </Button>
            <DialogPrimitive.Close asChild>
              <Button variant="ghost" size="icon" aria-label="Close inspector">
                <X className="size-4" />
              </Button>
            </DialogPrimitive.Close>
          </div>
          <ol className="flex-1 space-y-2 overflow-y-auto p-4 font-mono text-xs">
            {calls.length === 0 && <li className="font-sans text-sm text-muted-foreground">No calls yet.</li>}
            {calls.map((c) => (
              <li key={c.id} className="rounded-xl border p-3">
                <div className="flex items-center gap-2">
                  <span
                    className={cn(
                      "rounded-md px-1.5 py-0.5 font-semibold",
                      c.code === "OK" ? "bg-emerald-500/15 text-emerald-600 dark:text-emerald-400" : "bg-rose-500/15 text-rose-600 dark:text-rose-400",
                    )}
                  >
                    {c.code}
                  </span>
                  <span className="truncate font-semibold">{c.method}</span>
                  <span className="ml-auto text-muted-foreground">{c.durationMs} ms</span>
                </div>
                <p className="mt-1 truncate text-muted-foreground">
                  /{c.service}/{c.method}
                </p>
                <p className="mt-0.5 text-muted-foreground/70">{c.at.toLocaleTimeString()}</p>
              </li>
            ))}
          </ol>
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}
