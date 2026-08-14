[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repositoryRoot "Branding\greenbox-i18n-icon.png"
$vectorSource = Join-Path $repositoryRoot "Branding\greenbox-i18n-icon.svg"
$webAsset = Join-Path $repositoryRoot "GreenBox.I18n.Editor.Web\public\greenbox-i18n-icon.png"
$desktopIcon = Join-Path $repositoryRoot "GreenBox.I18n.Desktop\Assets\greenbox-i18n.ico"
$unityDocumentationAsset = Join-Path $repositoryRoot "Unity\Packages\com.greenbox.i18n\Documentation~\greenbox-i18n-icon.png"
$unityComponentIconDirectory = Join-Path $repositoryRoot "Unity\Packages\com.greenbox.i18n\Editor\Assets\ComponentIcons"
$riderAssets = @(
    (Join-Path $repositoryRoot "Ide\Rider\src\main\resources\META-INF\pluginIcon.svg"),
    (Join-Path $repositoryRoot "Ide\Rider\src\main\resources\META-INF\pluginIcon_dark.svg")
)

if (!(Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "Canonical brand icon was not found at '$source'."
}
if (!(Test-Path -LiteralPath $vectorSource -PathType Leaf)) {
    throw "Canonical vector brand icon was not found at '$vectorSource'."
}

Add-Type -AssemblyName System.Drawing

function Convert-ToPngBytes {
    param(
        [System.Drawing.Image] $Image,
        [int] $Size
    )

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $bitmap.SetResolution(96, 96)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($Image, 0, 0, $Size, $Size)
        }
        finally {
            $graphics.Dispose()
        }

        $stream = [IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            return ,$stream.ToArray()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

$webDirectory = Split-Path -Parent $webAsset
$desktopDirectory = Split-Path -Parent $desktopIcon
$unityDocumentationDirectory = Split-Path -Parent $unityDocumentationAsset
New-Item -ItemType Directory -Path $webDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $desktopDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $unityDocumentationDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $unityComponentIconDirectory -Force | Out-Null
Copy-Item -LiteralPath $source -Destination $webAsset -Force
Copy-Item -LiteralPath $source -Destination $unityDocumentationAsset -Force

[xml]$riderIconDocument = Get-Content -LiteralPath $vectorSource -Raw
$riderIconRoot = $riderIconDocument.DocumentElement
$sourceViewBox = $riderIconRoot.GetAttribute("viewBox").Split(
    [char[]]@(' ', ','),
    [StringSplitOptions]::RemoveEmptyEntries)
if ($sourceViewBox.Count -ne 4) {
    throw "Canonical vector brand icon has an invalid viewBox."
}

$invariantCulture = [Globalization.CultureInfo]::InvariantCulture
$sourceX = [double]::Parse($sourceViewBox[0], $invariantCulture)
$sourceY = [double]::Parse($sourceViewBox[1], $invariantCulture)
$sourceWidth = [double]::Parse($sourceViewBox[2], $invariantCulture)
$sourceHeight = [double]::Parse($sourceViewBox[3], $invariantCulture)
$paddedSize = [Math]::Max($sourceWidth, $sourceHeight) / 0.9
$paddedX = $sourceX - (($paddedSize - $sourceWidth) / 2)
$paddedY = $sourceY - (($paddedSize - $sourceHeight) / 2)
$paddedXText = $paddedX.ToString("0.###", $invariantCulture)
$paddedYText = $paddedY.ToString("0.###", $invariantCulture)
$paddedSizeText = $paddedSize.ToString("0.###", $invariantCulture)

$riderIconRoot.SetAttribute("width", "40")
$riderIconRoot.SetAttribute("height", "40")
$riderIconRoot.SetAttribute(
    "viewBox",
    "$paddedXText $paddedYText $paddedSizeText $paddedSizeText")

$xmlWriterSettings = [Xml.XmlWriterSettings]::new()
$xmlWriterSettings.Encoding = [Text.UTF8Encoding]::new($false)
$xmlWriterSettings.Indent = $true
foreach ($riderAsset in $riderAssets) {
    $writer = [Xml.XmlWriter]::Create($riderAsset, $xmlWriterSettings)
    try {
        $riderIconDocument.Save($writer)
    }
    finally {
        $writer.Dispose()
    }
}

$sourceImage = [System.Drawing.Image]::FromFile($source)
try {
    $unityComponentIconSizes = @(16, 32, 64, 128, 256)
    foreach ($size in $unityComponentIconSizes) {
        $fileName = if ($size -eq 16) { "GreenBox.I18n.png" } else { "GreenBox.I18n@$size.png" }
        $iconPath = Join-Path $unityComponentIconDirectory $fileName
        [IO.File]::WriteAllBytes($iconPath, (Convert-ToPngBytes -Image $sourceImage -Size $size))
    }

    $sizes = @(16, 24, 32, 40, 48, 64, 128, 256)
    $images = [Collections.Generic.List[byte[]]]::new()
    foreach ($size in $sizes) {
        $images.Add((Convert-ToPngBytes -Image $sourceImage -Size $size))
    }

    $stream = [IO.File]::Open($desktopIcon, [IO.FileMode]::Create, [IO.FileAccess]::Write)
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$sizes.Count)

        $offset = 6 + (16 * $sizes.Count)
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $sizeByte = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
            $writer.Write([byte]$sizeByte)
            $writer.Write([byte]$sizeByte)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$images[$index].Length)
            $writer.Write([uint32]$offset)
            $offset += $images[$index].Length
        }

        foreach ($image in $images) {
            $writer.Write($image)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}
finally {
    $sourceImage.Dispose()
}

Write-Host "Brand assets generated from $source"
