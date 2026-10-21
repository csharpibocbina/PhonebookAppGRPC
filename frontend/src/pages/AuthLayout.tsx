import type { ReactNode } from "react";
import { Logo } from "@/components/Logo";
import { ThemeToggle } from "@/components/ThemeToggle";
import { Card } from "@/components/ui/card";

export function AuthLayout({ title, subtitle, children, footer }: { title: string; subtitle: string; children: ReactNode; footer: ReactNode }) {
  return (
    <div className="bg-aurora flex min-h-dvh flex-col">
      <header className="mx-auto flex h-16 w-full max-w-6xl items-center px-4">
        <Logo />
        <div className="flex-1" />
        <ThemeToggle />
      </header>
      <main className="grid flex-1 place-items-center px-4 pb-16">
        <Card className="animate-fade-up w-full max-w-md bg-card/80 p-6 shadow-2xl shadow-indigo-500/10 backdrop-blur-xl sm:p-8">
          <h1 className="text-2xl font-bold tracking-tight">{title}</h1>
          <p className="mt-1 text-sm text-muted-foreground">{subtitle}</p>
          <div className="mt-6">{children}</div>
          <div className="mt-6 text-center text-sm text-muted-foreground">{footer}</div>
        </Card>
      </main>
    </div>
  );
}
