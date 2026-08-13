param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$R7ReferenceRoot = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'emby-plugin-danmuplus-2.0.3r7')
)

$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$R7ReferenceRoot = [IO.Path]::GetFullPath($R7ReferenceRoot)

if (-not (Test-Path -LiteralPath (Join-Path $R7ReferenceRoot 'artifacts/2.0.3r7/BASELINE.md'))) {
    throw "The verified r7 reference workspace is unavailable: $R7ReferenceRoot"
}
if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot 'artifacts/2.0.3r8/BASELINE.md'))) {
    throw 'The r8 baseline manifest is missing.'
}

# r8 is intentionally a Mango-search-only release.  Keep this list small: all
# other product sources must be byte-for-byte identical to the sibling r7 tree.
$allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
@(
    'Configuration/PluginConfiguration.cs',
    'Emby.Plugin.Danmu.csproj',
    'Properties/AssemblyInfo.cs',
    'Scraper/Mgtv/Mgtv.cs',
    'Scraper/Mgtv/MgtvApi.cs',
    'RegressionTests/VerifyR203R8Scope.ps1'
) | ForEach-Object { [void]$allowed.Add($_) }

$allowedPrefixes = @(
    'Scraper/Mgtv/Entity/',
    'RegressionTests/'
)

function Test-AllowedProductPath([string]$relative) {
    if ($allowed.Contains($relative)) { return $true }
    foreach ($prefix in $allowedPrefixes) {
        if ($relative.StartsWith($prefix, [StringComparison]::Ordinal)) { return $true }
    }
    return $false
}

function Get-ProductFileMap([string]$root) {
    $map = @{}
    foreach ($relativeRoot in @('Configuration', 'Core', 'Frontend', 'Model', 'Properties', 'Scraper', 'RegressionTests')) {
        $directory = Join-Path $root $relativeRoot
        if (-not (Test-Path -LiteralPath $directory)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File) {
            if ($file.FullName -match '[\\/](bin|obj)[\\/]') { continue }
            $relative = $file.FullName.Substring($root.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
            $map[$relative] = $file.FullName
        }
    }
    foreach ($relative in @('Emby.Plugin.Danmu.csproj', 'LibraryManagerEventsHelper.cs')) {
        $path = Join-Path $root $relative
        if (Test-Path -LiteralPath $path) { $map[$relative] = $path }
    }
    return $map
}

$r7Files = Get-ProductFileMap $R7ReferenceRoot
$r8Files = Get-ProductFileMap $RepositoryRoot
$violations = @()
foreach ($relative in @($r7Files.Keys + $r8Files.Keys | Sort-Object -Unique)) {
    if (Test-AllowedProductPath $relative) { continue }
    if (-not $r7Files.ContainsKey($relative) -or -not $r8Files.ContainsKey($relative)) {
        $violations += "added-or-deleted: $relative"
        continue
    }
    if ((Get-FileHash -LiteralPath $r7Files[$relative] -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $r8Files[$relative] -Algorithm SHA256).Hash) {
        $violations += "out-of-scope edit: $relative"
    }
}
if ($violations.Count) {
    throw "r8 is not a narrow MGTV delta over r7: $($violations -join ', ')"
}

function Get-NormalizedMethodRegion([string]$path, [string]$signature, [string]$nextSignature) {
    $text = Get-Content -Raw -LiteralPath $path
    $start = $text.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Protected method signature missing: $signature ($path)" }
    $end = $text.IndexOf($nextSignature, $start + $signature.Length, [StringComparison]::Ordinal)
    if ($end -le $start) { throw "Protected method end signature missing: $nextSignature ($path)" }
    return $text.Substring($start, $end - $start) -replace "`r`n?", "`n"
}

function Get-MethodRegionHash([string]$path, [string]$signature, [string]$nextSignature) {
    $bytes = [Text.Encoding]::UTF8.GetBytes((Get-NormalizedMethodRegion $path $signature $nextSignature))
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha256.Dispose() }
}

function Assert-R7MethodRegionUnchanged([string]$relative, [string]$signature, [string]$nextSignature, [string]$label) {
    $baseline = Get-MethodRegionHash (Join-Path $R7ReferenceRoot $relative) $signature $nextSignature
    $current = Get-MethodRegionHash (Join-Path $RepositoryRoot $relative) $signature $nextSignature
    if ($baseline -ne $current) { throw "r8 changed protected $label method: $signature" }
}

function Get-NormalizedMethodBody([string]$path, [string]$signature) {
    $text = Get-Content -Raw -LiteralPath $path
    $start = $text.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Protected method signature missing: $signature ($path)" }
    $open = $text.IndexOf('{', $start + $signature.Length)
    if ($open -lt 0) { throw "Protected method opening brace missing: $signature ($path)" }
    $depth = 0
    for ($index = $open; $index -lt $text.Length; $index++) {
        if ($text[$index] -eq '{') { $depth++ }
        elseif ($text[$index] -eq '}') {
            $depth--
            if ($depth -eq 0) { return $text.Substring($start, $index - $start + 1) -replace "`r`n?", "`n" }
        }
    }
    throw "Protected method closing brace missing: $signature ($path)"
}

function Assert-R7MethodBodyUnchanged([string]$relative, [string]$signature, [string]$label) {
    $baseline = Get-NormalizedMethodBody (Join-Path $R7ReferenceRoot $relative) $signature
    $current = Get-NormalizedMethodBody (Join-Path $RepositoryRoot $relative) $signature
    if ($baseline -ne $current) { throw "r8 changed protected $label method: $signature" }
}

# Search is the only MGTV behavior that may change.  Detail retrieval and
# danmaku retrieval stay frozen despite living next to SearchAsync.
foreach ($protected in @(
    @{ File = 'Scraper/Mgtv/MgtvApi.cs'; Label = 'MGTV GetVideo'; Signature = 'public async Task<MgtvVideo?> GetVideoAsync'; Next = 'public async Task<List<MgtvComment>> GetDanmuContentAsync' },
    @{ File = 'Scraper/Mgtv/MgtvApi.cs'; Label = 'MGTV danmaku'; Signature = 'public async Task<List<MgtvComment>> GetDanmuContentAsync'; Next = 'private async Task<List<MgtvComment>> GetDanmuContentByCdnAsync' },
    @{ File = 'Scraper/Mgtv/MgtvApi.cs'; Label = 'MGTV CDN danmaku'; Signature = 'private async Task<List<MgtvComment>> GetDanmuContentByCdnAsync'; Next = 'private async Task<T> DeserializeJsonResponseAsync' },
    @{ File = 'Scraper/Mgtv/Mgtv.cs'; Label = 'MGTV GetMedia'; Signature = 'public override async Task<ScraperMedia?> GetMedia'; Next = 'public override async Task<ScraperEpisode?> GetMediaEpisode' },
    @{ File = 'Scraper/Mgtv/Mgtv.cs'; Label = 'MGTV GetMediaEpisode'; Signature = 'public override async Task<ScraperEpisode?> GetMediaEpisode'; Next = 'public override async Task<ScraperDanmaku?> GetDanmuContent' },
    @{ File = 'Scraper/Mgtv/Mgtv.cs'; Label = 'MGTV GetDanmu'; Signature = 'public override async Task<ScraperDanmaku?> GetDanmuContent'; Next = 'public override Task<List<ScraperSearchInfo>> SearchForApi' },
    @{ File = 'Scraper/Mgtv/Mgtv.cs'; Label = 'MGTV API media listing'; Signature = 'public override async Task<List<ScraperEpisode>> GetEpisodesForApi'; Next = 'public override async Task<ScraperDanmaku?> DownloadDanmuForApi' }
)) {
    Assert-R7MethodRegionUnchanged $protected.File $protected.Signature $protected.Next $protected.Label
}
Assert-R7MethodBodyUnchanged 'Scraper/Mgtv/Mgtv.cs' 'public override async Task<ScraperDanmaku?> DownloadDanmuForApi' 'MGTV API danmaku download'

[xml]$project = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Emby.Plugin.Danmu.csproj')
$properties = $project.Project.PropertyGroup | Where-Object AssemblyVersion | Select-Object -First 1
if ($properties.TargetFramework -ne 'netstandard2.0' -or $properties.LangVersion -ne '8.0' -or
    $properties.AssemblyVersion -ne '2.0.3.0' -or $properties.FileVersion -ne '2.0.3.8' -or
    $properties.InformationalVersion -ne '2.0.3r8') {
    throw 'r8 target/version contract is not exact.'
}

$configuration = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Configuration/PluginConfiguration.cs')
$frontend = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Frontend/DanmuSmartMatch.CustomCssJS.js')
if (-not $configuration.Contains('Version { get; } = "2.0.3r8"') -or
    [regex]::Matches($frontend, '__embyDanmuSmartMenuV23').Count -ne 1 -or
    $frontend.Contains('__embyDanmuSmartMenuV22') -or
    [regex]::Matches($frontend, 'var MAPPING_PROTOCOL_VERSION = 21;').Count -ne 1) {
    throw 'r8 configuration, frozen V23 cache marker, or frozen V21 mapping protocol is invalid.'
}

$mgtvApi = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Scraper/Mgtv/MgtvApi.cs')
foreach ($legacyEndpoint in @('/msite/search/v2', '/pc/search/v1', '/pc/search/v2')) {
    if ([regex]::Matches($mgtvApi, [regex]::Escape($legacyEndpoint)).Count -ne 0) {
        throw "MGTV legacy search endpoint remains in product code: $legacyEndpoint"
    }
}
if ([regex]::Matches($mgtvApi, '/pc/suggest/v1').Count -ne 1) {
    throw 'MGTV endpoint migration must contain zero legacy search endpoints and exactly one PC suggest endpoint.'
}

$assemblyInfo = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Properties/AssemblyInfo.cs')
$friends = [regex]::Matches($assemblyInfo, 'InternalsVisibleTo\("([^"]+)"\)') |
    ForEach-Object { $_.Groups[1].Value }
if (@($friends).Count -ne 2 -or
    @($friends | Where-Object { $_ -eq 'Emby.Plugin.Danmu.RegressionTests' }).Count -ne 1 -or
    @($friends | Where-Object { $_ -eq 'MgtvSearch' }).Count -ne 1) {
    throw 'r8 may expose internals only to the two exact deterministic test assemblies.'
}

Write-Output '2.0.3r8 narrow-delta scope check passed against the verified 2.0.3r7 workspace.'
