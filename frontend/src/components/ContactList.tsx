import { Mail, MapPin, Pencil, Phone, Star, Trash2 } from "lucide-react";
import type { Contact } from "@/gen/phonebook/v1/phonebook_pb";
import { avatarGradient, cn, initials } from "@/lib/utils";
import { Button } from "./ui/button";
import { Card, Skeleton } from "./ui/card";

interface Props {
  contacts: Contact[];
  onEdit(contact: Contact): void;
  onDelete(contact: Contact): void;
  onToggleFavorite(contact: Contact): void;
}

export function ContactList({ contacts, onEdit, onDelete, onToggleFavorite }: Props) {
  return (
    <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3" aria-label="Contacts">
      {contacts.map((c, i) => {
        const name = `${c.firstName} ${c.lastName}`.trim();
        return (
          <li key={c.id} className="animate-fade-up" style={{ animationDelay: `${Math.min(i, 12) * 30}ms` }}>
            <Card className="group flex h-full flex-col p-4 transition hover:-translate-y-0.5 hover:shadow-lg hover:shadow-indigo-500/5">
              <div className="flex items-start gap-3">
                <div
                  className={cn(
                    "grid size-12 shrink-0 place-items-center rounded-2xl bg-gradient-to-br text-sm font-semibold text-white",
                    avatarGradient(name),
                  )}
                  aria-hidden
                >
                  {initials(c.firstName, c.lastName)}
                </div>
                <div className="min-w-0 flex-1">
                  <p className="truncate font-semibold">{name}</p>
                  <a href={`tel:${c.phoneNumber}`} className="mt-0.5 flex items-center gap-1.5 text-sm text-muted-foreground hover:text-primary">
                    <Phone className="size-3.5" aria-hidden /> {c.phoneNumber}
                  </a>
                </div>
                <button
                  type="button"
                  onClick={() => onToggleFavorite(c)}
                  aria-label={c.isFavorite ? `Remove ${name} from favorites` : `Add ${name} to favorites`}
                  aria-pressed={c.isFavorite}
                  className="rounded-lg p-1.5 hover:bg-muted"
                >
                  <Star className={cn("size-5 transition", c.isFavorite ? "fill-amber-400 text-amber-400" : "text-muted-foreground/50")} />
                </button>
              </div>
              <div className="mt-3 space-y-1 text-sm text-muted-foreground">
                {c.email && (
                  <a href={`mailto:${c.email}`} className="flex items-center gap-1.5 truncate hover:text-primary">
                    <Mail className="size-3.5 shrink-0" aria-hidden /> <span className="truncate">{c.email}</span>
                  </a>
                )}
                {c.address && (
                  <p className="flex items-center gap-1.5 truncate">
                    <MapPin className="size-3.5 shrink-0" aria-hidden /> <span className="truncate">{c.address}</span>
                  </p>
                )}
                {c.notes && <p className="line-clamp-2 pt-1 text-xs">{c.notes}</p>}
              </div>
              <div className="mt-auto flex justify-end gap-1 pt-3 opacity-100 transition sm:opacity-0 sm:group-focus-within:opacity-100 sm:group-hover:opacity-100">
                <Button variant="ghost" size="sm" onClick={() => onEdit(c)} aria-label={`Edit ${name}`}>
                  <Pencil className="size-4" /> Edit
                </Button>
                <Button variant="ghost" size="sm" className="text-danger" onClick={() => onDelete(c)} aria-label={`Delete ${name}`}>
                  <Trash2 className="size-4" /> Delete
                </Button>
              </div>
            </Card>
          </li>
        );
      })}
    </ul>
  );
}

export function ContactListSkeleton() {
  return (
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3" aria-hidden>
      {Array.from({ length: 6 }, (_, i) => (
        <Card key={i} className="p-4">
          <div className="flex gap-3">
            <Skeleton className="size-12 rounded-2xl" />
            <div className="flex-1 space-y-2 pt-1">
              <Skeleton className="h-4 w-2/3" />
              <Skeleton className="h-3 w-1/2" />
            </div>
          </div>
          <Skeleton className="mt-4 h-3 w-3/4" />
        </Card>
      ))}
    </div>
  );
}
