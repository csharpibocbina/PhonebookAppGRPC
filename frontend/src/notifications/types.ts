/** Matches the C# Notification record pushed by NotificationHub. */
export type NotificationLevel = "success" | "info" | "warning" | "error";

export interface AppNotification {
  id: string;
  type: "ContactCreated" | "ContactUpdated" | "ContactDeleted" | "SignedIn" | "EmailSent" | string;
  title: string;
  message: string;
  level: NotificationLevel;
  createdAt: string;
}
