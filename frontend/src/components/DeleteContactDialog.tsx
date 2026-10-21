import { errorMessage } from "@/api/errors";
import type { Contact } from "@/gen/phonebook/v1/phonebook_pb";
import { useDeleteContact } from "@/hooks/useContacts";
import { showError } from "@/notifications/notify";
import { Button } from "./ui/button";
import { Dialog } from "./ui/dialog";

export function DeleteContactDialog({ contact, onClose }: { contact: Contact | null; onClose(): void }) {
  const remove = useDeleteContact();
  const name = contact ? `${contact.firstName} ${contact.lastName}`.trim() : "";

  async function confirm() {
    if (!contact) return;
    try {
      await remove.mutateAsync(contact.id);
      onClose();
    } catch (err) {
      showError(errorMessage(err));
    }
  }

  return (
    <Dialog
      open={!!contact}
      onOpenChange={(open) => !open && onClose()}
      title="Delete contact?"
      description={`${name} will be removed from your phonebook. This can't be undone.`}
    >
      <div className="flex justify-end gap-2">
        <Button variant="ghost" onClick={onClose}>
          Cancel
        </Button>
        <Button variant="danger" onClick={confirm} loading={remove.isPending}>
          Delete
        </Button>
      </div>
    </Dialog>
  );
}
