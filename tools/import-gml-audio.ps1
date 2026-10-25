<#
.SYNOPSIS
  Imports the original's sound (gmlHARE's HARE.gmx sound resources and its credits data file) into
  Assets/Unity.Hare/Audio.
.DESCRIPTION
  Reads HARE.project.gmx (the resource tree, so every sound keeps its GameMaker group: music, player, vulcan or
  none) and every sound/*.sound.gmx (kind, file, volume, pan, compressed/streamed/uncompress-on-load, target type,
  sample rate, bit rate and depth, preload), and checks each one's audio file under sound/audio (its extension,
  and for a WAV its format, channels, sample rate and length from the RIFF header).

  It mirrors the audio files into Assets/Unity.Hare/Audio/Sounds/<group>/<sound><ext> and the original's data
  files (datafiles/credits.txt, the sound credits) into Assets/Unity.Hare/Audio/, copying only files whose bytes
  changed and deleting any the original no longer has, and writes Assets/Unity.Hare/Audio/gml-audio.json: one entry
  per GML sound with its sound.gmx fields, its source file's facts and the Unity clip it becomes, the data files,
  and every file or resource that was not imported and why.

  Unity then does the rest from that file: the editor's GmlAudioImporter sets each clip's import settings (load
  type, format, mono, preload, from Core's GmlSoundSettings) and GmlAudioBuilder writes the AudioMixer
  (Master > Music, Sfx) and the runtime HareAudioManifest. Unless -NoUnity is given this script runs
  GmlAudioBuilder headless through tools/unity.ps1 once the files are in place.

  Running it twice with the same source changes nothing.
.EXAMPLE
  powershell -File tools/import-gml-audio.ps1
.EXAMPLE
  powershell -File tools/import-gml-audio.ps1 -Source D:\copy\of\HARE.gmx -NoUnity
#>
param(
    [string]$Source = 'R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlHARE\HARE.gmx',
    [switch]$NoUnity
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2
. (Join-Path $PSScriptRoot 'gml-json.ps1')

$root = Split-Path -Parent $PSScriptRoot
$audioAssets = 'Assets/Unity.Hare/Audio'
$audioDir = Join-Path $root $audioAssets
$manifestPath = Join-Path $audioDir 'gml-audio.json'

# Unity-side constants the manifest records so the editor reads them from data, not code.
# GameMaker encoded its compressed sounds at 192 kbps; Unity's Vorbis quality 0.7 is its nearest setting.
$vorbisQuality = 0.7

if (-not (Test-Path (Join-Path $Source 'HARE.project.gmx'))) { throw "No HARE.project.gmx under $Source" }

function Read-Xml([string]$path) {
    $doc = New-Object Xml.XmlDocument
    $doc.Load($path)
    return $doc
}

# A WAV's fmt and data chunks: format tag, channels, sample rate, bits per sample, length in seconds; $null if not a WAV.
function Wav-Info([string]$path) {
    $fs = [IO.File]::OpenRead($path)
    try {
        $r = New-Object IO.BinaryReader $fs
        if ($fs.Length -lt 12) { return $null }
        $riff = [Text.Encoding]::ASCII.GetString($r.ReadBytes(4)); [void]$r.ReadUInt32()
        $wave = [Text.Encoding]::ASCII.GetString($r.ReadBytes(4))
        if ($riff -ne 'RIFF' -or $wave -ne 'WAVE') { return $null }
        $fmt = $null; $dataBytes = -1
        while ($fs.Position + 8 -le $fs.Length) {
            $id = [Text.Encoding]::ASCII.GetString($r.ReadBytes(4)); $size = [int64]$r.ReadUInt32()
            $next = $fs.Position + $size + ($size % 2)
            if ($id -eq 'fmt ') {
                $fmt = [ordered]@{ format = [int]$r.ReadUInt16(); channels = [int]$r.ReadUInt16(); sampleRate = [int]$r.ReadUInt32() }
                [void]$r.ReadUInt32(); $blockAlign = [int]$r.ReadUInt16(); $fmt.bitsPerSample = [int]$r.ReadUInt16(); $fmt.blockAlign = $blockAlign
            } elseif ($id -eq 'data') {
                $dataBytes = [Math]::Min($size, $fs.Length - $fs.Position)
            }
            if ($fmt -ne $null -and $dataBytes -ge 0) { break }
            $fs.Position = [Math]::Min($next, $fs.Length)
        }
        if ($fmt -eq $null -or $dataBytes -lt 0 -or $fmt.blockAlign -le 0 -or $fmt.sampleRate -le 0) { return $null }
        $seconds = [Math]::Round(($dataBytes / $fmt.blockAlign) / $fmt.sampleRate, 3)
        return [ordered]@{ format = $fmt.format; channels = $fmt.channels; sampleRate = $fmt.sampleRate; bitsPerSample = $fmt.bitsPerSample; seconds = $seconds }
    } finally { $fs.Dispose() }
}

# What the file really holds, from its first bytes: '.wav', '.mp3' or '.ogg', or $null.
function Sniff-Audio([string]$path) {
    $fs = [IO.File]::OpenRead($path)
    try {
        $b = New-Object byte[] 12
        $n = $fs.Read($b, 0, 12)
        if ($n -ge 12 -and [Text.Encoding]::ASCII.GetString($b, 0, 4) -eq 'RIFF' -and [Text.Encoding]::ASCII.GetString($b, 8, 4) -eq 'WAVE') { return '.wav' }
        if ($n -ge 4 -and [Text.Encoding]::ASCII.GetString($b, 0, 4) -eq 'OggS') { return '.ogg' }
        if ($n -ge 3 -and [Text.Encoding]::ASCII.GetString($b, 0, 3) -eq 'ID3') { return '.mp3' }
        if ($n -ge 2 -and $b[0] -eq 0xFF -and ($b[1] -band 0xE0) -eq 0xE0) { return '.mp3' }
        return $null
    } finally { $fs.Dispose() }
}

# --- the resource tree: every sound with its GameMaker group --------------------------------------------
$project = Read-Xml (Join-Path $Source 'HARE.project.gmx')
function Group-Of([Xml.XmlNode]$leaf) {
    $parts = @()
    $n = $leaf.ParentNode
    while ($n -ne $null -and $n.LocalName -eq 'sounds') {
        $parts = @($n.GetAttribute('name')) + $parts
        $n = $n.ParentNode
    }
    # The top folder is GameMaker's own ("sound"); the groups under it are the author's.
    if ($parts.Count -gt 0) { $parts = @($parts | Select-Object -Skip 1) }
    return ($parts -join '/')
}
$soundList = @(foreach ($node in $project.SelectNodes('//sounds//sound')) {
    [pscustomobject]@{ Name = ($node.InnerText -replace '^sound\\', ''); Group = (Group-Of $node) }
})

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

# --- sounds ------------------------------------------------------------------------------------------
$sounds = @(); $skipped = @()
$audioSource = Join-Path $Source 'sound\audio'
$usedFiles = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $soundList) {
    $name = $entry.Name
    $file = Join-Path $Source "sound\$name.sound.gmx"
    if (-not (Test-Path $file)) { $skipped += [ordered]@{ name = $name; kind = 'sound'; reason = "listed in HARE.project.gmx but sound\$name.sound.gmx is missing" }; continue }
    $s = (Read-Xml $file).sound
    $data = [string]$s.data
    $audio = Join-Path $audioSource $data
    if (-not $data -or -not (Test-Path $audio)) { $skipped += [ordered]@{ name = $name; kind = 'sound'; reason = "audio file sound\audio\$data is missing" }; continue }
    [void]$usedFiles.Add($data)
    $ext = [IO.Path]::GetExtension($data).ToLowerInvariant()
    if ($ext -ne ([string]$s.extension).ToLowerInvariant()) { $skipped += [ordered]@{ name = $name; kind = 'sound'; reason = "sound.gmx says $($s.extension) but its file is $data" }; continue }
    $notes = @()
    # Unity picks the decoder by extension, so the clip takes the extension of what the file really holds.
    $actual = Sniff-Audio $audio
    if ($actual -eq $null) { $skipped += [ordered]@{ name = $name; kind = 'sound'; reason = "$data is not WAV, MP3 or Ogg audio" }; continue }
    if ($actual -ne $ext) { $notes += "$data is really $($actual.TrimStart('.').ToUpperInvariant()) audio under a $ext name; imported as $name$actual so Unity can decode it" }
    $wav = $null
    if ($actual -eq '.wav') {
        $wav = Wav-Info $audio
        if ($wav -eq $null) { $skipped += [ordered]@{ name = $name; kind = 'sound'; reason = "$data is not a readable WAV" }; continue }
        if ($wav.format -ne 1) { $notes += "WAV format tag $($wav.format), not PCM" }
    }

    $asset = (Join-Asset @("$audioAssets/Sounds", $entry.Group)) + "/$name$actual"
    Mirror $audio $asset

    $sounds += [ordered]@{
        name = $name; group = $entry.Group
        kind = [int]$s.kind
        file = $data
        bytes = [int64](Get-Item $audio).Length
        sourceFormat = $actual.TrimStart('.')
        sourceCompressed = ($actual -ne '.wav' -or ($wav -ne $null -and $wav.format -ne 1))
        volume = [double]::Parse([string]$s.volume.volume, $inv); pan = [double]::Parse([string]$s.pan, $inv)
        effects = [int]$s.effects
        compressed = ([int]$s.compressed -ne 0); streamed = ([int]$s.streamed -ne 0); uncompressOnLoad = ([int]$s.uncompressOnLoad -ne 0)
        type = [int]$s.types.type
        sampleRate = [int]$s.sampleRates.sampleRate; bitRate = [int]$s.bitRates.bitRate; bitDepth = [int]$s.bitDepths.bitDepth
        preload = [int]$s.preload
        sourceChannels = $(if ($wav) { $wav.channels } else { 0 })
        sourceSampleRate = $(if ($wav) { $wav.sampleRate } else { 0 })
        sourceBitsPerSample = $(if ($wav) { $wav.bitsPerSample } else { 0 })
        sourceSeconds = $(if ($wav) { [double]$wav.seconds } else { 0.0 })
        clip = $asset
        notes = $notes
    }
}
foreach ($file in Get-ChildItem (Join-Path $Source 'sound') -Filter '*.sound.gmx') {
    $name = $file.Name -replace '\.sound\.gmx$', ''
    if (-not ($soundList | Where-Object { $_.Name -eq $name })) {
        $skipped += [ordered]@{ name = $name; kind = 'sound'; reason = 'on disk but not in HARE.project.gmx, so not part of the game' }
    }
}
foreach ($file in Get-ChildItem $audioSource -File | Sort-Object Name) {
    if (-not $usedFiles.Contains($file.Name)) {
        $skipped += [ordered]@{ name = $file.Name; kind = 'file'; reason = 'in sound\audio but no sound resource uses it' }
    }
}

# --- data files (datafiles/: the sound credits) -------------------------------------------------------
$datafiles = @()
foreach ($node in $project.SelectNodes('//datafiles//datafile')) {
    $fileName = $node.SelectSingleNode('filename').InnerText
    $path = Join-Path (Join-Path $Source 'datafiles') $fileName
    if (-not (Test-Path $path)) { $skipped += [ordered]@{ name = $fileName; kind = 'datafile'; reason = "datafiles\$fileName is missing" }; continue }
    $asset = "$audioAssets/$fileName"
    Mirror $path $asset
    $datafiles += [ordered]@{ name = $node.SelectSingleNode('name').InnerText; file = $fileName; asset = $asset }
}

# --- delete what the original no longer has ----------------------------------------------------------
$removed = 0
$soundsDir = Join-Path $audioDir 'Sounds'
if (Test-Path $soundsDir) {
    foreach ($f in Get-ChildItem $soundsDir -Recurse -File | Where-Object { $_.Extension -ne '.meta' }) {
        if (-not $written.Contains($f.FullName)) {
            Remove-Item $f.FullName -Force
            if (Test-Path "$($f.FullName).meta") { Remove-Item "$($f.FullName).meta" -Force }
            $removed++
        }
    }
    foreach ($d in @(Get-ChildItem $soundsDir -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending)) {
        if (@(Get-ChildItem $d.FullName -Force).Count -eq 0) {
            Remove-Item $d.FullName -Force
            if (Test-Path "$($d.FullName).meta") { Remove-Item "$($d.FullName).meta" -Force }
        }
    }
}

# --- the manifest ------------------------------------------------------------------------------------
$manifest = [ordered]@{
    source = 'gmlHARE HARE.gmx (GameMaker 8.1 / Studio 1.x project), read by tools/import-gml-audio.ps1'
    vorbisQuality = $vorbisQuality
    sounds = $sounds
    datafiles = $datafiles
    skipped = $skipped
}
New-Item -ItemType Directory -Force $audioDir | Out-Null
$json = (To-Json $manifest '') + "`n"
$old = if (Test-Path $manifestPath) { [IO.File]::ReadAllText($manifestPath) } else { '' }
if ($old -ne $json) { [IO.File]::WriteAllText($manifestPath, $json, (New-Object Text.UTF8Encoding $false)) }

$music = @($sounds | Where-Object { $_.kind -eq 1 -or $_.kind -eq 3 }).Count
Write-Host "[Unity.Hare] GML audio: $($sounds.Count) sounds ($music music, $($sounds.Count - $music) sfx), $($datafiles.Count) data files, $($skipped.Count) skipped; $copied files copied, $removed removed; manifest $(if ($old -ne $json) { 'updated' } else { 'unchanged' })"
foreach ($s in $skipped) { Write-Host "  skipped $($s.kind) $($s.name): $($s.reason)" }

if (-not $NoUnity) {
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'unity.ps1') -Method Unity.Hare.EditorTools.GmlAudioBuilder.BuildAll -Log audio
    exit $LASTEXITCODE
}
