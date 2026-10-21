import { useState } from "react";
import { ChevronLeft, ChevronRight, Plus, Search, Star, Users, X } from "lucide-react";
import { errorMessage } from "@/api/errors";
import { useAuth } from "@/auth/AuthContext";
import { AppHeader } from "@/components/AppHeader";
import { ContactFormDialog } from "@/components/ContactFormDialog";
import { ContactList, ContactListSkeleton } from "@/components/ContactList";
import { DeleteContactDialog } from "@/components/DeleteContactDialog";
import { GrpcInspector } from "@/components/GrpcInspector";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { SortBy, type Contact } from "@/gen/phonebook/v1/phonebook_pb";
import { useContactList, useToggleFavorite } from "@/hooks/useContacts";
import { useDebounce } from "@/hooks/useDebounce";
import { showError } from "@/notifications/notify";
import { useNotificationHub } from "@/notifications/useNotificationHub";
import { cn } from "@/lib/utils";

const PAGE_SIZE = 12;

const sortOptions = [
  { value: SortBy.FIRST_NAME, label: "First name" },
  { value: SortBy.LAST_NAME, label: "Last name" },
  { value: SortBy.CREATED_AT, label: "Newest" },
];

export function ContactsPage() {
  const { user, status } = useAuth();
  const hubState = useNotificationHub(status === "authenticated");

  const [search, setSearch] = useState("");
  const [sortBy, setSortBy] = useState(SortBy.FIRST_NAME);
  const [favoritesOnly, setFavoritesOnly] = useState(false);
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebounce(search);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Contact | undefined>();
  const [deleting, setDeleting] = useState<Contact | null>(null);
  const [inspectorOpen, setInspectorOpen] = useState(false);

  const { data, isPending, isError, error, isFetching, refetch } = useContactList({
    page,
    pageSize: PAGE_SIZE,
    search: debouncedSearch,
    sortBy,
    favoritesOnly,
  });
  const toggleFavorite = useToggleFavorite();

  const total = data?.totalCount ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const firstName = user?.displayName.split(" ")[0] ?? "";

  function openCreate() {
    setEditing(undefined);
    setFormOpen(true);
  }

  return (
    <div className="min-h-dvh">
      <AppHeader hubState={hubState} onOpenInspector={() => setInspectorOpen(true)} />

      <main className="mx-auto max-w-6xl px-4 py-8">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-end">
          <div className="flex-1">
            <h1 className="text-2xl font-bold tracking-tight sm:text-3xl">Hi {firstName} 👋</h1>
            <p className="mt-1 text-muted-foreground">
              {total} {total === 1 ? "contact" : "contacts"}
              {favoritesOnly ? " in favorites" : " in your phonebook"}
            </p>
          </div>
          <Button onClick={openCreate} size="lg" className="sm:w-auto">
            <Plus className="size-5" /> Add contact
          </Button>
        </div>

        <Card className="mt-6 flex flex-col gap-3 p-3 sm:flex-row sm:items-center">
          <div className="relative flex-1">
            <Search className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              placeholder="Search by name, phone or email"
              aria-label="Search contacts"
              className="border-transparent bg-muted/60 pl-10 shadow-none"
            />
            {search && (
              <button
                onClick={() => setSearch("")}
                aria-label="Clear search"
                className="absolute top-1/2 right-2 -translate-y-1/2 rounded-md p-1.5 text-muted-foreground hover:bg-muted"
              >
                <X className="size-4" />
              </button>
            )}
          </div>
          <div className="flex gap-2">
            <Button
              variant={favoritesOnly ? "secondary" : "ghost"}
              onClick={() => {
                setFavoritesOnly((v) => !v);
                setPage(1);
              }}
              aria-pressed={favoritesOnly}
            >
              <Star className={cn("size-4", favoritesOnly && "fill-amber-400 text-amber-400")} /> Favorites
            </Button>
            <select
              value={sortBy}
              onChange={(e) => setSortBy(Number(e.target.value) as SortBy)}
              aria-label="Sort contacts"
              className="h-10 rounded-xl border bg-card px-3 text-sm focus:ring-4 focus:ring-primary/15 focus:outline-none"
            >
              {sortOptions.map((o) => (
                <option key={o.value} value={o.value}>
                  Sort: {o.label}
                </option>
              ))}
            </select>
          </div>
        </Card>

        <section className={cn("mt-6 transition-opacity", isFetching && !isPending && "opacity-70")} aria-busy={isFetching}>
          {isPending ? (
            <ContactListSkeleton />
          ) : isError ? (
            <EmptyState
              title="Couldn't load contacts"
              text={errorMessage(error)}
              action={<Button variant="outline" onClick={() => refetch()}>Try again</Button>}
            />
          ) : data.contacts.length === 0 ? (
            debouncedSearch || favoritesOnly ? (
              <EmptyState title="No matches" text="Try a different search or clear the filters." />
            ) : (
              <EmptyState
                title="Your phonebook is empty"
                text="Add your first contact and watch the live notification pop up."
                action={<Button onClick={openCreate}><Plus className="size-4" /> Add contact</Button>}
              />
            )
          ) : (
            <ContactList
              contacts={data.contacts}
              onEdit={(c) => {
                setEditing(c);
                setFormOpen(true);
              }}
              onDelete={setDeleting}
              onToggleFavorite={(c) => toggleFavorite.mutate(c, { onError: (err) => showError(errorMessage(err)) })}
            />
          )}
        </section>

        {pageCount > 1 && (
          <nav className="mt-8 flex items-center justify-center gap-2" aria-label="Pagination">
            <Button variant="outline" size="icon" disabled={page <= 1} onClick={() => setPage((p) => p - 1)} aria-label="Previous page">
              <ChevronLeft className="size-4" />
            </Button>
            <span className="px-3 text-sm text-muted-foreground">
              Page {page} of {pageCount}
            </span>
            <Button variant="outline" size="icon" disabled={page >= pageCount} onClick={() => setPage((p) => p + 1)} aria-label="Next page">
              <ChevronRight className="size-4" />
            </Button>
          </nav>
        )}
      </main>

      <ContactFormDialog open={formOpen} onOpenChange={setFormOpen} contact={editing} />
      <DeleteContactDialog contact={deleting} onClose={() => setDeleting(null)} />
      <GrpcInspector open={inspectorOpen} onOpenChange={setInspectorOpen} />
    </div>
  );
}

function EmptyState({ title, text, action }: { title: string; text: string; action?: React.ReactNode }) {
  return (
    <Card className="grid place-items-center px-6 py-16 text-center">
      <div className="grid size-14 place-items-center rounded-2xl bg-gradient-to-br from-indigo-500/15 to-violet-500/15 text-primary">
        <Users className="size-7" />
      </div>
      <h2 className="mt-4 text-lg font-semibold">{title}</h2>
      <p className="mt-1 max-w-sm text-sm text-muted-foreground">{text}</p>
      {action && <div className="mt-5">{action}</div>}
    </Card>
  );
}
