import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Star } from "lucide-react";
import { errorMessage, getFieldErrors } from "@/api/errors";
import type { Contact } from "@/gen/phonebook/v1/phonebook_pb";
import { toFormValues, useCreateContact, useUpdateContact, type ContactFormValues } from "@/hooks/useContacts";
import { showError } from "@/notifications/notify";
import { cn } from "@/lib/utils";
import { Button } from "./ui/button";
import { Dialog } from "./ui/dialog";
import { FieldError, Input, Label, Textarea } from "./ui/input";

// Mirrors the server's FluentValidation rules so users get instant feedback.
// The server still validates: its INVALID_ARGUMENT field errors are shown too.
export const contactSchema = z.object({
  firstName: z.string().trim().min(1, "First name is required.").max(100),
  lastName: z.string().trim().max(100),
  phoneNumber: z
    .string()
    .trim()
    .min(1, "Phone number is required.")
    .regex(/^\+?[0-9\s\-().]+$/, "Use digits, spaces, dashes, dots, brackets and a leading +.")
    .refine((v) => {
      const digits = v.replace(/\D/g, "").length;
      return digits >= 6 && digits <= 15;
    }, "Phone number must contain 6 to 15 digits."),
  email: z.union([z.literal(""), z.email("Enter a valid email address.")]),
  address: z.string().max(300),
  notes: z.string().max(1000),
  isFavorite: z.boolean(),
});

interface Props {
  open: boolean;
  onOpenChange(open: boolean): void;
  contact?: Contact;
}

export function ContactFormDialog({ open, onOpenChange, contact }: Props) {
  const isEdit = !!contact;
  const create = useCreateContact();
  const update = useUpdateContact();
  const {
    register,
    handleSubmit,
    reset,
    setError,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<ContactFormValues>({ resolver: zodResolver(contactSchema), defaultValues: toFormValues(contact) });

  useEffect(() => {
    if (open) reset(toFormValues(contact));
  }, [open, contact, reset]);

  const onSubmit = handleSubmit(async (values) => {
    try {
      if (contact) await update.mutateAsync({ id: contact.id, values });
      else await create.mutateAsync(values);
      // No success toast here: the server pushes one over SignalR.
      onOpenChange(false);
    } catch (err) {
      const fieldErrors = getFieldErrors(err);
      const entries = Object.entries(fieldErrors);
      if (entries.length) entries.forEach(([field, msgs]) => setError(field as keyof ContactFormValues, { message: msgs[0] }));
      else showError(errorMessage(err));
    }
  });

  const favorite = watch("isFavorite");
  const field = (name: keyof ContactFormValues) => ({
    id: `contact-${name}`,
    "aria-invalid": errors[name] ? true : undefined,
    "aria-describedby": errors[name] ? `contact-${name}-error` : undefined,
  });

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={isEdit ? "Edit contact" : "Add contact"}
      description={isEdit ? "Update the details and save." : "Fill in the details. Only first name and phone are required."}
    >
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <Label htmlFor="contact-firstName">First name *</Label>
            <Input {...register("firstName")} {...field("firstName")} autoFocus placeholder="Maria" />
            <FieldError id="contact-firstName-error" message={errors.firstName?.message} />
          </div>
          <div>
            <Label htmlFor="contact-lastName">Last name</Label>
            <Input {...register("lastName")} {...field("lastName")} placeholder="Santos" />
            <FieldError id="contact-lastName-error" message={errors.lastName?.message} />
          </div>
        </div>
        <div>
          <Label htmlFor="contact-phoneNumber">Phone number *</Label>
          <Input {...register("phoneNumber")} {...field("phoneNumber")} type="tel" inputMode="tel" placeholder="+63 917 555 0101" />
          <FieldError id="contact-phoneNumber-error" message={errors.phoneNumber?.message} />
        </div>
        <div>
          <Label htmlFor="contact-email">Email</Label>
          <Input {...register("email")} {...field("email")} type="email" placeholder="maria@example.com" />
          <FieldError id="contact-email-error" message={errors.email?.message} />
        </div>
        <div>
          <Label htmlFor="contact-address">Address</Label>
          <Input {...register("address")} {...field("address")} placeholder="Makati City, Metro Manila" />
          <FieldError id="contact-address-error" message={errors.address?.message} />
        </div>
        <div>
          <Label htmlFor="contact-notes">Notes</Label>
          <Textarea {...register("notes")} {...field("notes")} placeholder="Anything worth remembering" />
          <FieldError id="contact-notes-error" message={errors.notes?.message} />
        </div>
        <button
          type="button"
          onClick={() => setValue("isFavorite", !favorite)}
          aria-pressed={favorite}
          className={cn(
            "flex w-full items-center gap-2 rounded-xl border px-3.5 py-2.5 text-sm transition",
            favorite ? "border-amber-400/60 bg-amber-400/10" : "hover:bg-muted",
          )}
        >
          <Star className={cn("size-4", favorite ? "fill-amber-400 text-amber-400" : "text-muted-foreground")} />
          {favorite ? "Marked as favorite" : "Mark as favorite"}
        </button>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button type="submit" loading={isSubmitting}>
            {isEdit ? "Save changes" : "Add contact"}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
