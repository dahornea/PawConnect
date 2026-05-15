using System.Globalization;
using System.Text;
using PawConnect.Entities;

namespace PawConnect.Services;

public static class VisitSchedulingHelper
{
    private const string VisitCalendarTimeZoneId = "Europe/Bucharest";

    public static readonly TimeSpan DefaultVisitStartTime = new(10, 0, 0);
    public static readonly TimeSpan DefaultVisitEndTime = new(17, 0, 0);

    private static readonly DayOfWeek[] Weekdays =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday
    ];

    public static TimeSpan GetVisitStartTime(Shelter? shelter)
    {
        return shelter?.VisitStartTime ?? DefaultVisitStartTime;
    }

    public static TimeSpan GetVisitEndTime(Shelter? shelter)
    {
        return shelter?.VisitEndTime ?? DefaultVisitEndTime;
    }

    public static bool IsVisitDayAllowed(Shelter? shelter, DayOfWeek day)
    {
        if (shelter is null || !HasAnyConfiguredDay(shelter))
        {
            return Weekdays.Contains(day);
        }

        return day switch
        {
            DayOfWeek.Monday => shelter.VisitsAllowedMonday,
            DayOfWeek.Tuesday => shelter.VisitsAllowedTuesday,
            DayOfWeek.Wednesday => shelter.VisitsAllowedWednesday,
            DayOfWeek.Thursday => shelter.VisitsAllowedThursday,
            DayOfWeek.Friday => shelter.VisitsAllowedFriday,
            DayOfWeek.Saturday => shelter.VisitsAllowedSaturday,
            DayOfWeek.Sunday => shelter.VisitsAllowedSunday,
            _ => false
        };
    }

    public static string FormatVisitingHours(Shelter? shelter)
    {
        var start = GetVisitStartTime(shelter);
        var end = GetVisitEndTime(shelter);
        var days = GetAllowedDayLabels(shelter);
        return $"{days}: {FormatTime(start)}-{FormatTime(end)}";
    }

    public static void ApplyDefaultVisitingHours(Shelter shelter)
    {
        shelter.VisitStartTime ??= DefaultVisitStartTime;
        shelter.VisitEndTime ??= DefaultVisitEndTime;

        if (HasAnyConfiguredDay(shelter))
        {
            return;
        }

        shelter.VisitsAllowedMonday = true;
        shelter.VisitsAllowedTuesday = true;
        shelter.VisitsAllowedWednesday = true;
        shelter.VisitsAllowedThursday = true;
        shelter.VisitsAllowedFriday = true;
    }

    public static void ValidatePreferredVisitTime(Shelter? shelter, DateTime? preferredVisitDateTime)
    {
        if (!preferredVisitDateTime.HasValue)
        {
            throw new InvalidOperationException("Preferred visit time is required.");
        }

        var localVisitTime = DateTime.SpecifyKind(preferredVisitDateTime.Value, DateTimeKind.Unspecified);
        if (localVisitTime <= DateTime.Now)
        {
            throw new InvalidOperationException("Please choose a future visit time.");
        }

        if (!IsVisitDayAllowed(shelter, localVisitTime.DayOfWeek))
        {
            throw new InvalidOperationException("This shelter is closed for visits on the selected day.");
        }

        var start = GetVisitStartTime(shelter);
        var end = GetVisitEndTime(shelter);
        if (start >= end)
        {
            throw new InvalidOperationException("Shelter visiting hours are not configured correctly.");
        }

        var visitTime = localVisitTime.TimeOfDay;
        if (visitTime < start || visitTime >= end)
        {
            throw new InvalidOperationException("Please choose a time within the shelter's visiting hours.");
        }
    }

    public static string FormatVisitDateTime(DateTime? visitDateTime)
    {
        return visitDateTime.HasValue
            ? visitDateTime.Value.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)
            : "No visit time selected";
    }

    public static EmailAttachment CreateCalendarInviteAttachment(AdoptionRequest request)
    {
        if (!request.PreferredVisitDateTime.HasValue)
        {
            throw new InvalidOperationException("A visit time is required before creating a calendar invite.");
        }

        var dogName = request.Dog?.Name ?? "Dog";
        var shelterName = request.Dog?.Shelter?.Name ?? "Shelter";
        var shelter = request.Dog?.Shelter;
        var adopterName = string.IsNullOrWhiteSpace(request.Adopter?.FullName)
            ? request.Adopter?.Email
            : request.Adopter.FullName.Trim();
        var startLocal = DateTime.SpecifyKind(request.PreferredVisitDateTime.Value, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddHours(1);
        var fileDate = request.PreferredVisitDateTime.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return new EmailAttachment
        {
            FileName = $"adoption-visit-{SanitizeFileName(dogName)}-{fileDate}.ics",
            ContentType = "text/calendar",
            IsCalendarInvite = true,
            CalendarMethod = "REQUEST",
            IncludeAsAttachmentFallback = true,
            Content = Encoding.UTF8.GetBytes(BuildCalendarInvite(
                request.Id,
                dogName,
                shelterName,
                BuildLocation(shelter),
                BuildDescription(dogName, shelterName, shelter),
                shelter?.Email,
                shelterName,
                request.Adopter?.Email,
                adopterName,
                startLocal,
                endLocal))
        };
    }

    private static string BuildCalendarInvite(
        int requestId,
        string dogName,
        string shelterName,
        string location,
        string description,
        string? organizerEmail,
        string? organizerName,
        string? attendeeEmail,
        string? attendeeName,
        DateTime startLocal,
        DateTime endLocal)
    {
        var nowUtc = DateTime.UtcNow;
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//PawConnect//Adoption Visit//EN",
            "CALSCALE:GREGORIAN",
            "METHOD:REQUEST",
            BuildBucharestTimeZone(),
            "BEGIN:VEVENT",
            $"UID:pawconnect-adoption-visit-{requestId}@pawconnect.local",
            $"DTSTAMP:{FormatIcsDateTime(nowUtc)}",
            $"DTSTART;TZID={VisitCalendarTimeZoneId}:{FormatIcsLocalDateTime(startLocal)}",
            $"DTEND;TZID={VisitCalendarTimeZoneId}:{FormatIcsLocalDateTime(endLocal)}",
            $"SUMMARY:{EscapeIcsText($"Visit {dogName} at {shelterName}")}",
            $"LOCATION:{EscapeIcsText(location)}",
            $"DESCRIPTION:{EscapeIcsText(description)}",
            "STATUS:CONFIRMED"
        };

        if (!string.IsNullOrWhiteSpace(organizerEmail))
        {
            lines.Add($"ORGANIZER;CN={EscapeIcsParameter(organizerName ?? shelterName)}:MAILTO:{organizerEmail.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(attendeeEmail))
        {
            lines.Add($"ATTENDEE;CN={EscapeIcsParameter(attendeeName ?? attendeeEmail)};ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=TRUE:MAILTO:{attendeeEmail.Trim()}");
        }

        lines.AddRange(
        [
            "END:VEVENT",
            "END:VCALENDAR"
        ]);

        return string.Join("\r\n", lines) + "\r\n";
    }

    private static string BuildBucharestTimeZone()
    {
        return string.Join("\r\n",
        [
            "BEGIN:VTIMEZONE",
            $"TZID:{VisitCalendarTimeZoneId}",
            "X-LIC-LOCATION:Europe/Bucharest",
            "BEGIN:DAYLIGHT",
            "TZOFFSETFROM:+0200",
            "TZOFFSETTO:+0300",
            "TZNAME:EEST",
            "DTSTART:19700329T030000",
            "RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=-1SU",
            "END:DAYLIGHT",
            "BEGIN:STANDARD",
            "TZOFFSETFROM:+0300",
            "TZOFFSETTO:+0200",
            "TZNAME:EET",
            "DTSTART:19701025T040000",
            "RRULE:FREQ=YEARLY;BYMONTH=10;BYDAY=-1SU",
            "END:STANDARD",
            "END:VTIMEZONE"
        ]);
    }

    private static bool HasAnyConfiguredDay(Shelter shelter)
    {
        return shelter.VisitsAllowedMonday ||
               shelter.VisitsAllowedTuesday ||
               shelter.VisitsAllowedWednesday ||
               shelter.VisitsAllowedThursday ||
               shelter.VisitsAllowedFriday ||
               shelter.VisitsAllowedSaturday ||
               shelter.VisitsAllowedSunday;
    }

    private static string GetAllowedDayLabels(Shelter? shelter)
    {
        var allowedDays = Enum.GetValues<DayOfWeek>()
            .Where(day => IsVisitDayAllowed(shelter, day))
            .OrderBy(day => day == DayOfWeek.Sunday ? 7 : (int)day)
            .Select(day => day.ToString()[..3])
            .ToList();

        return allowedDays.Count == 0 ? "No visiting days" : string.Join(", ", allowedDays);
    }

    private static string FormatTime(TimeSpan time)
    {
        return DateTime.Today.Add(time).ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private static string BuildLocation(Shelter? shelter)
    {
        return string.Join(", ", new[] { shelter?.Address, shelter?.City }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string BuildDescription(string dogName, string shelterName, Shelter? shelter)
    {
        var lines = new List<string>
        {
            $"Adoption visit for {dogName} at {shelterName}.",
            string.Empty,
            "If you cannot attend, please contact the shelter:"
        };

        if (!string.IsNullOrWhiteSpace(shelter?.Email))
        {
            lines.Add($"Email: {shelter.Email.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(shelter?.PhoneNumber))
        {
            lines.Add($"Phone: {shelter.PhoneNumber.Trim()}");
        }

        return string.Join("\n", lines);
    }

    private static string FormatIcsDateTime(DateTime value)
    {
        return value.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
    }

    private static string FormatIcsLocalDateTime(DateTime value)
    {
        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified)
            .ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
    }

    private static string EscapeIcsText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal);
    }

    private static string EscapeIcsParameter(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace(":", "\\:", StringComparison.Ordinal);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : char.ToLowerInvariant(ch))
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized)
            ? "dog"
            : sanitized.Trim('-');
    }
}
