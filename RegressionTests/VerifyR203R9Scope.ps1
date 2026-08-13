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

# r9 extends the verified r8 scope only for custom-query eligibility and its
# focused regression. All other product files remain byte-for-byte r7/r8 work.
$allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
@(
    'Configuration/PluginConfiguration.cs',
    'Emby.Plugin.Danmu.csproj',
    'Properties/AssemblyInfo.cs',
    'Scraper/DanmuMatchScorer.cs',
    'Scraper/Mgtv/Mgtv.cs',
    'Scraper/Mgtv/MgtvApi.cs',
    'RegressionTests/R3SearchQuality/Program.cs',
    'RegressionTests/VerifyR203R8Scope.ps1',
    'RegressionTests/VerifyR203R9Scope.ps1'
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
$r9Files = Get-ProductFileMap $RepositoryRoot
$violations = @()
foreach ($relative in @($r7Files.Keys + $r9Files.Keys | Sort-Object -Unique)) {
    if (Test-AllowedProductPath $relative) { continue }
    if (-not $r7Files.ContainsKey($relative) -or -not $r9Files.ContainsKey($relative)) {
        $violations += "added-or-deleted: $relative"
        continue
    }
    if ((Get-FileHash -LiteralPath $r7Files[$relative] -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $r9Files[$relative] -Algorithm SHA256).Hash) {
        $violations += "out-of-scope edit: $relative"
    }
}
if ($violations.Count) {
    throw "r9 is not a narrow search-query delta over r7: $($violations -join ', ')"
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
    if ($baseline -ne $current) { throw "r9 changed protected $label method: $signature" }
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
    if ($baseline -ne $current) { throw "r9 changed protected $label method: $signature" }
}

function Get-CSharpMethodBounds([string]$path, [string]$methodName) {
    $text = (Get-Content -Raw -LiteralPath $path) -replace "`r`n?", "`n"
    $declaration = [regex]::Match(
        $text,
        "(?m)^\s*public\s+static\s+bool\s+" + [regex]::Escape($methodName) + "\s*\(")
    if (-not $declaration.Success) {
        throw "Protected method declaration missing: $methodName ($path)"
    }
    if ([regex]::Matches($text, "(?m)^\s*public\s+static\s+bool\s+" + [regex]::Escape($methodName) + "\s*\(").Count -ne 1) {
        throw "Protected method declaration is ambiguous: $methodName ($path)"
    }

    $state = 'code'
    $open = -1
    for ($index = $declaration.Index; $index -lt $text.Length; $index++) {
        $character = $text[$index]
        $next = if ($index + 1 -lt $text.Length) { $text[$index + 1] } else { [char]0 }
        $third = if ($index + 2 -lt $text.Length) { $text[$index + 2] } else { [char]0 }
        switch ($state) {
            'lineComment' {
                if ($character -eq "`n") { $state = 'code' }
                continue
            }
            'blockComment' {
                if ($character -eq '*' -and $next -eq '/') { $state = 'code'; $index++ }
                continue
            }
            'string' {
                if ($character -eq '\\') { $index++ }
                elseif ($character -eq '"') { $state = 'code' }
                continue
            }
            'verbatimString' {
                if ($character -eq '"' -and $next -eq '"') { $index++ }
                elseif ($character -eq '"') { $state = 'code' }
                continue
            }
            'character' {
                if ($character -eq '\\') { $index++ }
                elseif ($character -eq "'") { $state = 'code' }
                continue
            }
        }

        if ($character -eq '/' -and $next -eq '/') { $state = 'lineComment'; $index++; continue }
        if ($character -eq '/' -and $next -eq '*') { $state = 'blockComment'; $index++; continue }
        if (($character -eq '$' -and $next -eq '@' -and $third -eq '"') -or
            ($character -eq '@' -and $next -eq '$' -and $third -eq '"')) {
            $state = 'verbatimString'; $index += 2; continue
        }
        if ($character -eq '@' -and $next -eq '"') { $state = 'verbatimString'; $index++; continue }
        if (($character -eq '$' -and $next -eq '"') -or $character -eq '"') { $state = 'string'; if ($character -eq '$') { $index++ }; continue }
        if ($character -eq "'") { $state = 'character'; continue }
        if ($character -eq '{') { $open = $index; break }
    }
    if ($open -lt 0) { throw "Protected method opening brace missing: $methodName ($path)" }

    $state = 'code'
    $depth = 0
    for ($index = $open; $index -lt $text.Length; $index++) {
        $character = $text[$index]
        $next = if ($index + 1 -lt $text.Length) { $text[$index + 1] } else { [char]0 }
        $third = if ($index + 2 -lt $text.Length) { $text[$index + 2] } else { [char]0 }
        switch ($state) {
            'lineComment' {
                if ($character -eq "`n") { $state = 'code' }
                continue
            }
            'blockComment' {
                if ($character -eq '*' -and $next -eq '/') { $state = 'code'; $index++ }
                continue
            }
            'string' {
                if ($character -eq '\\') { $index++ }
                elseif ($character -eq '"') { $state = 'code' }
                continue
            }
            'verbatimString' {
                if ($character -eq '"' -and $next -eq '"') { $index++ }
                elseif ($character -eq '"') { $state = 'code' }
                continue
            }
            'character' {
                if ($character -eq '\\') { $index++ }
                elseif ($character -eq "'") { $state = 'code' }
                continue
            }
        }

        if ($character -eq '/' -and $next -eq '/') { $state = 'lineComment'; $index++; continue }
        if ($character -eq '/' -and $next -eq '*') { $state = 'blockComment'; $index++; continue }
        if (($character -eq '$' -and $next -eq '@' -and $third -eq '"') -or
            ($character -eq '@' -and $next -eq '$' -and $third -eq '"')) {
            $state = 'verbatimString'; $index += 2; continue
        }
        if ($character -eq '@' -and $next -eq '"') { $state = 'verbatimString'; $index++; continue }
        if (($character -eq '$' -and $next -eq '"') -or $character -eq '"') { $state = 'string'; if ($character -eq '$') { $index++ }; continue }
        if ($character -eq "'") { $state = 'character'; continue }
        if ($character -eq '{') { $depth++; continue }
        if ($character -eq '}') {
            $depth--
            if ($depth -eq 0) {
                return @{ Text = $text; Start = $declaration.Index; End = $index + 1 }
            }
        }
    }
    throw "Protected method closing brace missing: $methodName ($path)"
}

function Get-NormalizedFileWithoutMethod([string]$path, [string]$methodName) {
    $bounds = Get-CSharpMethodBounds $path $methodName
    return $bounds.Text.Substring(0, $bounds.Start) + "<IsEligibleSeasonCandidate omitted>`n" +
        $bounds.Text.Substring($bounds.End)
}

function Assert-R7FileUnchangedExceptMethod([string]$relative, [string]$methodName) {
    $baseline = Get-NormalizedFileWithoutMethod (Join-Path $R7ReferenceRoot $relative) $methodName
    $current = Get-NormalizedFileWithoutMethod (Join-Path $RepositoryRoot $relative) $methodName
    if ($baseline -cne $current) {
        throw "r9 changed content outside the permitted $methodName method in $relative"
    }
}

# DanmuMatchScorer may differ from r7 only in the complete eligibility method;
# after removing that exact C# method body, every remaining normalized byte is frozen.
Assert-R7FileUnchangedExceptMethod 'Scraper/DanmuMatchScorer.cs' 'IsEligibleSeasonCandidate'

# Search is the only r8 MGTV behavior that changed. Its non-search retrieval
# and danmaku methods remain frozen at the verified r7 implementation.
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

# r9 must not revise a provider request path or URL encoding. The r8 MGTV
# sources changed from r7, so freeze their verified r8 content by hash here;
# every other provider source is already byte-for-byte constrained above.
$frozenProviderHashes = @{
    'Scraper/Mgtv/Entity/MgtvComment.cs' = '5b388ede93c7ccd18f293f3dd82cb90d45d890ede09e42c228438004331239b4'
    'Scraper/Mgtv/Entity/MgtvCommentResult.cs' = '7305d7036b0c747f0f28bbdde705e018b8dbb693ba6e59a2ee3691628e64d88e'
    'Scraper/Mgtv/Entity/MgtvCommentSegemntResult.cs' = '4624d9b1a0996c638ed618689c3d0e4b3dcd1debf8b17e72ee4df85a44e8bb70'
    'Scraper/Mgtv/Entity/MgtvControlBarrage.cs' = 'f8d635c3e174861d29b574bc17976b33ac060de5f66d030bf5309a1fd985bd0e'
    'Scraper/Mgtv/Entity/MgtvEpisode.cs' = '3193c07b63e6bde48db2b5cf3f7434ab67cd116b5d3cc7fb5a1aabd7c2bc7a12'
    'Scraper/Mgtv/Entity/MgtvEpisodeListRequest.cs' = 'efabafdfd06e7fde8a6590d068d3b4b6c07ef141c65755942b3a66168af6511a'
    'Scraper/Mgtv/Entity/MgtvEpisodeListResult.cs' = '6a9cc6dc6076bf1fb6c31b771e9587859dfd95a080d2c781f8362c183f9b9240'
    'Scraper/Mgtv/Entity/MgtvSearchResult.cs' = '45ae6852129ae32ed401b96c23fdd2acbe31945c287ce728a30e3d2a72bff509'
    'Scraper/Mgtv/Entity/MgtvVideo.cs' = 'e3982ea6e607995a663f7a61fa302d14cb707da712e69e54aaef6b5214ae9e3a'
    'Scraper/Mgtv/Entity/MgtvVideoInfoResult.cs' = '40351876c7199458038af172375067368a166f2fc55a17b4f3c4dc211085e284'
    'Scraper/Mgtv/ExternalId/EpisodeExternalId.cs' = '77284e40c3273dc67531db6e469c1bf6d5ed9f3af14d809724e38887990ec15e'
    'Scraper/Mgtv/ExternalId/MovieExternalId.cs' = 'f1ac3f8c30ef588adf365b7c6084523698b75140a9c4e8484a2700f3c3ff0af6'
    'Scraper/Mgtv/ExternalId/SeasonExternalId.cs' = 'd6e528f7d6b67ad5bbc2f0fc2d2a8fff6be9fe9242c13ca3c49e1e94dad0731c'
    'Scraper/Mgtv/Mgtv.cs' = '942cb39ed5352a208625b83f9a7a375622a0e52330c2955356dd24875e13231c'
    'Scraper/Mgtv/MgtvApi.cs' = 'cb217702e8475e27fe679bb7d13f11e97a9b80e2d9636a9139023f80d3fa840e'
}
foreach ($entry in $frozenProviderHashes.GetEnumerator()) {
    $actual = (Get-FileHash -LiteralPath (Join-Path $RepositoryRoot $entry.Key) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $entry.Value) { throw "r9 changed frozen provider source or URL encoding path: $($entry.Key)" }
}

[xml]$project = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Emby.Plugin.Danmu.csproj')
$properties = $project.Project.PropertyGroup | Where-Object AssemblyVersion | Select-Object -First 1
if ($properties.TargetFramework -ne 'netstandard2.0' -or $properties.LangVersion -ne '8.0' -or
    $properties.AssemblyVersion -ne '2.0.3.0' -or $properties.FileVersion -ne '2.0.3.9' -or
    $properties.InformationalVersion -ne '2.0.3r9') {
    throw 'r9 target/version contract is not exact.'
}

$configuration = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Configuration/PluginConfiguration.cs')
$frontend = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Frontend/DanmuSmartMatch.CustomCssJS.js')
if (-not $configuration.Contains('Version { get; } = "2.0.3r9"') -or
    [regex]::Matches($frontend, '__embyDanmuSmartMenuV23').Count -ne 1 -or
    $frontend.Contains('__embyDanmuSmartMenuV22') -or
    [regex]::Matches($frontend, 'var MAPPING_PROTOCOL_VERSION = 21;').Count -ne 1) {
    throw 'r9 configuration, frozen V23 cache marker, or frozen V21 mapping protocol is invalid.'
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
    throw 'r9 may expose internals only to the two exact deterministic test assemblies.'
}

Write-Output '2.0.3r9 narrow-delta scope check passed against the verified 2.0.3r7 workspace.'
