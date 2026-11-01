<#
.SYNOPSIS
  Imports the original's eight fonts (gmlHARE's HARE.gmx font resources) into Assets/Unity.Rea.Hare/Fonts.
.DESCRIPTION
  GameMaker never ships a font file: at build time it rasterises each font resource once, at its size and style,
  into a glyph atlas (fonts/<name>.png, white glyphs on transparency) and a glyph table (fonts/<name>.font.gmx:
  each character's cell in the atlas, its advance "shift" and left bearing "offset"), and the game draws text from
  those. This script imports exactly that, never the typeface itself (Arial and Berlin Sans FB are not ours to
  redistribute; see the licences section of the manifest and docs/BIBLE.md).

  It reads HARE.project.gmx (which fonts are in the resource tree), every fonts/*.font.gmx (face name, size, bold,
  italic, charset, anti-aliasing, ranges, glyphs, kerning pairs, atlas image) and every object and script for its
  draw_set_font calls (outside comments), mirrors each atlas to Assets/Unity.Rea.Hare/Fonts/Atlases/<name>.png (copying
  only changed bytes, deleting atlases the original no longer has), and writes Assets/Unity.Rea.Hare/Fonts/gml-fonts.json:
  one entry per font with its fields, glyphs, atlas, the objects that draw with it and any notes, the licence of each
  typeface, and a skipped list.

  Unity then does the rest from that file: the editor's GmlFontBuilder copies each glyph cell out of its atlas onto a
  repacked Alpha8 atlas (clear gaps between cells) and builds one bitmap FontAsset per font under Resources/Hare/Fonts
  (what HareFonts and the Theme.uss text styles load). Unless -NoUnity is given this script runs GmlFontBuilder headless through
  tools/unity.ps1 once the files are in place.

  Running it twice with the same source changes nothing.
.EXAMPLE
  powershell -File tools/import-gml-fonts.ps1
.EXAMPLE
  powershell -File tools/import-gml-fonts.ps1 -Source D:\copy\of\HARE.gmx -NoUnity
#>
param(
    [string]$Source = 'R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlHARE\HARE.gmx',
    [switch]$NoUnity
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2
. (Join-Path $PSScriptRoot 'gml-json.ps1')

$root = Split-Path -Parent $PSScriptRoot
$fontAssets = 'Assets/Unity.Rea.Hare/Fonts'
$fontDir = Join-Path $root $fontAssets
$atlasDir = Join-Path $fontDir 'Atlases'
$manifestPath = Join-Path $fontDir 'gml-fonts.json'

if (-not (Test-Path (Join-Path $Source 'HARE.project.gmx'))) { throw "No HARE.project.gmx under $Source" }

function Read-Xml([string]$path) {
    $doc = New-Object Xml.XmlDocument
    $doc.Load($path)
    return $doc
}

# A PNG's width and height from its IHDR chunk.
function Png-Size([string]$path) {
    $b = [IO.File]::ReadAllBytes($path)
    if ($b.Length -lt 24 -or $b[1] -ne 0x50 -or $b[2] -ne 0x4E -or $b[3] -ne 0x47) { return $null }
    $w = ([int]$b[16] -shl 24) -bor ([int]$b[17] -shl 16) -bor ([int]$b[18] -shl 8) -bor [int]$b[19]
    $h = ([int]$b[20] -shl 24) -bor ([int]$b[21] -shl 16) -bor ([int]$b[22] -shl 8) -bor [int]$b[23]
    return @($w, $h)
}

# The typefaces the font resources name, and what their licences allow (checked for U.HARE-T48).
$licences = @(
    [ordered]@{
        family = 'Arial'
        owner = 'Monotype (designed by Robin Nicholas and Patricia Saunders, 1982); licensed to Microsoft, which ships it with Windows'
        licence = 'Proprietary. The Windows licence lets the font be used on that machine and embedded in documents, not redistributed as a font file.'
        rebuild = 'Not shipped. Only GameMaker''s rasterised atlases of it (fnt_basic, font4, font7, font8, all byte-identical bakes of Arial 12) are imported, which is what the original game itself shipped.'
    },
    [ordered]@{
        family = 'Berlin Sans FB'
        owner = 'The Font Bureau (designed by David Berlow, 1994, after Lucian Bernhard); licensed to Microsoft, which ships it with Microsoft Office'
        licence = 'Proprietary. Installed with Office for use on that machine; the font file may not be redistributed.'
        rebuild = 'Not shipped. Only GameMaker''s rasterised atlases of it (fnt_medium, fnt_point, fnt_score, fnt_small) are imported, which is what the original game itself shipped.'
    }
)

# --- the resource tree -----------------------------------------------------------------------------
$project = Read-Xml (Join-Path $Source 'HARE.project.gmx')
$inTree = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($node in $project.SelectNodes('//fonts//font')) { [void]$inTree.Add(($node.InnerText -replace '^fonts\\', '')) }

# --- who draws with which font: every draw_set_font(<font>) outside a comment -----------------------
function Strip-Comments([string]$code) {
    $code = [regex]::Replace($code, '/\*.*?\*/', '', 'Singleline')
    return [regex]::Replace($code, '//[^\r\n]*', '')
}
$usedBy = @{}
function Note-Uses([string]$owner, [string]$code) {
    foreach ($m in [regex]::Matches((Strip-Comments $code), 'draw_set_font\s*\(\s*([A-Za-z_][A-Za-z0-9_]*)\s*\)')) {
        $font = $m.Groups[1].Value
        if (-not $usedBy.ContainsKey($font)) { $usedBy[$font] = New-Object 'Collections.Generic.SortedSet[string]' ([StringComparer]::Ordinal) }
        [void]$usedBy[$font].Add($owner)
    }
}
foreach ($file in Get-ChildItem (Join-Path $Source 'objects') -Filter '*.object.gmx') {
    $owner = $file.Name -replace '\.object\.gmx$', ''
    foreach ($s in (Read-Xml $file.FullName).SelectNodes('//argument/string')) { Note-Uses $owner $s.InnerText }
}
foreach ($file in Get-ChildItem (Join-Path $Source 'scripts') -Filter '*.gml') {
    Note-Uses ($file.Name -replace '\.gml$', '') ([IO.File]::ReadAllText($file.FullName))
}

# --- mirror files ------------------------------------------------------------------------------------
$written = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$copied = 0
function Mirror([string]$from, [string]$assetPath) {
    $to = Join-Path $root $assetPath
    [void]$written.Add([IO.Path]::GetFullPath($to))
    if (Test-Path $to) {
        $a = Get-Item $from; $b = Get-Item $to
        if ($a.Length -eq $b.Length -and (Get-FileHash $from).Hash -eq (Get-FileHash $to).Hash) { return }
    }
    New-Item -ItemType Directory -Force (Split-Path -Parent $to) | Out-Null
    Copy-Item $from $to -Force
    $script:copied++
}

# --- fonts -------------------------------------------------------------------------------------------
$fonts = @(); $skipped = @()
$atlasByHash = @{}
foreach ($file in Get-ChildItem (Join-Path $Source 'fonts') -Filter '*.font.gmx' | Sort-Object Name) {
    $name = $file.Name -replace '\.font\.gmx$', ''
    $f = (Read-Xml $file.FullName).font
    $image = [string]$f.image
    $png = Join-Path (Join-Path $Source 'fonts') $image
    if (-not $image -or -not (Test-Path $png)) { $skipped += [ordered]@{ name = $name; kind = 'font'; reason = "atlas fonts\$image is missing" }; continue }
    $size = Png-Size $png
    if ($size -eq $null) { $skipped += [ordered]@{ name = $name; kind = 'font'; reason = "fonts\$image is not a PNG" }; continue }

    $glyphs = @($f.glyphs.glyph | Sort-Object { [int]$_.character } | ForEach-Object {
        [ordered]@{
            character = [int]$_.character
            x = [int]$_.x; y = [int]$_.y; w = [int]$_.w; h = [int]$_.h
            shift = [int]$_.shift; offset = [int]$_.offset
        }
    })
    $bad = @($glyphs | Where-Object { $_.x -lt 0 -or $_.y -lt 0 -or $_.x + $_.w -gt $size[0] -or $_.y + $_.h -gt $size[1] })
    if ($bad.Count -gt 0) { $skipped += [ordered]@{ name = $name; kind = 'font'; reason = "$($bad.Count) glyph cells fall outside the $($size[0])x$($size[1]) atlas" }; continue }
    $kerning = @(foreach ($k in $f.SelectNodes('kerningPairs/pair')) {
        [ordered]@{ first = [int]$k.GetAttribute('first'); second = [int]$k.GetAttribute('second'); amount = [int]$k.GetAttribute('amount') }
    })
    $ranges = @(foreach ($r in $f.ranges.ChildNodes) { if ($r.NodeType -eq 'Element') { [string]$r.InnerText } })

    $notes = @()
    # Same atlas bytes and same glyph table (in character order) as an earlier font: a duplicate bake.
    $hash = (Get-FileHash $png -Algorithm SHA256).Hash + '|' + (To-Json $glyphs '')
    if ($atlasByHash.ContainsKey($hash)) { $notes += "atlas and glyph table are a byte-identical bake of $($atlasByHash[$hash])'s ($($f.name) $([int]$f.size))" }
    else { $atlasByHash[$hash] = $name }
    $inResourceTree = $inTree.Contains($name)
    if (-not $inResourceTree) { $notes += 'on disk under fonts\ but not in HARE.project.gmx, so GameMaker never built it into the game; imported anyway so the eight face names all resolve' }
    $users = @(if ($usedBy.ContainsKey($name)) { $usedBy[$name] })
    if ($users.Count -eq 0) { $notes += 'no object or script calls draw_set_font with it' }

    $asset = "$fontAssets/Atlases/$name.png"
    Mirror $png $asset

    $fonts += [ordered]@{
        name = $name
        inResourceTree = $inResourceTree
        family = [string]$f.name
        size = [int]$f.size
        bold = ([int]$f.bold -ne 0)
        italic = ([int]$f.italic -ne 0)
        charset = [int]$f.charset
        antialias = [int]$f.aa
        ranges = $ranges
        image = $image
        atlas = $asset
        atlasWidth = $size[0]
        atlasHeight = $size[1]
        usedBy = $users
        notes = $notes
        kerning = $kerning
        glyphs = $glyphs
    }
}
foreach ($n in $inTree) {
    if (-not ($fonts | Where-Object { $_.name -eq $n }) -and -not ($skipped | Where-Object { $_.name -eq $n })) {
        $skipped += [ordered]@{ name = $n; kind = 'font'; reason = "listed in HARE.project.gmx but fonts\$n.font.gmx is missing" }
    }
}
foreach ($k in $usedBy.Keys) {
    if (-not ($fonts | Where-Object { $_.name -eq $k })) {
        $skipped += [ordered]@{ name = $k; kind = 'draw_set_font'; reason = "drawn with by $(@($usedBy[$k]) -join ', ') but no such font resource" }
    }
}

# --- delete what the original no longer has ----------------------------------------------------------
$removed = 0
if (Test-Path $atlasDir) {
    foreach ($f in Get-ChildItem $atlasDir -File | Where-Object { $_.Extension -ne '.meta' }) {
        if (-not $written.Contains($f.FullName)) {
            Remove-Item $f.FullName -Force
            if (Test-Path "$($f.FullName).meta") { Remove-Item "$($f.FullName).meta" -Force }
            $removed++
        }
    }
}

# --- the manifest ------------------------------------------------------------------------------------
$manifest = [ordered]@{
    source = 'gmlHARE HARE.gmx (GameMaker 8.1 / Studio 1.x project), read by tools/import-gml-fonts.ps1'
    licences = $licences
    fonts = $fonts
    skipped = $skipped
}
New-Item -ItemType Directory -Force $fontDir | Out-Null
$json = (To-Json $manifest '') + "`n"
$old = if (Test-Path $manifestPath) { [IO.File]::ReadAllText($manifestPath) } else { '' }
if ($old -ne $json) { [IO.File]::WriteAllText($manifestPath, $json, (New-Object Text.UTF8Encoding $false)) }

Write-Host "[Unity.Rea.Hare] GML fonts: $($fonts.Count) fonts, $($skipped.Count) skipped; $copied atlases copied, $removed removed; manifest $(if ($old -ne $json) { 'updated' } else { 'unchanged' })"
foreach ($f in $fonts) { Write-Host ("  {0,-10} {1} {2}{3}{4}, {5} glyphs, {6}x{7}; used by {8}" -f $f.name, $f.family, $f.size, $(if ($f.bold) { ' bold' } else { '' }), $(if ($f.italic) { ' italic' } else { '' }), $f.glyphs.Count, $f.atlasWidth, $f.atlasHeight, $(if ($f.usedBy.Count) { $f.usedBy.Count.ToString() + ' objects' } else { 'nothing' })) }
foreach ($s in $skipped) { Write-Host "  skipped $($s.kind) $($s.name): $($s.reason)" }

if (-not $NoUnity) {
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'unity.ps1') -Method Unity.Rea.Hare.EditorTools.GmlFontBuilder.BuildAll -Log fonts
    exit $LASTEXITCODE
}
