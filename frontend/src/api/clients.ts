import { createClient } from "@connectrpc/connect";
import { AuthService } from "@/gen/auth/v1/auth_pb";
import { PhonebookService } from "@/gen/phonebook/v1/phonebook_pb";
import { transport } from "./transport";

// createClient reads the service descriptor generated from the .proto and
// returns an object with one typed async method per rpc, e.g.
//   await phonebookClient.createContact({ firstName: "Ana", phoneNumber: "..." })
export const authClient = createClient(AuthService, transport);
export const phonebookClient = createClient(PhonebookService, transport);
