Add-Type -AssemblyName System.Drawing

$outPath = Join-Path $PSScriptRoot "pawconnect-use-case-diagram.png"
$width = 4200
$height = 2600

$bmp = New-Object System.Drawing.Bitmap($width, $height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
$g.Clear([System.Drawing.Color]::FromArgb(255, 252, 250, 246))

$fontRegular = New-Object System.Drawing.Font("Segoe UI", 32, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$fontSmall = New-Object System.Drawing.Font("Segoe UI", 28, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$fontBold = New-Object System.Drawing.Font("Segoe UI", 40, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fontTitle = New-Object System.Drawing.Font("Segoe UI", 64, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fontSection = New-Object System.Drawing.Font("Segoe UI", 34, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fontActor = New-Object System.Drawing.Font("Segoe UI", 32, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

$black = [System.Drawing.Color]::FromArgb(35, 48, 45)
$muted = [System.Drawing.Color]::FromArgb(96, 110, 104)
$green = [System.Drawing.Color]::FromArgb(38, 119, 93)
$line = [System.Drawing.Color]::FromArgb(150, 100, 112, 108)
$boundary = [System.Drawing.Color]::FromArgb(42, 86, 72)
$sectionBorder = [System.Drawing.Color]::FromArgb(195, 213, 204)
$publicFill = [System.Drawing.Color]::FromArgb(242, 249, 246)
$adopterFill = [System.Drawing.Color]::FromArgb(239, 248, 252)
$shelterFill = [System.Drawing.Color]::FromArgb(250, 246, 237)
$adminFill = [System.Drawing.Color]::FromArgb(248, 242, 249)
$useCaseFill = [System.Drawing.Color]::White

function New-Pen($color, $width = 3) {
    $pen = New-Object System.Drawing.Pen($color, $width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    return $pen
}

function New-Brush($color) {
    return New-Object System.Drawing.SolidBrush($color)
}

function Draw-RoundedRect($rect, $radius, $fill, $outline, $outlineWidth = 3) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Brush $fill), $path)
    $g.DrawPath((New-Pen $outline $outlineWidth), $path)
    $path.Dispose()
}

function Draw-CenteredText($text, $rect, $font, $color) {
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $format.Trimming = [System.Drawing.StringTrimming]::Word
    $g.DrawString($text, $font, (New-Brush $color), $rect, $format)
    $format.Dispose()
}

function Draw-LeftText($text, $x, $y, $font, $color) {
    $g.DrawString($text, $font, (New-Brush $color), [System.Drawing.PointF]::new($x, $y))
}

function Draw-UseCase($uc) {
    $rect = $uc.Rect
    $g.FillEllipse((New-Brush $useCaseFill), $rect)
    $g.DrawEllipse((New-Pen $black 4), $rect)
    $inner = [System.Drawing.RectangleF]::new($rect.X + 28, $rect.Y + 12, $rect.Width - 56, $rect.Height - 24)
    Draw-CenteredText $uc.Text $inner $fontRegular $black
}

function Draw-Actor($actor) {
    $x = $actor.X
    $y = $actor.Y
    $pen = New-Pen $black 5
    $g.DrawEllipse($pen, $x - 36, $y, 72, 72)
    $g.DrawLine($pen, $x, $y + 72, $x, $y + 180)
    $g.DrawLine($pen, $x - 76, $y + 115, $x + 76, $y + 115)
    $g.DrawLine($pen, $x, $y + 180, $x - 68, $y + 285)
    $g.DrawLine($pen, $x, $y + 180, $x + 68, $y + 285)
    $labelRect = [System.Drawing.RectangleF]::new($x - 285, $y + 310, 570, 92)
    Draw-CenteredText $actor.Label $labelRect $fontActor $black
}

function Edge-Point($rect, $fromY) {
    $cx = $rect.X + ($rect.Width / 2)
    if ($fromY -lt $rect.Y) {
        return [System.Drawing.PointF]::new($cx, $rect.Y + 5)
    }
    return [System.Drawing.PointF]::new($cx, $rect.Bottom - 5)
}

$systemRect = [System.Drawing.Rectangle]::new(150, 120, 3900, 2250)
Draw-RoundedRect $systemRect 20 ([System.Drawing.Color]::FromArgb(255, 255, 255, 255)) $boundary 5
Draw-CenteredText "PawConnect" ([System.Drawing.RectangleF]::new($systemRect.X, 145, $systemRect.Width, 90)) $fontTitle $black
Draw-LeftText "Use case diagram - main platform roles and interactions" 1515 2290 $fontSmall $muted

$sections = @(
    @{ Rect = [System.Drawing.Rectangle]::new(260, 340, 850, 1780); Title = "Public Visitor"; Fill = $publicFill },
    @{ Rect = [System.Drawing.Rectangle]::new(1210, 340, 850, 1780); Title = "Adopter"; Fill = $adopterFill },
    @{ Rect = [System.Drawing.Rectangle]::new(2160, 340, 850, 1780); Title = "Shelter Representative"; Fill = $shelterFill },
    @{ Rect = [System.Drawing.Rectangle]::new(3110, 340, 850, 1780); Title = "Administrator"; Fill = $adminFill }
)

foreach ($section in $sections) {
    Draw-RoundedRect $section.Rect 24 $section.Fill $sectionBorder 3
    Draw-LeftText $section.Title ($section.Rect.X + 36) ($section.Rect.Y + 24) $fontSection $green
}

$actors = @{
    Public = @{ X = 685; Y = 455; Label = "Public Visitor" }
    Adopter = @{ X = 1635; Y = 455; Label = "Adopter" }
    Shelter = @{ X = 2585; Y = 455; Label = "Shelter Representative" }
    Admin = @{ X = 3535; Y = 455; Label = "Administrator" }
}

$useCases = @{}
function Add-UC($key, $text, $x, $y, $w = 620, $h = 110) {
    $script:useCases[$key] = @{ Text = $text; Rect = [System.Drawing.RectangleF]::new($x, $y, $w, $h) }
}

Add-UC "BrowseDogs" "Browse dogs" 375 870
Add-UC "FilterDogs" "Filter dogs by shelter, area, and status" 375 1015 620 125
Add-UC "DogDetails" "View dog details" 375 1175
Add-UC "BrowseShelters" "Browse shelters and maps" 375 1320 620 125
Add-UC "Register" "Register / log in" 375 1480
Add-UC "ShelterApply" "Submit shelter application" 375 1625 620 125

Add-UC "Profile" "Manage adopter profile" 1325 870
Add-UC "Favorites" "Save favorite dogs" 1325 1015
Add-UC "Copilot" "Use AI Adoption Copilot" 1325 1160 620 125
Add-UC "Recommendations" "View recommended dogs" 1325 1320 620 125
Add-UC "AdoptionRequest" "Submit adoption request" 1325 1480 620 125
Add-UC "TrackRequests" "Track / cancel requests" 1325 1640 620 125
Add-UC "AdopterNotifications" "View notifications" 1325 1800

Add-UC "ShelterProfile" "Manage shelter profile and location" 2275 870 620 125
Add-UC "ManageDogs" "Manage dogs" 2275 1030
Add-UC "DogRecords" "Manage dog images and medical records" 2275 1175 620 125
Add-UC "ReviewRequests" "Review adoption requests" 2275 1335 620 125
Add-UC "ConfirmVisits" "Confirm visits / finalize adoption" 2275 1495 620 125
Add-UC "ShelterExport" "Export shelter data" 2275 1655
Add-UC "ReportHistory" "View report history" 2275 1800
Add-UC "ShelterNotifications" "View notifications" 2275 1945

Add-UC "ApproveShelters" "Review shelter applications" 3225 870 620 125
Add-UC "ManageUsers" "Manage users and platform data" 3225 1030 620 125
Add-UC "AdminReports" "View reports and activity logs" 3225 1190 620 125
Add-UC "RebuildIndex" "Rebuild dog search index" 3225 1350 620 125
Add-UC "SystemHealth" "Monitor system health" 3225 1510

$connections = @(
    @{ A = "Public"; U = "BrowseDogs" }, @{ A = "Public"; U = "FilterDogs" }, @{ A = "Public"; U = "DogDetails" },
    @{ A = "Public"; U = "BrowseShelters" }, @{ A = "Public"; U = "Register" }, @{ A = "Public"; U = "ShelterApply" },
    @{ A = "Adopter"; U = "Profile" }, @{ A = "Adopter"; U = "Favorites" }, @{ A = "Adopter"; U = "Copilot" },
    @{ A = "Adopter"; U = "Recommendations" }, @{ A = "Adopter"; U = "AdoptionRequest" }, @{ A = "Adopter"; U = "TrackRequests" },
    @{ A = "Adopter"; U = "AdopterNotifications" },
    @{ A = "Shelter"; U = "ShelterProfile" }, @{ A = "Shelter"; U = "ManageDogs" },
    @{ A = "Shelter"; U = "DogRecords" }, @{ A = "Shelter"; U = "ReviewRequests" }, @{ A = "Shelter"; U = "ConfirmVisits" },
    @{ A = "Shelter"; U = "ShelterExport" }, @{ A = "Shelter"; U = "ReportHistory" }, @{ A = "Shelter"; U = "ShelterNotifications" },
    @{ A = "Admin"; U = "ApproveShelters" }, @{ A = "Admin"; U = "ManageUsers" }, @{ A = "Admin"; U = "AdminReports" },
    @{ A = "Admin"; U = "RebuildIndex" }, @{ A = "Admin"; U = "SystemHealth" }
)

$linePen = New-Pen ([System.Drawing.Color]::FromArgb(105, 104, 116, 112)) 3
foreach ($connection in $connections) {
    $actor = $actors[$connection.A]
    $uc = $useCases[$connection.U]
    $start = [System.Drawing.PointF]::new($actor.X, $actor.Y + 292)
    $target = Edge-Point $uc.Rect $start.Y
    $g.DrawLine($linePen, $start, $target)
}

foreach ($uc in $useCases.Values) {
    Draw-UseCase $uc
}

foreach ($actor in $actors.Values) {
    Draw-Actor $actor
}

$legendRect = [System.Drawing.Rectangle]::new(880, 2170, 2440, 110)
Draw-RoundedRect $legendRect 18 ([System.Drawing.Color]::FromArgb(248, 252, 250)) $sectionBorder 2
Draw-CenteredText "Readable layout: one column per role; solid lines show which actor initiates each platform function." ([System.Drawing.RectangleF]::new($legendRect.X + 25, $legendRect.Y + 15, $legendRect.Width - 50, $legendRect.Height - 30)) $fontSmall $muted

$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()
Write-Host $outPath
