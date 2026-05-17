Add-Type -AssemblyName System.Drawing

$outPath = Join-Path $PSScriptRoot "pawconnect-use-case-diagram-classic.png"
$width = 2600
$height = 1900

$bmp = New-Object System.Drawing.Bitmap($width, $height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
$g.Clear([System.Drawing.Color]::White)

$fontRegular = New-Object System.Drawing.Font("Segoe UI", 24, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$fontSmall = New-Object System.Drawing.Font("Segoe UI", 22, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$fontBold = New-Object System.Drawing.Font("Segoe UI", 25, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fontTitle = New-Object System.Drawing.Font("Segoe UI", 28, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$fontCaption = New-Object System.Drawing.Font("Times New Roman", 28, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)

$black = [System.Drawing.Color]::FromArgb(25, 25, 25)
$lineColor = [System.Drawing.Color]::FromArgb(150, 75, 75, 75)
$lightLine = [System.Drawing.Color]::FromArgb(90, 90, 90, 90)

function New-Pen($color, $width = 2) {
    $pen = New-Object System.Drawing.Pen($color, $width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    return $pen
}

function New-Brush($color) {
    return New-Object System.Drawing.SolidBrush($color)
}

function Draw-CenteredText($text, $rect, $font, $color) {
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $format.Trimming = [System.Drawing.StringTrimming]::Word
    $g.DrawString($text, $font, (New-Brush $color), $rect, $format)
    $format.Dispose()
}

function Draw-UseCase($uc) {
    $rect = $uc.Rect
    $g.FillEllipse((New-Brush ([System.Drawing.Color]::White)), $rect)
    $g.DrawEllipse((New-Pen $black 2), $rect)
    $inner = [System.Drawing.RectangleF]::new($rect.X + 18, $rect.Y + 8, $rect.Width - 36, $rect.Height - 16)
    Draw-CenteredText $uc.Text $inner $fontRegular $black
}

function Draw-Actor($actor) {
    $x = $actor.X
    $y = $actor.Y
    $pen = New-Pen $black 3
    $g.DrawEllipse($pen, $x - 24, $y, 48, 48)
    $g.DrawLine($pen, $x, $y + 48, $x, $y + 126)
    $g.DrawLine($pen, $x - 54, $y + 82, $x + 54, $y + 82)
    $g.DrawLine($pen, $x, $y + 126, $x - 48, $y + 205)
    $g.DrawLine($pen, $x, $y + 126, $x + 48, $y + 205)
    $labelRect = [System.Drawing.RectangleF]::new($x - 130, $y + 218, 260, 70)
    Draw-CenteredText $actor.Label $labelRect $fontBold $black
}

function UseCase-Edge($rect, $side) {
    $cy = $rect.Y + ($rect.Height / 2)
    if ($side -eq "left") {
        return [System.Drawing.PointF]::new($rect.X + 3, $cy)
    }
    return [System.Drawing.PointF]::new($rect.Right - 3, $cy)
}

function Actor-Anchor($actor, $side, $offset = 0) {
    if ($side -eq "left") {
        return [System.Drawing.PointF]::new($actor.X + 58, $actor.Y + 82 + $offset)
    }
    return [System.Drawing.PointF]::new($actor.X - 58, $actor.Y + 82 + $offset)
}

function Draw-Association($actor, $uc, $side, $offset = 0) {
    $start = Actor-Anchor $actor $side $offset
    $end = UseCase-Edge $uc.Rect $side
    $direction = if ($side -eq "left") { 1 } else { -1 }
    $c1 = [System.Drawing.PointF]::new($start.X + ($direction * 130), $start.Y)
    $c2 = [System.Drawing.PointF]::new($end.X - ($direction * 130), $end.Y)
    $g.DrawBezier((New-Pen $lineColor 2), $start, $c1, $c2, $end)
}

function Draw-Include($from, $to, $label) {
    $pen = New-Pen $lightLine 2
    $pen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
    $start = [System.Drawing.PointF]::new($from.Rect.X + ($from.Rect.Width / 2), $from.Rect.Bottom)
    $end = [System.Drawing.PointF]::new($to.Rect.X + ($to.Rect.Width / 2), $to.Rect.Y)
    $g.DrawLine($pen, $start, $end)
    $labelRect = [System.Drawing.RectangleF]::new(($start.X + $end.X) / 2 - 65, ($start.Y + $end.Y) / 2 - 22, 130, 42)
    Draw-CenteredText $label $labelRect $fontSmall $black
}

$systemRect = [System.Drawing.RectangleF]::new(360, 120, 1880, 1600)
$g.DrawRectangle((New-Pen $black 2), $systemRect.X, $systemRect.Y, $systemRect.Width, $systemRect.Height)
Draw-CenteredText "PawConnect" ([System.Drawing.RectangleF]::new($systemRect.X, $systemRect.Y + 10, $systemRect.Width, 48)) $fontTitle $black

$actors = @{
    Public = @{ X = 160; Y = 315; Label = "Public Visitor" }
    Adopter = @{ X = 160; Y = 1030; Label = "Adopter" }
    Shelter = @{ X = 2460; Y = 335; Label = "Shelter Representative" }
    Admin = @{ X = 2460; Y = 1030; Label = "Administrator" }
}

$useCases = @{}
function Add-UC($key, $text, $x, $y, $w = 260, $h = 66) {
    $script:useCases[$key] = @{ Text = $text; Rect = [System.Drawing.RectangleF]::new($x, $y, $w, $h) }
}

Add-UC "Register" "Register / log in" 600 205 320 62
Add-UC "BrowseDogs" "Browse dogs" 600 315 320 62
Add-UC "FilterDogs" "Filter dogs" 600 425 320 62
Add-UC "DogDetails" "View dog details" 600 535 320 62
Add-UC "BrowseShelters" "Browse shelters and maps" 600 645 360 70
Add-UC "ShelterApply" "Submit shelter application" 600 770 360 70

Add-UC "Profile" "Manage adopter profile" 600 900 360 68
Add-UC "Favorites" "Save favorite dogs" 600 1015 340 62
Add-UC "Copilot" "Use AI Adoption Copilot" 600 1125 380 70
Add-UC "Recommendations" "View recommended dogs" 600 1245 370 70
Add-UC "AdoptionRequest" "Submit adoption request" 600 1365 370 70
Add-UC "TrackRequests" "Track / cancel requests" 600 1485 360 70
Add-UC "AdopterNotifications" "View notifications" 600 1605 340 62

Add-UC "ShelterProfile" "Manage shelter profile and location" 1600 205 440 70
Add-UC "ManageDogs" "Manage dogs" 1600 325 320 62
Add-UC "DogRecords" "Manage images and medical records" 1600 435 460 70
Add-UC "ReviewRequests" "Review adoption requests" 1600 555 390 70
Add-UC "ConfirmVisits" "Confirm visits / finalize adoption" 1600 675 440 70
Add-UC "ShelterExport" "Export shelter data" 1600 795 340 62
Add-UC "ReportHistory" "View report history" 1600 905 340 62
Add-UC "ShelterNotifications" "View notifications" 1600 1015 340 62

Add-UC "ApproveShelters" "Review shelter applications" 1600 1145 390 70
Add-UC "ManageUsers" "Manage users and platform data" 1600 1265 410 70
Add-UC "AdminReports" "View reports and activity logs" 1600 1385 420 70
Add-UC "RebuildIndex" "Rebuild dog search index" 1600 1505 380 70
Add-UC "SystemHealth" "Monitor system health" 1600 1625 360 70

$connections = @(
    @{ A = "Public"; U = "Register"; Side = "left"; Offset = -50 },
    @{ A = "Public"; U = "BrowseDogs"; Side = "left"; Offset = -20 },
    @{ A = "Public"; U = "FilterDogs"; Side = "left"; Offset = 0 },
    @{ A = "Public"; U = "DogDetails"; Side = "left"; Offset = 25 },
    @{ A = "Public"; U = "BrowseShelters"; Side = "left"; Offset = 50 },
    @{ A = "Public"; U = "ShelterApply"; Side = "left"; Offset = 78 },

    @{ A = "Adopter"; U = "Profile"; Side = "left"; Offset = -55 },
    @{ A = "Adopter"; U = "Favorites"; Side = "left"; Offset = -35 },
    @{ A = "Adopter"; U = "Copilot"; Side = "left"; Offset = -12 },
    @{ A = "Adopter"; U = "Recommendations"; Side = "left"; Offset = 12 },
    @{ A = "Adopter"; U = "AdoptionRequest"; Side = "left"; Offset = 35 },
    @{ A = "Adopter"; U = "TrackRequests"; Side = "left"; Offset = 58 },
    @{ A = "Adopter"; U = "AdopterNotifications"; Side = "left"; Offset = 80 },

    @{ A = "Shelter"; U = "ShelterProfile"; Side = "right"; Offset = -58 },
    @{ A = "Shelter"; U = "ManageDogs"; Side = "right"; Offset = -35 },
    @{ A = "Shelter"; U = "DogRecords"; Side = "right"; Offset = -12 },
    @{ A = "Shelter"; U = "ReviewRequests"; Side = "right"; Offset = 12 },
    @{ A = "Shelter"; U = "ConfirmVisits"; Side = "right"; Offset = 35 },
    @{ A = "Shelter"; U = "ShelterExport"; Side = "right"; Offset = 58 },
    @{ A = "Shelter"; U = "ReportHistory"; Side = "right"; Offset = 80 },
    @{ A = "Shelter"; U = "ShelterNotifications"; Side = "right"; Offset = 102 },

    @{ A = "Admin"; U = "ApproveShelters"; Side = "right"; Offset = -42 },
    @{ A = "Admin"; U = "ManageUsers"; Side = "right"; Offset = -18 },
    @{ A = "Admin"; U = "AdminReports"; Side = "right"; Offset = 10 },
    @{ A = "Admin"; U = "RebuildIndex"; Side = "right"; Offset = 38 },
    @{ A = "Admin"; U = "SystemHealth"; Side = "right"; Offset = 64 }
)

foreach ($connection in $connections) {
    Draw-Association $actors[$connection.A] $useCases[$connection.U] $connection.Side $connection.Offset
}

foreach ($uc in $useCases.Values) {
    Draw-UseCase $uc
}

foreach ($actor in $actors.Values) {
    Draw-Actor $actor
}

Draw-CenteredText "Figure 4.1: Use case diagram of the PawConnect application" ([System.Drawing.RectangleF]::new(0, 1765, $width, 70)) $fontCaption $black

$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()
Write-Host $outPath
