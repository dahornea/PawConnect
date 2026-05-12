using System.Net;
using System.Text;

namespace PawConnect.Services;

public sealed record EmailTemplateButton(string Text, string Url);

public sealed record EmailTemplateDetail(string Label, string Value);

public static class PawConnectEmailTemplate
{
    public static string BuildHtml(
        string title,
        string greeting,
        IEnumerable<string> paragraphs,
        EmailTemplateButton? primaryButton = null,
        string? fallbackLink = null,
        IEnumerable<EmailTemplateDetail>? details = null,
        string? note = null,
        bool hasAttachment = false)
    {
        var detailList = details?
            .Where(detail => !string.IsNullOrWhiteSpace(detail.Value))
            .ToList() ?? [];

        var builder = new StringBuilder();
        builder.Append($"""
            <!doctype html>
            <html>
            <body style="margin:0; padding:0; background:#f3f8f4; font-family:Arial, Helvetica, sans-serif; color:#17342f;">
                <div style="display:none; overflow:hidden; line-height:1px; opacity:0; max-height:0; max-width:0;">{Encode(title)}</div>
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f8f4; padding:28px 14px;">
                    <tr>
                        <td align="center">
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:700px; border-collapse:separate;">
                                <tr>
                                    <td style="background:#2f7d6b; color:#ffffff; padding:24px 32px; border-radius:18px 18px 0 0;">
                                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                                            <tr>
                                                <td style="width:42px; vertical-align:middle;">
                                                    <div style="width:34px; height:34px; border-radius:10px; background:#ffffff22; text-align:center; line-height:34px; font-size:18px; font-weight:700;">P</div>
                                                </td>
                                                <td style="vertical-align:middle;">
                                                    <div style="font-size:25px; font-weight:700; letter-spacing:.2px;">PawConnect</div>
                                                    <div style="font-size:13px; opacity:.92; margin-top:3px;">Stray dog adoption and shelter management</div>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="background:#ffffff; padding:30px 34px 28px; border:1px solid #dce9e2; border-top:0; border-radius:0 0 18px 18px; box-shadow:0 12px 32px rgba(23,52,47,.09);">
                                        <h1 style="margin:0 0 16px; font-size:26px; line-height:1.22; color:#123d35;">{Encode(title)}</h1>
                                        <p style="margin:0 0 12px; font-size:16px; line-height:1.55;">{Encode(greeting)}</p>
            """);

        foreach (var paragraph in paragraphs.Where(paragraph => !string.IsNullOrWhiteSpace(paragraph)))
        {
            builder.Append($"""
                                        <p style="margin:0 0 13px; font-size:15px; line-height:1.58; color:#37534d;">{Encode(paragraph)}</p>
                """);
        }

        if (detailList.Count > 0)
        {
            builder.Append("""
                                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:18px 0; background:#f7fbf8; border:1px solid #dce9e2; border-radius:12px;">
                """);

            foreach (var detail in detailList)
            {
                builder.Append($"""
                                            <tr>
                                                <td style="padding:9px 14px; width:36%; font-size:13px; color:#5f756f; border-bottom:1px solid #e7f0eb;">{Encode(detail.Label)}</td>
                                                <td style="padding:9px 14px; font-size:14px; color:#17342f; border-bottom:1px solid #e7f0eb;">{Encode(detail.Value)}</td>
                                            </tr>
                    """);
            }

            builder.Append("""
                                        </table>
                """);
        }

        if (primaryButton is not null)
        {
            builder.Append($"""
                                        <p style="margin:22px 0 18px;">
                                            <a href="{Encode(primaryButton.Url)}" style="display:inline-block; background:#e47a00; color:#ffffff; text-decoration:none; padding:15px 28px; border-radius:9px; font-weight:700; font-size:15px; letter-spacing:.1px;">{Encode(primaryButton.Text)}</a>
                                        </p>
                """);
        }

        if (!string.IsNullOrWhiteSpace(fallbackLink))
        {
            builder.Append($"""
                                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:16px 0; background:#fbfdfb; border:1px solid #e6f0eb; border-radius:10px;">
                                            <tr>
                                                <td style="padding:12px 14px;">
                                                    <div style="font-size:12px; color:#6a817a; margin-bottom:7px;">If the button does not work, copy and paste this link into your browser:</div>
                                                    <div style="font-size:12px; line-height:1.5; color:#2f7d6b; word-break:break-all; overflow-wrap:anywhere;">{Encode(fallbackLink)}</div>
                                                </td>
                                            </tr>
                                        </table>
                """);
        }

        if (hasAttachment)
        {
            builder.Append("""
                                        <p style="margin:14px 0; padding:11px 14px; background:#fff7ed; border:1px solid #fed7aa; border-radius:10px; color:#7c3f00; font-size:14px;">A PDF report is attached to this email.</p>
                """);
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            builder.Append($"""
                                        <p style="margin:14px 0 0; font-size:13px; line-height:1.55; color:#5f756f;">{Encode(note)}</p>
                """);
        }

        builder.Append("""
                                        <div style="margin-top:22px; padding-top:14px; border-top:1px solid #e7f0eb; color:#78918a; font-size:12px;">
                                            PawConnect - Stray dog adoption and shelter management
                                        </div>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """);

        return builder.ToString();
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
