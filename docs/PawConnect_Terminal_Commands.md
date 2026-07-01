# PawConnect Terminal Commands

Useful commands for running PawConnect locally, preparing the demo laptop, updating OpenAI user secrets, starting smtp4dev, restoring packages, applying migrations, and basic troubleshooting.

Run most commands from the project root:

```powershell
cd D:\PawConnect
```

or on the main laptop:

```powershell
cd D:\Licenta\PawConnect
```

## 1. Start smtp4dev for Local Emails

PawConnect is configured to send development email to:

```text
SMTP host: localhost
SMTP port: 2525
Inbox UI: http://localhost:3000
```

Start smtp4dev in a separate terminal before testing emails:

```powershell
smtp4dev --smtpport 2525 --urls http://localhost:3000
```

Then open:

```text
http://localhost:3000
```

If `smtp4dev` is not recognized, install it:

```powershell
dotnet tool install -g Rnwood.Smtp4dev
```

If it is already installed but outdated:

```powershell
dotnet tool update -g Rnwood.Smtp4dev
```

If the terminal still does not recognize `smtp4dev`, close and reopen PowerShell so the global tool path is refreshed.

## 2. Check .NET SDK

PawConnect targets:

```text
net10.0
```

Check installed SDKs:

```powershell
dotnet --list-sdks
```

Check the SDK selected in the PawConnect folder:

```powershell
dotnet --version
```

Expected:

```text
10.0.301
```

or another compatible .NET 10 SDK if `global.json` allows roll-forward.

## 3. Restore Packages

Normal restore:

```powershell
dotnet restore
```

If another laptop has private NuGet feeds causing `401 Unauthorized`, force the repo config:

```powershell
dotnet restore --configfile NuGet.config
```

List NuGet sources:

```powershell
dotnet nuget list source
```

Disable a problematic global source if needed:

```powershell
dotnet nuget disable source "SOURCE_NAME_HERE"
```

## 4. Build and Run

Build:

```powershell
dotnet build
```

Run with HTTPS profile:

```powershell
dotnet run --launch-profile https
```

Main local URLs:

```text
https://localhost:7125
http://localhost:5180
```

If the app is already running and `dotnet build` fails because files are locked, stop the running app first.

Temporary build that avoids the normal `bin` folder:

```powershell
dotnet build -o .tmp-build
```

Clean temporary build folder:

```powershell
Remove-Item -LiteralPath .tmp-build -Recurse -Force
```

## 5. Entity Framework / Database

Apply migrations:

```powershell
dotnet ef database update
```

If `dotnet ef` is not installed:

```powershell
dotnet tool install --global dotnet-ef
```

Update `dotnet ef`:

```powershell
dotnet tool update --global dotnet-ef
```

Add a migration:

```powershell
dotnet ef migrations add MigrationNameHere
```

Example:

```powershell
dotnet ef migrations add AddCoatColorToDog
```

Check LocalDB instances:

```powershell
sqllocaldb info
```

Start LocalDB:

```powershell
sqllocaldb start MSSQLLocalDB
```

## 6. OpenAI User Secrets

List user secrets:

```powershell
dotnet user-secrets list
```

Set OpenAI API key:

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "sk-your-key-here"
```

Enable OpenAI:

```powershell
dotnet user-secrets set "OpenAI:Enabled" "true"
```

Set chat model if needed:

```powershell
dotnet user-secrets set "OpenAI:ChatModel" "gpt-5.4-mini"
```

Set embedding model if needed:

```powershell
dotnet user-secrets set "OpenAI:EmbeddingModel" "text-embedding-3-small"
```

Remove old OpenAI key:

```powershell
dotnet user-secrets remove "OpenAI:ApiKey"
```

After changing user secrets, restart the app.

## 7. Tests

Run tests:

```powershell
dotnet test
```

Run tests with normal verbosity:

```powershell
dotnet test --verbosity normal
```

## 8. Database Backup and Restore with SQL

Create a single-file backup from SQL Server:

```sql
BACKUP DATABASE [PawConnect]
TO DISK = N'C:\SQLBackups\PawConnect_full.bak'
WITH INIT, FORMAT, NAME = N'PawConnect Full Backup';
```

Verify backup:

```sql
RESTORE VERIFYONLY
FROM DISK = N'C:\SQLBackups\PawConnect_full.bak';
```

Restore backup:

```sql
RESTORE DATABASE [PawConnect]
FROM DISK = N'C:\SQLBackups\PawConnect_full.bak'
WITH REPLACE;
```

If restore complains about file paths, inspect logical file names:

```sql
RESTORE FILELISTONLY
FROM DISK = N'C:\SQLBackups\PawConnect_full.bak';
```

Then restore with explicit file paths:

```sql
RESTORE DATABASE [PawConnect]
FROM DISK = N'C:\SQLBackups\PawConnect_full.bak'
WITH REPLACE,
MOVE N'PawConnect' TO N'C:\SQLData\PawConnect.mdf',
MOVE N'PawConnect_log' TO N'C:\SQLData\PawConnect_log.ldf';
```

Use the actual logical names from `RESTORE FILELISTONLY`.

## 9. Common Demo Startup Order

Use two or three terminals.

Terminal 1, smtp4dev:

```powershell
smtp4dev --smtpport 2525 --urls http://localhost:3000
```

Terminal 2, PawConnect:

```powershell
cd D:\PawConnect
dotnet run --launch-profile https
```

Browser:

```text
https://localhost:7125
http://localhost:3000
```

## 10. Quick Troubleshooting

If projects do not load in Visual Studio:

```powershell
dotnet --version
dotnet --list-sdks
dotnet restore
dotnet build
```

If restore fails with `NU1301` and Azure Artifacts `401 Unauthorized`:

```powershell
dotnet restore --configfile NuGet.config
```

If `Microsoft.NET.Sdk.Web` cannot be found:

```powershell
Test-Path "C:\Program Files\dotnet\sdk\10.0.301\Sdks\Microsoft.NET.Sdk.Web\Sdk\Sdk.props"
```

Expected:

```text
True
```

If false, reinstall the .NET 10 SDK and Visual Studio ASP.NET workload.

If OpenAI changes are not taking effect:

```powershell
dotnet user-secrets list
```

Then restart the app.

If emails are not visible:

1. Make sure smtp4dev is running.
2. Confirm smtp4dev uses SMTP port `2525`.
3. Open `http://localhost:3000`.
4. Restart PawConnect after changing email settings.

