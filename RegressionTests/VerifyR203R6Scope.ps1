param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$R5ReferenceRoot = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'emby-plugin-danmuplus-2.0.3r5')
)

$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$R5ReferenceRoot = [IO.Path]::GetFullPath($R5ReferenceRoot)

if (-not (Test-Path -LiteralPath (Join-Path $R5ReferenceRoot 'artifacts/2.0.3r5/VERIFICATION.md'))) {
    throw "The frozen r5 reference workspace is unavailable: $R5ReferenceRoot"
}
if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot 'artifacts/2.0.3r6/BASELINE.md'))) {
    throw 'The r6 frozen-baseline manifest is missing.'
}

$expectedAssets = @{
    'artifacts/2.0.3r5/Emby.Plugin.Danmu.dll' = '123ee755f22ae20a1a2492f4d616c4b6f8cd232bfc629fac25f0a4c466b8d552'
    'artifacts/2.0.3r5/DanmuSmartMatch.CustomCssJS.js' = 'b457b4cbd4dc91a250230531cc8124bbd174872577963bf8976491d870546b9d'
}
foreach ($entry in $expectedAssets.GetEnumerator()) {
    $path = Join-Path $RepositoryRoot $entry.Key
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $entry.Value) {
        throw "Frozen r5 asset hash mismatch: $($entry.Key)"
    }
}

$allowedProductDelta = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
@(
    'Configuration/PluginConfiguration.cs',
    'Core/Controllers/DanmuController.cs',
    'Emby.Plugin.Danmu.csproj',
    'Frontend/DanmuSmartMatch.CustomCssJS.js',
    'Frontend/DanmuSmartMatch.RegressionTests.js',
    'Model/DanmuMatchResult.cs',
    'RegressionTests/Program.cs',
    'RegressionTests/R5TargetSeasonScope/Program.cs',
    'RegressionTests/VerifyR203R6Scope.ps1'
) | ForEach-Object { [void]$allowedProductDelta.Add($_) }

$roots = @('Configuration', 'Core', 'Frontend', 'Model', 'Scraper', 'RegressionTests')
function Get-ProductFileMap([string]$root) {
    $map = @{}
    foreach ($relativeRoot in $roots) {
        $absoluteRoot = Join-Path $root $relativeRoot
        foreach ($file in Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File) {
            if ($file.FullName -match '[\\/](bin|obj)[\\/]') { continue }
            $relative = $file.FullName.Substring($root.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
            $map[$relative] = $file.FullName
        }
    }
    foreach ($relative in @('Emby.Plugin.Danmu.csproj', 'LibraryManagerEventsHelper.cs')) {
        $map[$relative] = Join-Path $root $relative
    }
    return $map
}

$r5Files = Get-ProductFileMap $R5ReferenceRoot
$r6Files = Get-ProductFileMap $RepositoryRoot
$allPaths = @($r5Files.Keys + $r6Files.Keys | Sort-Object -Unique)
$violations = @()
foreach ($relative in $allPaths) {
    if ($allowedProductDelta.Contains($relative)) { continue }
    if (-not $r5Files.ContainsKey($relative) -or -not $r6Files.ContainsKey($relative)) {
        $violations += "added-or-deleted: $relative"
        continue
    }
    $r5Hash = (Get-FileHash -LiteralPath $r5Files[$relative] -Algorithm SHA256).Hash
    $r6Hash = (Get-FileHash -LiteralPath $r6Files[$relative] -Algorithm SHA256).Hash
    if ($r5Hash -ne $r6Hash) { $violations += "out-of-scope edit: $relative" }
}
if ($violations.Count) {
    throw "r6 is not a narrow delta over the frozen r5 source: $($violations -join ', ')"
}

$forbidden = '(?i)DanmuSeasonSegment|DanmuSeasonCollection|SegmentSelections|CollectionSelections'
$scanPaths = @('Configuration', 'Core', 'Frontend', 'Model', 'Scraper', 'LibraryManagerEventsHelper.cs')
$matches = @(Get-ChildItem -LiteralPath ($scanPaths | ForEach-Object { Join-Path $RepositoryRoot $_ }) -Recurse -File -ErrorAction SilentlyContinue |
    Select-String -Pattern $forbidden)
if ($matches.Count) {
    $locations = $matches | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "Later collection/segment protocols are forbidden in r6: $($locations -join ', ')"
}

[xml]$project = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Emby.Plugin.Danmu.csproj')
$properties = $project.Project.PropertyGroup | Where-Object AssemblyVersion | Select-Object -First 1
if ($properties.TargetFramework -ne 'netstandard2.0' -or $properties.LangVersion -ne '8.0' -or
    $properties.AssemblyVersion -ne '2.0.3.0' -or $properties.FileVersion -ne '2.0.3.6' -or
    $properties.InformationalVersion -ne '2.0.3r6') {
    throw 'r6 target/version contract is not exact.'
}
$configuration = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Configuration/PluginConfiguration.cs')
$frontend = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Frontend/DanmuSmartMatch.CustomCssJS.js')
if (-not $configuration.Contains('Version { get; } = "2.0.3r6"') -or
    [regex]::Matches($frontend, '__embyDanmuSmartMenuV22').Count -ne 1 -or
    $frontend.Contains('__embyDanmuSmartMenuV21') -or
    [regex]::Matches($frontend, 'var MAPPING_PROTOCOL_VERSION = 21;').Count -ne 1) {
    throw 'r6 configuration, V22 cache marker, or preserved V21 mapping protocol is invalid.'
}

Write-Output '2.0.3r6 narrow-delta scope check passed against the frozen 2.0.3r5 source workspace.'
