using System.Diagnostics;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using PawConnect.Components;
using PawConnect.Components.Account;
using PawConnect.Data;
using PawConnect.Jobs;
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
builder.Services.AddMudServices();
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
builder.Services.AddScoped<IAdoptionRequestService, AdoptionRequestService>();
builder.Services.AddScoped<IFavoriteDogService, FavoriteDogService>();
builder.Services.AddScoped<IShelterService, ShelterService>();
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
builder.Services.AddScoped<IBrowserFileDownloadService, BrowserFileDownloadService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<ScheduledReportSettings>(builder.Configuration.GetSection("ScheduledReports"));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IEmailSender<ApplicationUser>, PawConnectIdentityEmailSender>();
builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(client =>
{
    client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PawConnect/1.0 (shelter map demo)");
});

var scheduledReportSettings = builder.Configuration
    .GetSection("ScheduledReports")
    .Get<ScheduledReportSettings>() ?? new ScheduledReportSettings();
var shelterReportIntervalMinutes = scheduledReportSettings.GetSafeShelterReportIntervalMinutes();

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
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

try
{
    await IdentitySeedData.SeedAsync(app.Services);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Identity seed data was not applied. Run the EF database update command, then restart the app.");
}

OpenLocalEmailInboxOnStartup(app);

app.Run();

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
