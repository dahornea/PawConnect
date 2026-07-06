using System.Diagnostics;
using System.Threading.RateLimiting;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using MudBlazor;
using MudBlazor.Services;
using PawConnect.Components;
using PawConnect.Components.Account;
using PawConnect.Data;
using PawConnect.Hubs;
using PawConnect.Jobs;
using PawConnect.OpenApi;
using PawConnect.Repositories;
using PawConnect.Services;
using Quartz;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ApiFixedWindow", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetRateLimitPartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("AuthenticatedDownload", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetRateLimitPartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PawConnect Public API",
        Version = "v1",
        Description = "REST API for public dog discovery, shelters, adoption applications, notification preferences, and selected admin summaries.",
        Contact = new OpenApiContact
        {
            Name = "PawConnect"
        }
    });

    options.AddSecurityDefinition("PawConnectCookie", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = ".AspNetCore.Identity.Application",
        Description = "PawConnect uses ASP.NET Core Identity cookie authentication. Sign in through the Blazor UI in the same browser before calling protected endpoints from Swagger."
    });
    options.OperationFilter<AuthorizeOperationFilter>();
});
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Docker")
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);

    options.Events.OnRedirectToLogin = context =>
    {
        if (IsApiRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (IsApiRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString), ServiceLifetime.Scoped);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<IDogService, DogService>();
builder.Services.AddScoped<IDogTransferService, DogTransferService>();

builder.Services.AddScoped<IVolunteerTaskService, VolunteerTaskService>();
builder.Services.AddScoped<IDogBreedService, DogBreedService>();
builder.Services.AddScoped<IAdoptionRequestService, AdoptionRequestService>();
builder.Services.AddScoped<IFavoriteDogService, FavoriteDogService>();
builder.Services.AddScoped<ISavedDogSearchService, SavedDogSearchService>();
builder.Services.AddScoped<IShelterService, ShelterService>();
builder.Services.AddScoped<IShelterAvailabilityService, ShelterAvailabilityService>();
builder.Services.AddScoped<IResourceStockService, ResourceStockService>();
builder.Services.AddScoped<IMedicalRecordService, MedicalRecordService>();
builder.Services.AddScoped<IDogImageService, DogImageService>();
builder.Services.AddScoped<IResourceCategoryService, ResourceCategoryService>();
builder.Services.AddScoped<IFoodTypeService, FoodTypeService>();
builder.Services.AddScoped<IAdopterProfileService, AdopterProfileService>();
builder.Services.AddScoped<IRecentlyViewedDogService, RecentlyViewedDogService>();
builder.Services.AddScoped<IPdfReportService, PdfReportService>();
builder.Services.AddScoped<IShelterRegistrationRequestService, ShelterRegistrationRequestService>();
builder.Services.AddScoped<IShelterSummaryReportService, ShelterSummaryReportService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<ICsvImportService, CsvImportService>();
builder.Services.AddScoped<IBrowserFileDownloadService, BrowserFileDownloadService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
builder.Services.AddScoped<INotificationDeliveryLogService, NotificationDeliveryLogService>();
builder.Services.AddScoped<INotificationOutboxService, NotificationOutboxService>();
builder.Services.AddScoped<INotificationOutboxProcessor, NotificationOutboxProcessor>();
builder.Services.AddScoped<IReportHistoryService, ReportHistoryService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IVisitReminderService, VisitReminderService>();
builder.Services.AddScoped<IDogRecommendationService, DogRecommendationService>();
builder.Services.AddScoped<IDogSearchDocumentService, DogSearchDocumentService>();
builder.Services.AddScoped<IDogSearchEmbeddingService, DogSearchEmbeddingService>();
builder.Services.AddScoped<ISearchIndexDashboardService, SearchIndexDashboardService>();
builder.Services.AddScoped<ISemanticDogSearchService, SemanticDogSearchService>();
builder.Services.AddScoped<IAdoptionCopilotToolService, AdoptionCopilotToolService>();
builder.Services.AddScoped<ICopilotHistoryService, CopilotHistoryService>();
builder.Services.AddScoped<ICopilotFeedbackService, CopilotFeedbackService>();
builder.Services.AddScoped<IAdoptionCopilotService, AdoptionCopilotService>();
builder.Services.AddScoped<ICopilotCriteriaComparisonService, CopilotCriteriaComparisonService>();
builder.Services.AddScoped<ICopilotEvaluationService, CopilotEvaluationService>();
builder.Services.AddScoped<ICopilotStateService, CopilotStateService>();
builder.Services.AddScoped<IDogProfileQualityService, DogProfileQualityService>();
builder.Services.AddScoped<IAiReportSummaryService, AiReportSummaryService>();
builder.Services.AddScoped<INaturalLanguageSearchService, NaturalLanguageSearchService>();
builder.Services.AddScoped<IShelterOperationsAssistantService, ShelterOperationsAssistantService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IMessageReportService, MessageReportService>();
builder.Services.AddScoped<IMessageAttachmentStorageService, LocalMessageAttachmentStorageService>();
builder.Services.AddScoped<ILostFoundPostService, LostFoundPostService>();
builder.Services.AddSingleton<IConversationRealtimeNotifier, ConversationRealtimeNotifier>();
builder.Services.AddSingleton<IDistanceService, DistanceService>();
builder.Services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<NotificationOutboxSettings>(builder.Configuration.GetSection("NotificationOutbox"));
builder.Services.Configure<ScheduledReportSettings>(builder.Configuration.GetSection("ScheduledReports"));
builder.Services.Configure<VisitReminderSettings>(builder.Configuration.GetSection("VisitReminders"));
builder.Services.Configure<OpenAiSettings>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<DogImageStorageOptions>(builder.Configuration.GetSection("DogImageStorage"));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddHostedService<NotificationOutboxHostedService>();
builder.Services.AddScoped<IEmailSender<ApplicationUser>, PawConnectIdentityEmailSender>();
builder.Services.AddScoped<IDogImageStorageService, LocalDogImageStorageService>();
builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(client =>
{
    client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PawConnect/1.0 (shelter map demo)");
});
builder.Services.AddHttpClient<IOpenAiRecommendationClient, OpenAiRecommendationClient>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
});
builder.Services.AddHttpClient<IEmbeddingService, OpenAiEmbeddingService>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
});
builder.Services.AddHttpClient<IOpenAiAdoptionCopilotClient, OpenAiAdoptionCopilotClient>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
});
builder.Services.AddHttpClient<IOpenAiDogProfileQualityClient, OpenAiDogProfileQualityClient>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
});
builder.Services.AddHttpClient<IOpenAiReportSummaryClient, OpenAiReportSummaryClient>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
});
builder.Services.AddHttpClient<IOpenAiNaturalLanguageSearchClient, OpenAiNaturalLanguageSearchClient>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
});
builder.Services.AddHttpClient<IOpenAiShelterOperationsAssistantClient, OpenAiShelterOperationsAssistantClient>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
});

var scheduledReportSettings = builder.Configuration
    .GetSection("ScheduledReports")
    .Get<ScheduledReportSettings>() ?? new ScheduledReportSettings();
var shelterReportIntervalMinutes = scheduledReportSettings.GetSafeShelterReportIntervalMinutes();
var visitReminderSettings = builder.Configuration
    .GetSection("VisitReminders")
    .Get<VisitReminderSettings>() ?? new VisitReminderSettings();
var visitReminderCheckIntervalMinutes = visitReminderSettings.GetSafeCheckIntervalMinutes();

builder.Services.AddQuartz(quartz =>
{
    if (scheduledReportSettings.Enabled)
    {
        var jobKey = new JobKey(nameof(ShelterSummaryReportJob));
        quartz.AddJob<ShelterSummaryReportJob>(options => options.WithIdentity(jobKey));
        quartz.AddTrigger(options =>
        {
            options
                .ForJob(jobKey)
                .WithIdentity($"{nameof(ShelterSummaryReportJob)}-trigger")
                .WithSimpleSchedule(schedule => schedule
                    .WithIntervalInMinutes(shelterReportIntervalMinutes)
                    .RepeatForever());

            if (scheduledReportSettings.RunOnStartupInDevelopment && builder.Environment.IsDevelopment())
            {
                options.StartNow();
            }
            else
            {
                options.StartAt(DateBuilder.FutureDate(shelterReportIntervalMinutes, IntervalUnit.Minute));
            }
        });
    }

    if (visitReminderSettings.Enabled)
    {
        var jobKey = new JobKey(nameof(VisitReminderJob));
        quartz.AddJob<VisitReminderJob>(options => options.WithIdentity(jobKey));
        quartz.AddTrigger(options =>
        {
            options
                .ForJob(jobKey)
                .WithIdentity($"{nameof(VisitReminderJob)}-trigger")
                .WithSimpleSchedule(schedule => schedule
                    .WithIntervalInMinutes(visitReminderCheckIntervalMinutes)
                    .RepeatForever())
                .StartAt(DateBuilder.FutureDate(visitReminderCheckIntervalMinutes, IntervalUnit.Minute));
        });
    }
});
builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        AddSecurityHeaders(context);
        return Task.CompletedTask;
    });

    await next(context);
});
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PawConnect Public API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "PawConnect Public API";
    });
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRateLimiter();

app.MapControllers().RequireRateLimiting("ApiFixedWindow");
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHub<AdoptionChatHub>("/hubs/adoption-chat");
app.MapGet("/health", async (
        ApplicationDbContext context,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return await context.Database.CanConnectAsync(cancellationToken)
                ? Results.Ok(new { status = "Healthy" })
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    })
    .AllowAnonymous();
app.MapGet("/message-attachments/{attachmentId:int}", async (
        int attachmentId,
        ClaimsPrincipal user,
        IMessageService messageService,
        CancellationToken cancellationToken) =>
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var attachment = await messageService.GetAttachmentFileAsync(attachmentId, userId, cancellationToken);
        return attachment is null
            ? Results.NotFound()
            : Results.File(
                attachment.Content,
                attachment.ContentType,
                enableRangeProcessing: true);
    })
    .RequireAuthorization()
    .RequireRateLimiting("AuthenticatedDownload");

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

await ApplyConfiguredDatabaseMigrationsAsync(app);

if (ShouldSeedData(app))
{
    try
    {
        await IdentitySeedData.SeedAsync(app.Services);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Identity seed data was not applied. Run the EF database update command, then restart the app.");
    }
}

OpenLocalEmailInboxOnStartup(app);

app.Run();

static string GetRateLimitPartitionKey(HttpContext context)
{
    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!string.IsNullOrWhiteSpace(userId))
    {
        return $"user:{userId}";
    }

    var remoteIp = context.Connection.RemoteIpAddress?.ToString();
    return string.IsNullOrWhiteSpace(remoteIp) ? "anonymous:unknown" : $"ip:{remoteIp}";
}

static void AddSecurityHeaders(HttpContext context)
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), payment=(), usb=(), geolocation=(self)";
}
static bool IsApiRequest(HttpRequest request)
{
    return request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
}

static async Task ApplyConfiguredDatabaseMigrationsAsync(WebApplication app)
{
    if (!app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    {
        return;
    }

    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
        app.Logger.LogInformation("Database migrations were applied on startup.");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database migrations could not be applied on startup. Check SQL Server startup and connection string settings.");
    }
}

static bool ShouldSeedData(WebApplication app)
{
    var configuredSeedEnabled = app.Configuration.GetValue<bool?>("SeedData:Enabled");
    if (configuredSeedEnabled.HasValue)
    {
        return configuredSeedEnabled.Value;
    }

    return app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker");
}

static void OpenLocalEmailInboxOnStartup(WebApplication app)
{
    if (!app.Environment.IsDevelopment())
    {
        return;
    }

    var emailSettings = app.Configuration
        .GetSection("EmailSettings")
        .Get<EmailSettings>() ?? new EmailSettings();

    if (!emailSettings.OpenLocalInboxOnStartup ||
        string.IsNullOrWhiteSpace(emailSettings.LocalInboxUrl) ||
        !Uri.TryCreate(emailSettings.LocalInboxUrl, UriKind.Absolute, out var inboxUri))
    {
        return;
    }

    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = inboxUri.ToString(),
            UseShellExecute = true
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Local email inbox could not be opened automatically.");
    }
}

public partial class Program;

