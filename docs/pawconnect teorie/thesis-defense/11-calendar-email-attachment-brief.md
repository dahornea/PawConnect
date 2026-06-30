# Calendar Email Attachment Thesis Brief

This document briefly explains how PawConnect adds calendar invitations to adoption visit confirmation emails. It is intended as a small source brief that can be given to ChatGPT or used directly when writing a bachelor thesis subsection.

## Suggested Prompt To Give ChatGPT

Use the technical notes below to write a short bachelor thesis paragraph or subsection about how PawConnect sends calendar attachments for confirmed shelter visits.

Requirements:

- Write in clear academic English.
- Explain that the calendar invite is generated only after the shelter confirms a visit.
- Mention the actual files/classes involved.
- Explain `.ics` / iCalendar briefly.
- Explain that the email is still sent through the normal email service.
- Do not claim that PawConnect integrates directly with Google Calendar, Outlook Calendar, or an external calendar API.
- Do not claim that the calendar event is automatically inserted into the user's calendar. It is sent as an invite/attachment that the email client can interpret.

Suggested title:

`Calendar Invitations for Confirmed Shelter Visits`

## Feature Summary

When a shelter confirms an adoption visit, PawConnect sends an email to the adopter. That email includes a calendar invitation file in iCalendar format (`.ics`). This allows common email clients to show an "Add to calendar" or meeting-invitation style action.

The calendar invite contains information about:

- the dog
- the shelter
- the visit date and time
- the shelter location
- shelter contact details
- organizer and attendee email information when available

## Main Files and Classes

| File | Role |
| --- | --- |
| `Services/AdoptionRequestService.cs` | Triggers the visit confirmation email after the shelter confirms a visit. |
| `Services/VisitSchedulingHelper.cs` | Validates visit times, formats visit dates, and creates the `.ics` calendar invite attachment. |
| `Services/EmailAttachment.cs` | Represents normal attachments and calendar invite attachments. |
| `Services/EmailMimeBuilder.cs` | Builds the MIME email body and inserts calendar invite parts correctly. |
| `Services/SmtpEmailService.cs` | Sends the final email through SMTP using MailKit/MimeKit. |
| `PawConnect.Tests/Tests/EmailMimeBuilderTests.cs` | Tests that calendar invites are included both inline and as an attachment fallback. |

## When the Calendar Attachment Is Created

The calendar invite is created when the shelter confirms a visit.

Main method:

- `AdoptionRequestService.ConfirmVisitAsync(...)`

After the request is confirmed, the service calls:

- `NotifyAdopterAboutVisitConfirmedAsync(request)`

Inside that method, the attachment is created with:

- `VisitSchedulingHelper.CreateCalendarInviteAttachment(request)`

The email is then sent with:

- `emailService.SendEmailAsync(...)`

## Visit Confirmation Flow

1. The adopter submits an adoption request and chooses a preferred visit date/time.
2. The shelter reviews the request on the shelter adoption requests page.
3. The shelter confirms the visit.
4. `AdoptionRequestService.ConfirmVisitAsync` changes:
   - request status to `VisitConfirmed`
   - visit status to `Confirmed`
   - dog status to `Reserved`
5. PawConnect creates a notification and sends a confirmation email to the adopter.
6. The email includes an `.ics` calendar invitation.

Important related files:

- `Components/Pages/Shelter/ShelterAdoptionRequests.razor`
- `Services/AdoptionRequestService.cs`
- `Entities/AdoptionRequest.cs`
- `Entities/AdoptionVisitStatus.cs`
- `Entities/AdoptionRequestStatus.cs`

## How the `.ics` Invite Is Built

File:

- `Services/VisitSchedulingHelper.cs`

Method:

- `CreateCalendarInviteAttachment(AdoptionRequest request)`

The method returns an `EmailAttachment` with:

- `FileName`: generated from dog name and visit date, for example `adoption-visit-bella-2026-05-20.ics`
- `ContentType`: `text/calendar`
- `IsCalendarInvite`: `true`
- `CalendarMethod`: `REQUEST`
- `IncludeAsAttachmentFallback`: `true`
- `Content`: UTF-8 bytes containing the iCalendar event

The invite content includes standard iCalendar sections such as:

- `BEGIN:VCALENDAR`
- `VERSION:2.0`
- `METHOD:REQUEST`
- `BEGIN:VEVENT`
- `UID`
- `DTSTAMP`
- `DTSTART`
- `DTEND`
- `SUMMARY`
- `LOCATION`
- `DESCRIPTION`
- `STATUS:CONFIRMED`
- `ORGANIZER`
- `ATTENDEE`
- `END:VEVENT`
- `END:VCALENDAR`

## Time Zone Handling

The calendar invite uses the timezone:

- `Europe/Bucharest`

This is defined in:

- `VisitSchedulingHelper.VisitCalendarTimeZoneId`

The helper includes a `VTIMEZONE` block for Bucharest. This helps calendar clients interpret the visit time correctly, including daylight saving rules.

The visit duration is currently generated as:

- start time: preferred visit date/time
- end time: one hour after the start time

## Email MIME Structure

File:

- `Services/EmailMimeBuilder.cs`

The email builder treats calendar invites differently from normal PDF attachments.

For calendar invites:

1. It adds an inline `text/calendar` part inside the alternative email body.
2. If `IncludeAsAttachmentFallback` is true, it also adds the same calendar data as a downloadable `.ics` attachment.

This is useful because different email clients handle calendar invites differently. Some clients detect the inline calendar part as a meeting invitation, while others expose the `.ics` file as an attachment.

## Sending the Email

File:

- `Services/SmtpEmailService.cs`

The final email is sent through:

- `IEmailService.SendEmailAsync(...)`

Implementation:

- `SmtpEmailService`

Libraries:

- MailKit
- MimeKit

If email settings are incomplete or sending fails, the service logs a warning. It does not crash the main adoption workflow.

## Test Coverage

Test file:

- `PawConnect.Tests/Tests/EmailMimeBuilderTests.cs`

Important test:

- `BuildBody_AddsCalendarInvitePartAndAttachmentFallback`

This test verifies that a calendar invite is included:

- once as an inline `text/calendar` part
- once as an attachment fallback with the `.ics` filename

Another test:

- `BuildBody_PreservesPdfAttachmentBehavior`

This verifies that normal PDF attachments still behave correctly and are not treated as calendar invites.

## Short Thesis-Style Explanation

PawConnect improves the adoption visit workflow by attaching an iCalendar invitation to the email sent after a shelter confirms a visit. The visit confirmation is handled in `AdoptionRequestService.ConfirmVisitAsync`, which updates the request and visit status, reserves the dog, and calls the adopter notification method. The calendar attachment itself is generated by `VisitSchedulingHelper.CreateCalendarInviteAttachment`, which builds a standard `.ics` file containing the dog name, shelter name, location, visit start and end time, and attendee/organizer information when available. The invite uses the `Europe/Bucharest` timezone and is marked as a calendar `REQUEST`.

The email body is built by `EmailMimeBuilder`, which adds the calendar invite as an inline `text/calendar` MIME part and also as a fallback `.ics` attachment. This design improves compatibility with different email clients: some clients can display the message as a meeting invitation, while others allow the user to download and open the calendar file manually. The email is then sent through `SmtpEmailService` using MailKit and MimeKit. This feature does not directly integrate with external calendar APIs; it relies on the standard iCalendar format understood by common email and calendar clients.

## Things ChatGPT Must Not Claim

Do not claim:

- PawConnect directly creates events in Google Calendar or Outlook Calendar.
- The event is automatically inserted into the user's calendar without user action.
- The application uses an external calendar API.
- Calendar sending is separate from the normal email system.
- Calendar invites are sent for every adoption request.

Correct wording:

- "PawConnect sends an iCalendar `.ics` invitation."
- "Email clients can interpret the invite and allow the user to add it to their calendar."
- "The invite is generated after the shelter confirms the visit."
- "The feature uses standard MIME email parts and the iCalendar format."
