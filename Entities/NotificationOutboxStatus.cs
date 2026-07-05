namespace PawConnect.Entities;

public enum NotificationOutboxStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3,
    DeadLetter = 4,
    Cancelled = 5
}
