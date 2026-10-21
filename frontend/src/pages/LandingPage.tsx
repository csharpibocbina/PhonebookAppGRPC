import { Link } from "react-router";
import { ArrowRight, BellRing, Braces, Lock, Search, Star, Zap } from "lucide-react";
import { useAuth } from "@/auth/AuthContext";
import { Logo } from "@/components/Logo";
import { ThemeToggle } from "@/components/ThemeToggle";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";

const features = [
  { icon: Zap, title: "Fast gRPC calls", text: "Compact binary protobuf messages over gRPC-Web, typed end to end from one .proto contract." },
  { icon: Lock, title: "Secure sign-in", text: "Short-lived JWT access tokens with rotating refresh tokens kept in an httpOnly cookie." },
  { icon: BellRing, title: "Live notifications", text: "SignalR pushes a toast to every open tab the moment a contact changes." },
  { icon: Search, title: "Search & favorites", text: "Find anyone by name, phone or email, sort your list and star the people who matter." },
];

const steps = [
  { label: "React", detail: "phonebookClient.createContact({...})" },
  { label: "gRPC-Web", detail: "POST /phonebook.v1.PhonebookService/CreateContact" },
  { label: ".NET 10", detail: "PhonebookGrpcService.CreateContact()" },
  { label: "SignalR", detail: "Notify → toast in every tab" },
];

export function LandingPage() {
  const { status } = useAuth();
  const signedIn = status === "authenticated";

  return (
    <div className="bg-aurora min-h-dvh">
      <header className="mx-auto flex h-16 max-w-6xl items-center gap-2 px-4">
        <Logo />
        <div className="flex-1" />
        <ThemeToggle />
        {signedIn ? (
          <Link to="/contacts">
            <Button size="sm">Open phonebook</Button>
          </Link>
        ) : (
          <>
            <Link to="/login">
              <Button variant="ghost" size="sm">Sign in</Button>
            </Link>
            <Link to="/register" className="hidden sm:block">
              <Button size="sm">Get started</Button>
            </Link>
          </>
        )}
      </header>

      <main className="mx-auto max-w-6xl px-4">
        <section className="animate-fade-up py-16 text-center sm:py-24">
          <span className="inline-flex items-center gap-2 rounded-full border bg-card/70 px-3 py-1 text-xs font-medium text-muted-foreground backdrop-blur">
            <Braces className="size-3.5 text-primary" /> Built with gRPC · .NET 10 · React
          </span>
          <h1 className="mx-auto mt-6 max-w-3xl text-4xl font-extrabold tracking-tight text-balance sm:text-6xl">
            Your contacts,{" "}
            <span className="bg-gradient-to-r from-indigo-500 via-violet-500 to-fuchsia-500 bg-clip-text text-transparent">beautifully organized</span>
          </h1>
          <p className="mx-auto mt-5 max-w-xl text-lg text-pretty text-muted-foreground">
            A modern phonebook to add, edit and find people in seconds, and a hands-on way to see how gRPC really works.
          </p>
          <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row">
            <Link to={signedIn ? "/contacts" : "/register"}>
              <Button size="lg" className="w-full sm:w-auto">
                {signedIn ? "Go to my contacts" : "Create free account"} <ArrowRight className="size-4" />
              </Button>
            </Link>
            {!signedIn && (
              <Link to="/login">
                <Button size="lg" variant="outline" className="w-full sm:w-auto">
                  Try the demo account
                </Button>
              </Link>
            )}
          </div>
        </section>

        <section aria-labelledby="features" className="pb-16">
          <h2 id="features" className="sr-only">Features</h2>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {features.map(({ icon: Icon, title, text }, i) => (
              <Card key={title} className="animate-fade-up bg-card/70 p-5 backdrop-blur" style={{ animationDelay: `${100 + i * 80}ms` }}>
                <div className="grid size-11 place-items-center rounded-xl bg-gradient-to-br from-indigo-500 to-violet-500 text-white shadow-lg shadow-indigo-500/25">
                  <Icon className="size-5" />
                </div>
                <h3 className="mt-4 font-semibold">{title}</h3>
                <p className="mt-1.5 text-sm text-muted-foreground">{text}</p>
              </Card>
            ))}
          </div>
        </section>

        <section aria-labelledby="how" className="pb-24">
          <Card className="bg-card/70 p-6 backdrop-blur sm:p-8">
            <h2 id="how" className="text-xl font-bold">How one click travels</h2>
            <p className="mt-1 text-sm text-muted-foreground">Adding a contact, from button to toast:</p>
            <ol className="mt-6 grid gap-3 md:grid-cols-4">
              {steps.map((s, i) => (
                <li key={s.label} className="relative rounded-xl border bg-background/60 p-4">
                  <span className="text-xs font-semibold text-primary">Step {i + 1}</span>
                  <p className="mt-1 font-semibold">{s.label}</p>
                  <p className="mt-1 font-mono text-xs break-words text-muted-foreground">{s.detail}</p>
                </li>
              ))}
            </ol>
          </Card>
        </section>
      </main>

      <footer className="border-t py-6 text-center text-sm text-muted-foreground">
        <Star className="mr-1 inline size-3.5 fill-amber-400 text-amber-400" /> Phonebook · API docs at{" "}
        <a className="font-medium text-primary hover:underline" href="/scalar">/scalar</a>
      </footer>
    </div>
  );
}
