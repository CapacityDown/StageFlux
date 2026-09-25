param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetRoot = Join-Path $ProjectRoot 'Assets/EventIcons'
$manifest = Get-Content -LiteralPath (Join-Path $assetRoot 'v4.3.0-generation.json') -Raw | ConvertFrom-Json
foreach ($asset in $manifest.assets) {
    if ($asset.name -notmatch '^[A-Za-z]+$') { throw 'Invalid asset name' }
    $masterPath = Join-Path $assetRoot ('masters/' + $asset.name + '.png')
    $runtimePath = Join-Path $assetRoot ('Runtime/' + $asset.name + '.png')
    Copy-Item -LiteralPath $asset.source -Destination $masterPath -Force
    $sourceImage = [System.Drawing.Bitmap]::FromFile($masterPath)
    try {
        if ($sourceImage.GetPixel(0, 0).A -ne 0) { throw "Expected transparency: $($asset.name)" }
        # Package the generated artwork at HUD resolution; preserve its alpha and orientation.
        $runtimeImage = [System.Drawing.Bitmap]::new(512, 512, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($runtimeImage)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($sourceImage, [System.Drawing.Rectangle]::new(0, 0, 512, 512))
            $runtimeImage.Save($runtimePath, [System.Drawing.Imaging.ImageFormat]::Png)
        } finally {
            $graphics.Dispose()
            $runtimeImage.Dispose()
        }
        Write-Output "$($asset.name): transparent 512x512 runtime icon imported"
    } finally { $sourceImage.Dispose() }
}
