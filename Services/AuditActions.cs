namespace PawConnect.Services;

public static class AuditActions
{
    public const string DogCreated = nameof(DogCreated);
    public const string DogUpdated = nameof(DogUpdated);
    public const string DogDeleted = nameof(DogDeleted);
    public const string DogStatusChanged = nameof(DogStatusChanged);
    public const string DogImageAdded = nameof(DogImageAdded);
    public const string DogImageDeleted = nameof(DogImageDeleted);
    public const string MedicalRecordAdded = nameof(MedicalRecordAdded);
    public const string MedicalRecordUpdated = nameof(MedicalRecordUpdated);
    public const string MedicalRecordDeleted = nameof(MedicalRecordDeleted);
    public const string AdoptionRequestSubmitted = nameof(AdoptionRequestSubmitted);
    public const string VisitConfirmed = nameof(VisitConfirmed);
    public const string VisitReminderSent = nameof(VisitReminderSent);
    public const string VisitCompleted = nameof(VisitCompleted);
    public const string AdoptionRequestAccepted = nameof(AdoptionRequestAccepted);
    public const string AdoptionRequestRejected = nameof(AdoptionRequestRejected);
    public const string AdoptionRequestCancelled = nameof(AdoptionRequestCancelled);
    public const string DogMarkedAdopted = nameof(DogMarkedAdopted);
    public const string ShelterRegistrationRequestSubmitted = nameof(ShelterRegistrationRequestSubmitted);
    public const string ShelterRegistrationRequestAccepted = nameof(ShelterRegistrationRequestAccepted);
    public const string ShelterRegistrationRequestRejected = nameof(ShelterRegistrationRequestRejected);
    public const string ShelterCreated = nameof(ShelterCreated);
    public const string ShelterUpdated = nameof(ShelterUpdated);
    public const string ResourceCreated = nameof(ResourceCreated);
    public const string ResourceUpdated = nameof(ResourceUpdated);
    public const string ResourceDeleted = nameof(ResourceDeleted);
    public const string ResourceCsvImported = nameof(ResourceCsvImported);
    public const string DogCsvImported = nameof(DogCsvImported);
    public const string ShelterCsvImported = nameof(ShelterCsvImported);
    public const string ShelterRequestsCsvImported = nameof(ShelterRequestsCsvImported);
    public const string UserUpdatedByAdmin = nameof(UserUpdatedByAdmin);
    public const string ShelterUpdatedByAdmin = nameof(ShelterUpdatedByAdmin);
    public const string ReportGenerated = nameof(ReportGenerated);
    public const string ExportGenerated = nameof(ExportGenerated);
    public const string AdoptionCopilotRequested = nameof(AdoptionCopilotRequested);
    public const string AdoptionCopilotCompleted = nameof(AdoptionCopilotCompleted);
    public const string AdoptionCopilotFailed = nameof(AdoptionCopilotFailed);
    public const string CopilotEvaluationRun = nameof(CopilotEvaluationRun);
    public const string CopilotEvaluationFailed = nameof(CopilotEvaluationFailed);
    public const string ShelterAvailabilitySlotCreated = nameof(ShelterAvailabilitySlotCreated);
    public const string ShelterAvailabilitySlotCancelled = nameof(ShelterAvailabilitySlotCancelled);
    public const string ShelterAvailabilitySlotBooked = nameof(ShelterAvailabilitySlotBooked);
    public const string DogTransferRequested = nameof(DogTransferRequested);
    public const string DogTransferApproved = nameof(DogTransferApproved);
    public const string DogTransferRejected = nameof(DogTransferRejected);
    public const string DogTransferCancelled = nameof(DogTransferCancelled);
    public const string DogTransferCompleted = nameof(DogTransferCompleted);
    public const string DogTransferAdminNoteUpdated = nameof(DogTransferAdminNoteUpdated);

    public static readonly IReadOnlyList<string> All =
    [
        DogCreated,
        DogUpdated,
        DogDeleted,
        DogStatusChanged,
        DogImageAdded,
        DogImageDeleted,
        MedicalRecordAdded,
        MedicalRecordUpdated,
        MedicalRecordDeleted,
        AdoptionRequestSubmitted,
        VisitConfirmed,
        VisitReminderSent,
        VisitCompleted,
        AdoptionRequestAccepted,
        AdoptionRequestRejected,
        AdoptionRequestCancelled,
        DogMarkedAdopted,
        ShelterRegistrationRequestSubmitted,
        ShelterRegistrationRequestAccepted,
        ShelterRegistrationRequestRejected,
        ShelterCreated,
        ShelterUpdated,
        ResourceCreated,
        ResourceUpdated,
        ResourceDeleted,
        ResourceCsvImported,
        DogCsvImported,
        ShelterCsvImported,
        ShelterRequestsCsvImported,
        UserUpdatedByAdmin,
        ShelterUpdatedByAdmin,
        ReportGenerated,
        ExportGenerated,
        AdoptionCopilotRequested,
        AdoptionCopilotCompleted,
        AdoptionCopilotFailed,
        CopilotEvaluationRun,
        CopilotEvaluationFailed,
        ShelterAvailabilitySlotCreated,
        ShelterAvailabilitySlotCancelled,
        ShelterAvailabilitySlotBooked,
        DogTransferRequested,
        DogTransferApproved,
        DogTransferRejected,
        DogTransferCancelled,
        DogTransferCompleted,
        DogTransferAdminNoteUpdated
    ];
}
