using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services.Intelligence;

public sealed class TransferSignalProvider(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IOptions<IntelligenceHubOptions> options) : IIntelligenceSignalProvider
{
    private readonly IntelligenceHubOptions settings = options.Value;

    public string ProviderKey => "DogTransfers";

    public async Task<IReadOnlyCollection<IntelligenceSignal>> CollectSignalsAsync(IntelligenceContext context, CancellationToken cancellationToken)
    {
        if (context.AudienceType == IntelligenceAudienceType.Adopter)
        {
            return [];
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.DogTransferRequests.AsNoTracking().Include(transfer => transfer.Dog).AsQueryable();
        if (context.AudienceType == IntelligenceAudienceType.Shelter)
        {
            query = query.Where(transfer => transfer.SourceShelterId == context.ShelterId || transfer.DestinationShelterId == context.ShelterId);
        }

        var transfers = await query.Where(transfer => transfer.Status == DogTransferStatus.Pending || transfer.Status == DogTransferStatus.Approved).ToListAsync(cancellationToken);
        var route = context.AudienceType == IntelligenceAudienceType.Shelter ? "/shelter/transfers" : "/admin/transfers";
        var signals = new List<IntelligenceSignal>();

        foreach (var transfer in transfers)
        {
            var ageHours = Math.Max(0, (context.UtcNow - transfer.RequestedAtUtc).TotalHours);
            var isUrgentPending = transfer.Status == DogTransferStatus.Pending && transfer.Priority == DogTransferPriority.Urgent;
            var isDelayed = ageHours >= settings.TransferPendingWarningHours;
            var approvedWaiting = transfer.Status == DogTransferStatus.Approved && ageHours >= settings.TransferPendingWarningHours;
            if (!isUrgentPending && !isDelayed && !approvedWaiting)
            {
                continue;
            }

            var actionLabel = transfer.Status == DogTransferStatus.Pending ? "Review transfer" : "Complete transfer";
            signals.Add(new IntelligenceSignal(
                $"TransferNeedsAction:{transfer.Id}", IntelligenceCategory.Transfer, "DogTransfers", "DogTransferRequest",
                transfer.Id.ToString(), transfer.Dog?.Name, null,
                context.AudienceType == IntelligenceAudienceType.Shelter ? context.ShelterId : transfer.DestinationShelterId,
                $"{actionLabel}: {transfer.Dog?.Name ?? "dog"}",
                transfer.Status == DogTransferStatus.Pending
                    ? $"The {transfer.Priority.ToString().ToLowerInvariant()} transfer request has waited {Math.Floor(ageHours)} hours for a response."
                    : $"The approved transfer has waited {Math.Floor(ageHours)} hours for completion.",
                "Transfer delays can block placement planning and leave the dog's operational ownership unclear.",
                "the transfer is responded to, completed, or cancelled",
                isUrgentPending ? "Urgent pending transfer" : $"Transfer unchanged for {settings.TransferPendingWarningHours} hours",
                [$"Status: {transfer.Status}", $"Priority: {transfer.Priority}", $"Requested: {transfer.RequestedAtUtc:dd MMM yyyy HH:mm}"],
                [new("Urgency", isUrgentPending ? 38 : 26, isUrgentPending ? "Transfer priority is urgent." : $"Waiting {Math.Floor(ageHours)} hours."), new("Operational impact", 28, "The transfer blocks shelter coordination."), new("Time sensitivity", isDelayed ? 20 : 12, $"Request age is {Math.Floor(ageHours)} hours.")],
                [new("open-transfer", actionLabel, "Open the transfer workspace and review the current state.", "Navigate", route, context.AudienceType.ToString(), "DogTransferRequest", transfer.Id.ToString(), true)],
                context.UtcNow));
        }

        return signals;
    }
}

