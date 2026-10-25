<#
.SYNOPSIS
  Imports the original's art (gmlHARE's HARE.gmx sprites and backgrounds) into Assets/Unity.Hare/Art.
.DESCRIPTION
  Reads HARE.project.gmx (the resource tree, so every sprite keeps its GameMaker group), every
  sprites/*.sprite.gmx (frames, origin, bounding box, collision-mask shape) and background/*.background.gmx
  (image, tiling), and works out each sprite's image_speed from the objects that show it (the first
  image_speed = <number> in the Create event of every object whose sprite it is) and the room_speed it plays
  at (the speeds of every room from which those objects are reachable, by placement or by being named in the
  code of something already reachable).

  It mirrors the frame images into Assets/Unity.Hare/Art/Sprites/<group>/<sprite>/<sprite>_<frame>.png and the
  backgrounds into Assets/Unity.Hare/Art/Backgrounds/<background>.png (copying only files whose bytes changed,
  deleting any the original no longer has), and writes Assets/Unity.Hare/Art/gml-art.json: one entry per GML
  sprite and background with its source data and the Unity assets it becomes (frames, animation clip, sprite
  atlas), plus every sprite that was not imported and why.

  Unity then does the rest from that file: the editor's GmlArtImporter sets each texture's import settings
  (pivot on the GML origin, pixels per unit, filtering, tiling) and GmlArtBuilder writes the animation clips,
  sprite atlases and the runtime HareArtManifest. Unless -NoUnity is given this script runs GmlArtBuilder
  headless through tools/unity.ps1 once the files are in place.

  Running it twice with the same source changes nothing.
.EXAMPLE
  powershell -File tools/import-gml-art.ps1
.EXAMPLE
  powershell -File tools/import-gml-art.ps1 -Source D:\copy\of\HARE.gmx -NoUnity
#>
param(
    [string]$Source = 'R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlHARE\HARE.gmx',
    [switch]$NoUnity
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2
$inv = [Globalization.CultureInfo]::InvariantCulture

$root = Split-Path -Parent $PSScriptRoot
$artAssets = 'Assets/Unity.Hare/Art'
$artDir = Join-Path $root $artAssets
$manifestPath = Join-Path $artDir 'gml-art.json'

# Unity-side constants the manifest records so the editor reads them from data, not code.
$pixelsPerUnit = 100          # GameClock.PixelsPerUnit
$defaultRoomSpeed = 120       # room_gamescreen, the rooms where play happens
$atlasMaxSide = 1024          # a sprite wider or taller than this keeps its own texture
$atlasPrefix = 'Sprites_'

if (-not (Test-Path (Join-Path $Source 'HARE.project.gmx'))) { throw "No HARE.project.gmx under $Source" }

function Read-Xml([string]$path) {
    $doc = New-Object Xml.XmlDocument
    $doc.Load($path)
    return $doc
}

function Png-Size([string]$path) {
    $fs = [IO.File]::OpenRead($path)
    try {
        $b = New-Object byte[] 24
        if ($fs.Read($b, 0, 24) -ne 24) { return $null }
        if ($b[0] -ne 0x89 -or $b[1] -ne 0x50 -or $b[12] -ne 0x49 -or $b[13] -ne 0x48) { return $null }
        $w = ([int]$b[16] -shl 24) -bor ([int]$b[17] -shl 16) -bor ([int]$b[18] -shl 8) -bor [int]$b[19]
        $h = ([int]$b[20] -shl 24) -bor ([int]$b[21] -shl 16) -bor ([int]$b[22] -shl 8) -bor [int]$b[23]
        return @($w, $h)
    } finally { $fs.Dispose() }
}

# --- the resource tree: every sprite and background with its GameMaker group --------------------------
$project = Read-Xml (Join-Path $Source 'HARE.project.gmx')
function Group-Of([Xml.XmlNode]$leaf, [string]$container) {
    $parts = @()
    $n = $leaf.ParentNode
    while ($n -ne $null -and $n.LocalName -eq $container) {
        $parts = @($n.GetAttribute('name')) + $parts
        $n = $n.ParentNode
    }
    # The top folder is GameMaker's own ("sprites", "background"); the groups under it are the author's.
    if ($parts.Count -gt 0) { $parts = @($parts | Select-Object -Skip 1) }
    return ($parts -join '/')
}
$spriteList = @(foreach ($node in $project.SelectNodes('//sprites//sprite')) {
    [pscustomobject]@{ Name = ($node.InnerText -replace '^sprites\\', ''); Group = (Group-Of $node 'sprites') }
})
$backgroundList = @(foreach ($node in $project.SelectNodes('//backgrounds//background')) {
    [pscustomobject]@{ Name = ($node.InnerText -replace '^background\\', ''); Group = (Group-Of $node 'backgrounds') }
})

# --- which room speed each object runs at, and which image_speed each object's Create sets -------------
$objectCode = @{}; $objectSprite = @{}; $objectCreateSpeed = @{}
foreach ($file in Get-ChildItem (Join-Path $Source 'objects') -Filter '*.object.gmx') {
    $name = $file.Name -replace '\.object\.gmx$', ''
    $doc = Read-Xml $file.FullName
    $objectSprite[$name] = $doc.object.spriteName
    $objectCode[$name] = (@($doc.SelectNodes('//string') | ForEach-Object { $_.InnerText }) -join "`n")
    $create = $doc.SelectSingleNode("//event[@eventtype='0' and @enumb='0']")
    if ($create -ne $null) {
        $code = (@($create.SelectNodes('.//string') | ForEach-Object { $_.InnerText }) -join "`n")
        $m = [regex]::Match($code, '(?m)^[ \t]*image_speed[ \t]*=[ \t]*([0-9]*\.?[0-9]+)[ \t]*;')
        if ($m.Success) { $objectCreateSpeed[$name] = [double]::Parse($m.Groups[1].Value, $inv) }
    }
}
$scriptCode = @{}
foreach ($file in Get-ChildItem (Join-Path $Source 'scripts') -Filter '*.gml') {
    $scriptCode[$file.BaseName] = [IO.File]::ReadAllText($file.FullName)
}
function Mentions([string]$code) {
    $found = New-Object 'Collections.Generic.HashSet[string]'
    foreach ($m in [regex]::Matches($code, '\b[A-Za-z_][A-Za-z0-9_]*\b')) {
        $id = $m.Value
        if ($objectCode.ContainsKey($id) -or $scriptCode.ContainsKey($id)) { [void]$found.Add($id) }
    }
    return $found
}
$edges = @{}
foreach ($k in $objectCode.Keys) { $edges[$k] = Mentions $objectCode[$k] }
foreach ($k in $scriptCode.Keys) { $edges[$k] = Mentions $scriptCode[$k] }

$objectRoomSpeeds = @{}
foreach ($file in Get-ChildItem (Join-Path $Source 'rooms') -Filter '*.room.gmx') {
    $doc = Read-Xml $file.FullName
    $speed = [int]$doc.room.speed
    $start = New-Object 'Collections.Generic.HashSet[string]'
    foreach ($i in $doc.SelectNodes('//instance')) {
        $o = $i.GetAttribute('objName')
        if ($objectCode.ContainsKey($o)) { [void]$start.Add($o) }
        foreach ($id in (Mentions $i.GetAttribute('code'))) { [void]$start.Add($id) }
    }
    $roomCode = $doc.SelectSingleNode('/room/code')
    if ($roomCode -ne $null) { foreach ($id in (Mentions $roomCode.InnerText)) { [void]$start.Add($id) } }
    $seen = New-Object 'Collections.Generic.HashSet[string]'
    $queue = New-Object 'Collections.Generic.Queue[string]'
    foreach ($s in $start) { if ($seen.Add($s)) { $queue.Enqueue($s) } }
    while ($queue.Count -gt 0) {
        foreach ($next in $edges[$queue.Dequeue()]) { if ($seen.Add($next)) { $queue.Enqueue($next) } }
    }
    foreach ($s in $seen) {
        if (-not $objectCode.ContainsKey($s)) { continue }
        if (-not $objectRoomSpeeds.ContainsKey($s)) { $objectRoomSpeeds[$s] = New-Object 'Collections.Generic.SortedSet[int]' }
        [void]$objectRoomSpeeds[$s].Add($speed)
    }
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
function Join-Asset([string[]]$parts) { return (@($parts | Where-Object { $_ }) -join '/') }

# --- sprites -----------------------------------------------------------------------------------------
$sprites = @(); $skipped = @()
foreach ($entry in $spriteList) {
    $name = $entry.Name
    $file = Join-Path $Source "sprites\$name.sprite.gmx"
    if (-not (Test-Path $file)) { $skipped += [ordered]@{ name = $name; kind = 'sprite'; reason = "listed in HARE.project.gmx but sprites\$name.sprite.gmx is missing" }; continue }
    $s = (Read-Xml $file).sprite
    $width = [int]$s.width; $height = [int]$s.height
    $frameNodes = @($s.SelectNodes('frames/frame') | Sort-Object { [int]$_.GetAttribute('index') })
    if ($frameNodes.Count -eq 0) { $skipped += [ordered]@{ name = $name; kind = 'sprite'; reason = 'the sprite has no frames' }; continue }
    $problem = $null
    foreach ($f in $frameNodes) {
        $img = Join-Path (Join-Path $Source 'sprites') $f.InnerText
        if (-not (Test-Path $img)) { $problem = "frame image $($f.InnerText) is missing"; break }
        $size = Png-Size $img
        if ($size -eq $null) { $problem = "frame image $($f.InnerText) is not a PNG"; break }
        if ($size[0] -ne $width -or $size[1] -ne $height) { $problem = "frame image $($f.InnerText) is $($size[0])x$($size[1]), not the sprite's ${width}x${height}"; break }
    }
    if ($problem) { $skipped += [ordered]@{ name = $name; kind = 'sprite'; reason = $problem }; continue }

    $folder = Join-Asset @("$artAssets/Sprites", $entry.Group, $name)
    $frames = @()
    for ($i = 0; $i -lt $frameNodes.Count; $i++) {
        $asset = "$folder/${name}_$i.png"
        Mirror (Join-Path (Join-Path $Source 'sprites') $frameNodes[$i].InnerText) $asset
        $frames += $asset
    }

    # image_speed: every object showing this sprite whose Create sets one; GameMaker's default 1 otherwise.
    $owners = @($objectSprite.Keys | Where-Object { $objectSprite[$_] -eq $name } | Sort-Object)
    $speedOwners = @($owners | Where-Object { $objectCreateSpeed.ContainsKey($_) })
    $notes = @()
    if ($speedOwners.Count -gt 0) {
        $imageSpeed = $objectCreateSpeed[$speedOwners[0]]
        $distinct = @($speedOwners | ForEach-Object { $objectCreateSpeed[$_] } | Sort-Object -Unique)
        if ($distinct.Count -gt 1) { $notes += "objects disagree on image_speed ($(($speedOwners | ForEach-Object { "$_=$($objectCreateSpeed[$_].ToString($inv))" }) -join ', ')); the clip uses $($speedOwners[0])'s" }
        $speedSource = ($speedOwners | ForEach-Object { "$_ Create" }) -join ', '
    } else {
        $imageSpeed = 1.0
        $speedSource = 'GameMaker default (no object showing it sets image_speed in Create)'
    }
    $speeds = New-Object 'Collections.Generic.SortedSet[int]'
    foreach ($o in $(if ($speedOwners.Count -gt 0) { $speedOwners } else { $owners })) {
        if ($objectRoomSpeeds.ContainsKey($o)) { foreach ($v in $objectRoomSpeeds[$o]) { [void]$speeds.Add($v) } }
    }
    $clip = ''
    if ($frameNodes.Count -gt 1 -and $imageSpeed -gt 0) { $clip = (Join-Asset @("$artAssets/Animations", $entry.Group, "$name.anim")) }

    if ($speeds.Count -eq 1) { $roomSpeed = @($speeds)[0] }
    else {
        # Only an animation cares about room speed; reachability is coarse (a name in code counts), so it
        # falls back to the combat rooms' speed, which is where every such animated sprite actually plays.
        $roomSpeed = $defaultRoomSpeed
        if ($speeds.Count -gt 1 -and $clip) { $notes += "reachable from rooms of more than one speed ($(@($speeds) -join ', ')); the clip uses $defaultRoomSpeed" }
    }

    $groupParts = @($entry.Group -split '/' | Where-Object { $_ })
    $atlas = ''
    if ($width -gt $atlasMaxSide -or $height -gt $atlasMaxSide) {
        $notes += "${width}x$height is larger than $atlasMaxSide px on a side, so it keeps its own texture instead of joining a sprite atlas"
    } else {
        $key = if ($groupParts.Count -eq 0) { 'root' } else { (@($groupParts | Select-Object -First 2)) -join '_' }
        $atlas = "$artAssets/Atlases/$atlasPrefix$key.spriteatlasv2"
    }

    $sprites += [ordered]@{
        name = $name; group = $entry.Group
        width = $width; height = $height
        xorig = [int]$s.xorig; yorig = [int]$s.yorigin
        colkind = [int]$s.colkind; coltolerance = [int]$s.coltolerance; sepmasks = ([int]$s.sepmasks -ne 0)
        bboxmode = [int]$s.bboxmode
        bboxLeft = [int]$s.bbox_left; bboxTop = [int]$s.bbox_top; bboxRight = [int]$s.bbox_right; bboxBottom = [int]$s.bbox_bottom
        tileHorizontal = ([int]$s.HTile -ne 0); tileVertical = ([int]$s.VTile -ne 0)
        imageSpeed = $imageSpeed; imageSpeedSource = $speedSource; roomSpeed = $roomSpeed
        frames = $frames; clip = $clip; atlas = $atlas
        notes = $notes
    }
}
foreach ($file in Get-ChildItem (Join-Path $Source 'sprites') -Filter '*.sprite.gmx') {
    $name = $file.Name -replace '\.sprite\.gmx$', ''
    if (-not ($spriteList | Where-Object { $_.Name -eq $name })) {
        $skipped += [ordered]@{ name = $name; kind = 'sprite'; reason = 'on disk but not in HARE.project.gmx, so not part of the game' }
    }
}

# --- backgrounds -------------------------------------------------------------------------------------
$backgrounds = @()
foreach ($entry in $backgroundList) {
    $name = $entry.Name
    $file = Join-Path $Source "background\$name.background.gmx"
    if (-not (Test-Path $file)) { $skipped += [ordered]@{ name = $name; kind = 'background'; reason = "listed in HARE.project.gmx but background\$name.background.gmx is missing" }; continue }
    $b = (Read-Xml $file).background
    $img = Join-Path (Join-Path $Source 'background') $b.data
    $size = if (Test-Path $img) { Png-Size $img } else { $null }
    if ($size -eq $null) { $skipped += [ordered]@{ name = $name; kind = 'background'; reason = "image $($b.data) is missing or not a PNG" }; continue }
    $asset = (Join-Asset @("$artAssets/Backgrounds", $entry.Group, "$name.png"))
    Mirror $img $asset
    $backgrounds += [ordered]@{
        name = $name; group = $entry.Group
        width = $size[0]; height = $size[1]
        tileHorizontal = ([int]$b.HTile -ne 0); tileVertical = ([int]$b.VTile -ne 0)
        tileset = ([int]$b.istileset -ne 0); tileWidth = [int]$b.tilewidth; tileHeight = [int]$b.tileheight
        texture = $asset
    }
}

# --- delete what the original no longer has ----------------------------------------------------------
$removed = 0
foreach ($sub in 'Sprites', 'Backgrounds') {
    $dir = Join-Path $artDir $sub
    if (-not (Test-Path $dir)) { continue }
    foreach ($f in Get-ChildItem $dir -Recurse -File -Filter '*.png') {
        if (-not $written.Contains($f.FullName)) {
            Remove-Item $f.FullName -Force
            if (Test-Path "$($f.FullName).meta") { Remove-Item "$($f.FullName).meta" -Force }
            $removed++
        }
    }
    foreach ($d in @(Get-ChildItem $dir -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending)) {
        if (@(Get-ChildItem $d.FullName -Force).Count -eq 0) {
            Remove-Item $d.FullName -Force
            if (Test-Path "$($d.FullName).meta") { Remove-Item "$($d.FullName).meta" -Force }
        }
    }
}

# --- the manifest ------------------------------------------------------------------------------------
function To-Json($value, [string]$indent) {
    $inner = $indent + '  '
    if ($value -eq $null) { return 'null' }
    if ($value -is [bool]) { return $(if ($value) { 'true' } else { 'false' }) }
    if ($value -is [int] -or $value -is [long]) { return $value.ToString($inv) }
    if ($value -is [double] -or $value -is [single]) { return ([double]$value).ToString('R', $inv) }
    if ($value -is [string]) {
        $sb = New-Object Text.StringBuilder
        foreach ($c in $value.ToCharArray()) {
            switch ($c) {
                '"' { [void]$sb.Append('\"') } '\' { [void]$sb.Append('\\') }
                "`n" { [void]$sb.Append('\n') } "`r" { [void]$sb.Append('\r') } "`t" { [void]$sb.Append('\t') }
                default { if ([int]$c -lt 32) { [void]$sb.AppendFormat('\u{0:x4}', [int]$c) } else { [void]$sb.Append($c) } }
            }
        }
        return '"' + $sb.ToString() + '"'
    }
    if ($value -is [Collections.IDictionary]) {
        if ($value.Count -eq 0) { return '{}' }
        $items = foreach ($k in $value.Keys) { $inner + '"' + $k + '": ' + (To-Json $value[$k] $inner) }
        return "{`n" + ($items -join ",`n") + "`n$indent}"
    }
    if ($value -is [Collections.IEnumerable]) {
        $list = @($value)
        if ($list.Count -eq 0) { return '[]' }
        if (-not ($list | Where-Object { $_ -is [Collections.IDictionary] })) {
            return '[' + ((@($list | ForEach-Object { To-Json $_ $inner })) -join ', ') + ']'
        }
        $items = foreach ($v in $list) { $inner + (To-Json $v $inner) }
        return "[`n" + ($items -join ",`n") + "`n$indent]"
    }
    throw "Cannot write $($value.GetType()) as JSON"
}
$manifest = [ordered]@{
    source = 'gmlHARE HARE.gmx (GameMaker 8.1 / Studio 1.x project), read by tools/import-gml-art.ps1'
    pixelsPerUnit = $pixelsPerUnit
    defaultRoomSpeed = $defaultRoomSpeed
    atlasMaxSide = $atlasMaxSide
    sprites = $sprites
    backgrounds = $backgrounds
    skipped = $skipped
}
New-Item -ItemType Directory -Force $artDir | Out-Null
$json = (To-Json $manifest '') + "`n"
$old = if (Test-Path $manifestPath) { [IO.File]::ReadAllText($manifestPath) } else { '' }
if ($old -ne $json) { [IO.File]::WriteAllText($manifestPath, $json, (New-Object Text.UTF8Encoding $false)) }

$frameCount = ($sprites | ForEach-Object { $_.frames.Count } | Measure-Object -Sum).Sum
Write-Host "[Unity.Hare] GML art: $($sprites.Count) sprites ($frameCount frames), $($backgrounds.Count) backgrounds, $($skipped.Count) skipped; $copied files copied, $removed removed; manifest $(if ($old -ne $json) { 'updated' } else { 'unchanged' })"
foreach ($s in $skipped) { Write-Host "  skipped $($s.kind) $($s.name): $($s.reason)" }

if (-not $NoUnity) {
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'unity.ps1') -Method Unity.Hare.EditorTools.GmlArtBuilder.BuildAll -Log art
    exit $LASTEXITCODE
}
