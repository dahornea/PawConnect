namespace PawConnect.Services;

public interface ICopilotFeedbackService
{
    Task<CopilotFeedbackDto> SubmitFeedbackAsync(
        SubmitCopilotFeedbackRequest request,
        string adopterUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, CopilotFeedbackDto>> GetFeedbackForSessionAsync(
        int sessionId,
        string adopterUserId,
        CancellationToken cancellationToken = default);

    Task<CopilotExplanationDto> BuildExplanationAsync(
        int sessionId,
        string adopterUserId,
        AdoptionCopilotDogResult result,
        IReadOnlyList<AdoptionCopilotConstraint> appliedConstraints,
        CancellationToken cancellationToken = default);
}
