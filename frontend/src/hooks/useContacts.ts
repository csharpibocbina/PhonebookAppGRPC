import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { phonebookClient } from "@/api/clients";
import { SortBy, type Contact } from "@/gen/phonebook/v1/phonebook_pb";

export interface ContactFilters {
  page: number;
  pageSize: number;
  search: string;
  sortBy: SortBy;
  favoritesOnly: boolean;
}

/** Editable fields, shared by the create and update RPCs. */
export interface ContactFormValues {
  firstName: string;
  lastName: string;
  phoneNumber: string;
  email: string;
  address: string;
  notes: string;
  isFavorite: boolean;
}

// Each hook is a thin wrapper around one gRPC method. TanStack Query adds
// caching, loading states and refetching on top.

export function useContactList(filters: ContactFilters) {
  return useQuery({
    queryKey: ["contacts", filters],
    queryFn: ({ signal }) => phonebookClient.listContacts(filters, { signal }),
    placeholderData: keepPreviousData,
  });
}

export function useCreateContact() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (values: ContactFormValues) => phonebookClient.createContact(values),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["contacts"] }),
  });
}

export function useUpdateContact() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, values }: { id: string; values: ContactFormValues }) => phonebookClient.updateContact({ id, ...values }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["contacts"] }),
  });
}

/** Optimistic favorite toggle: the star flips instantly and rolls back on error. */
export function useToggleFavorite() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (contact: Contact) =>
      phonebookClient.updateContact({ ...toFormValues(contact), id: contact.id, isFavorite: !contact.isFavorite }),
    onMutate: async (contact) => {
      await queryClient.cancelQueries({ queryKey: ["contacts"] });
      const previous = queryClient.getQueriesData({ queryKey: ["contacts"] });
      queryClient.setQueriesData<{ contacts: Contact[] }>({ queryKey: ["contacts"] }, (old) =>
        old ? { ...old, contacts: old.contacts.map((c) => (c.id === contact.id ? { ...c, isFavorite: !c.isFavorite } : c)) } : old,
      );
      return { previous };
    },
    onError: (_err, _contact, ctx) => ctx?.previous.forEach(([key, data]) => queryClient.setQueryData(key, data)),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["contacts"] }),
  });
}

export function useDeleteContact() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => phonebookClient.deleteContact({ id }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["contacts"] }),
  });
}

export function toFormValues(c?: Contact): ContactFormValues {
  return {
    firstName: c?.firstName ?? "",
    lastName: c?.lastName ?? "",
    phoneNumber: c?.phoneNumber ?? "",
    email: c?.email ?? "",
    address: c?.address ?? "",
    notes: c?.notes ?? "",
    isFavorite: c?.isFavorite ?? false,
  };
}
