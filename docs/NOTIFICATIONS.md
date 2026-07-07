# PawConnect Notifications

PawConnect uses local, database-backed notifications for important in-app updates. The notification system does not require a paid push/SMS/email provider to work locally.

## Notification Center

The user-facing Notification Center is available at:

- `/notifications`

It shows notifications for the currently signed-in user only. The page supports:

- unread, read, and all filters;
- category filters based on the notification categories that actually exist for the user;
- search by title, message, or related entity name;
- grouping by time: Today, Yesterday, This Week, and Older;
- mark as read;
- mark as unread;
- mark all as read;
- dismiss notification;
- safe related links when the notification has an internal PawConnect URL.

The topbar notification bell uses the same display model and shows a small preview of recent notifications plus the unread count.

## Privacy And Scoping

Notifications are scoped by `Notification.UserId`.

Important rules:

- users only load notifications where `UserId` equals their own account id;
- marking as read/unread checks the same user id;
- dismissing a notification checks the same user id;
- external URLs are not shown as related actions in the Notification Center;
- admin/system delivery failures remain separate in the admin notification delivery/outbox pages.

This means UI hiding is not the only protection. The service methods also filter and update by the current user id.

## Notification Categories

Current supported categories come from `NotificationCategory`:

- Adoption
- Shelter Applications
- Resources
- Reports
- System
- Transfers
- Volunteer Tasks
- Saved Searches

The center only displays category filters for categories that appear in the current user's notifications.

## Read, Unread, And Dismiss

Read state is stored on the existing `Notification` entity:

- `IsRead`
- `ReadAt`

Marking a notification as unread sets `IsRead = false` and clears `ReadAt`.

Dismiss currently removes the notification record for that user. There is no separate archive table or soft-delete field yet.

## Preferences

Notification preferences are managed at:

- `/notification-preferences`

Preferences are handled by `NotificationPreferenceService`. When a notification is created, `NotificationService` checks whether the in-app channel is enabled for the mapped notification event type.

If in-app notifications are disabled for a type, the notification is not inserted into the user's Notification Center.

## Outbox Relationship

The Notification Center shows in-app notifications from the `Notifications` table.

Email delivery uses the existing notification outbox infrastructure:

- `NotificationOutboxMessage`
- `NotificationOutboxService`
- `NotificationOutboxProcessor`
- admin pages for delivery/outbox review

The Notification Center does not expose raw outbox payloads or internal delivery errors to normal users.

## Main Code Files

- `Entities/Notification.cs`
- `Entities/NotificationCategory.cs`
- `Services/NotificationService.cs`
- `Services/NotificationCenterService.cs`
- `Services/NotificationCenterModels.cs`
- `Components/Pages/Notifications.razor`
- `Components/Shared/NotificationBell.razor`
- `Components/Pages/NotificationPreferences.razor`
- `Components/Pages/Admin/AdminNotificationOutbox.razor`
- `Components/Pages/Admin/AdminNotificationDelivery.razor`

## Local-Only Note

This card does not add paid notification providers, mobile push notifications, SMS, or external notification SaaS. Everything needed for the in-app notification center is stored and read locally through the application database.
