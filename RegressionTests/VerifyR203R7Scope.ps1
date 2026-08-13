param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$R6ReferenceRoot = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'emby-plugin-danmuplus-2.0.3r6')
)

$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$R6ReferenceRoot = [IO.Path]::GetFullPath($R6ReferenceRoot)

if (-not (Test-Path -LiteralPath (Join-Path $R6ReferenceRoot 'artifacts/2.0.3r6/VERIFICATION.md'))) {
    throw "The verified r6 reference workspace is unavailable: $R6ReferenceRoot"
}
if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot 'artifacts/2.0.3r7/BASELINE.md'))) {
    throw 'The r7 baseline manifest is missing.'
}

$expectedR6Assets = @{
    'artifacts/2.0.3r6/Emby.Plugin.Danmu.dll' = 'dc437aea76f1db9b437257a9829b4ebb958815f1065102307835bffc9cf52807'
    'artifacts/2.0.3r6/DanmuSmartMatch.CustomCssJS.js' = '6f1a78e04397f377c0bd50129bc83857ee8cd3a8cd9a37d4bb7138a5946f397c'
}
foreach ($entry in $expectedR6Assets.GetEnumerator()) {
    $path = Join-Path $RepositoryRoot $entry.Key
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $entry.Value) { throw "Frozen r6 asset hash mismatch: $($entry.Key)" }
}

$protectedHashes = @{
    'Core/SeasonPlanningContext.cs' = '64ee701c8513a136dcc07a7960946b55f6d6e6faccff72ee7e62ebc86c12c208'
    'Core/SeasonTargetPlanningCoordinator.cs' = '49d7f166282ab29889e15b1968032115c5f2cb31a2ae5e543e398c1f433d31ac'
    'Core/SeasonPlanGenerationCoordinator.cs' = '2747dfb94b315d0ca60fd685d462aca0d7d170aad79feba65d13dd4b1897ad5c'
    'Model/CompositeSeasonOwnership.cs' = 'e3521e8e3f006d06088c6d511730d92c94f9da1c7832abe43eed423250fda414'
    'Model/CompositeSeasonMatch.cs' = 'ab102047fdfd3d7e48f46939c346abb5bc83da7c4b415270168214a3f9b57b3c'
    'Model/DanmuTemporaryRangeSearchPolicy.cs' = 'd6740c051f454058be46bfea2bf805ba4fa2dbfd601eea19fa551a86daac03d0'
    'LibraryManagerEventsHelper.cs' = 'cfaaeee7aac989b818995b6dd5d16f19a3383ca2cd214f918c3985e623dc7cbf'
    'Scraper/CompositeSeasonPlanner.cs' = '4887911e919ce3a21b66aa22ed256c27f5eb2c7568b1ada5789c738563f9d866'
    'Core/DanmuCandidateEvidenceRegistry.cs' = 'da35644376cdb0b5e77077bc9d55b4c55c95e1b1796534c65cc3008a70036923'
    'Core/DanmuDownloadPolicy.cs' = '859ea7e11ffe372a86f80d6aa72c886e5b95e98223f97e340a2a284ca86bc6b6'
    'Core/DanmuDownloadPersistencePolicy.cs' = '4fb928c7bae4361923b26d47e300df323f7405182f2949aaa58b3909d2049767'
    'Core/SingleTargetDownloadArbiter.cs' = '8c880d2fe2b987f5f408a90b0437402121d910677b419fac75e53d59a4bff970'
    'Core/Controllers/DanmuDispatchOption.cs' = '3d27413acc133f7b8c12786366dd51b141818a5e19b118aa27d8fa024b94971e'
}
foreach ($entry in $protectedHashes.GetEnumerator()) {
    $actual = (Get-FileHash -LiteralPath (Join-Path $RepositoryRoot $entry.Key) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $entry.Value) { throw "r6 scope/fingerprint/download file changed in r7: $($entry.Key)" }
}

$allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
@(
    'Configuration/PluginConfiguration.cs',
    'Core/Controllers/DanmuController.cs',
    'Emby.Plugin.Danmu.csproj',
    'Frontend/DanmuSmartMatch.CustomCssJS.js',
    'Frontend/DanmuSmartMatch.RegressionTests.js',
    'Model/DanmuMatchResult.cs',
    'Scraper/CompositeSeasonMatchService.cs',
    'RegressionTests/CompositeSeasonPlannerTests.cs',
    'RegressionTests/Program.cs',
    'RegressionTests/VerifyR203R7Scope.ps1'
) | ForEach-Object { [void]$allowed.Add($_) }

$roots = @('Configuration', 'Core', 'Frontend', 'Model', 'Scraper', 'RegressionTests')
function Get-ProductFileMap([string]$root) {
    $map = @{}
    foreach ($relativeRoot in $roots) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root $relativeRoot) -Recurse -File) {
            if ($file.FullName -match '[\\/](bin|obj)[\\/]' -or
                $file.FullName -match '[\\/]RegressionTests[\\/][^\\/]+[\\/]Emby\.Plugin\.Danmu\.dll$') { continue }
            $relative = $file.FullName.Substring($root.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
            $map[$relative] = $file.FullName
        }
    }
    foreach ($relative in @('Emby.Plugin.Danmu.csproj', 'LibraryManagerEventsHelper.cs')) {
        $map[$relative] = Join-Path $root $relative
    }
    return $map
}

$r6Files = Get-ProductFileMap $R6ReferenceRoot
$r7Files = Get-ProductFileMap $RepositoryRoot
$violations = @()
foreach ($relative in @($r6Files.Keys + $r7Files.Keys | Sort-Object -Unique)) {
    if ($allowed.Contains($relative)) { continue }
    if (-not $r6Files.ContainsKey($relative) -or -not $r7Files.ContainsKey($relative)) {
        $violations += "added-or-deleted: $relative"
        continue
    }
    if ((Get-FileHash $r6Files[$relative] -Algorithm SHA256).Hash -ne
        (Get-FileHash $r7Files[$relative] -Algorithm SHA256).Hash) {
        $violations += "out-of-scope edit: $relative"
    }
}
if ($violations.Count) { throw "r7 is not a narrow delta over r6: $($violations -join ', ')" }

$forbidden = '(?i)DanmuSeasonSegment|DanmuSeasonCollection|SegmentSelections|CollectionSelections'
$scanPaths = @('Configuration', 'Core', 'Frontend', 'Model', 'Scraper', 'LibraryManagerEventsHelper.cs')
$matches = @(Get-ChildItem -LiteralPath ($scanPaths | ForEach-Object { Join-Path $RepositoryRoot $_ }) -Recurse -File -ErrorAction SilentlyContinue |
    Select-String -Pattern $forbidden)
if ($matches.Count) { throw 'Later collection/segment protocols are forbidden in r7.' }

[xml]$project = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Emby.Plugin.Danmu.csproj')
$properties = $project.Project.PropertyGroup | Where-Object AssemblyVersion | Select-Object -First 1
if ($properties.TargetFramework -ne 'netstandard2.0' -or $properties.LangVersion -ne '8.0' -or
    $properties.AssemblyVersion -ne '2.0.3.0' -or $properties.FileVersion -ne '2.0.3.7' -or
    $properties.InformationalVersion -ne '2.0.3r7') { throw 'r7 target/version contract is not exact.' }

$configuration = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Configuration/PluginConfiguration.cs')
$frontend = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Frontend/DanmuSmartMatch.CustomCssJS.js')
if (-not $configuration.Contains('Version { get; } = "2.0.3r7"') -or
    [regex]::Matches($frontend, '__embyDanmuSmartMenuV23').Count -ne 1 -or
    $frontend.Contains('__embyDanmuSmartMenuV22') -or
    [regex]::Matches($frontend, 'var MAPPING_PROTOCOL_VERSION = 21;').Count -ne 1) {
    throw 'r7 configuration, V23 cache marker, or preserved V21 mapping protocol is invalid.'
}

$busy = [regex]::Match($frontend, '(?s)function setBusy\(.*?(?=\n\s*function appendForceRefreshOption)').Value
if (-not $busy.Contains('cancel.textContent') -or
    $busy -match 'appendPreDownloadFooter|appendForceRefreshOption|snapshotForceRefresh|forceRefresh\s*=') {
    throw 'Busy rendering must retain cancellation but must not render or mutate force refresh.'
}

$r6ProductText = (Get-Content -Raw (Join-Path $R6ReferenceRoot 'Core/Controllers/DanmuController.cs')) +
    (Get-Content -Raw (Join-Path $R6ReferenceRoot 'Scraper/CompositeSeasonMatchService.cs'))
$r7ProductText = (Get-Content -Raw (Join-Path $RepositoryRoot 'Core/Controllers/DanmuController.cs')) +
    (Get-Content -Raw (Join-Path $RepositoryRoot 'Scraper/CompositeSeasonMatchService.cs'))
foreach ($pattern in @('\.GetMedia\(', 'ResolveDirectEpisodeMediaAsync\(')) {
    if ([regex]::Matches($r6ProductText, $pattern).Count -ne [regex]::Matches($r7ProductText, $pattern).Count) {
        throw "r7 changed provider-resolution call count for pattern $pattern"
    }
}

$titleForbiddenFiles = @(
    'Core/SeasonPlanningContext.cs', 'Core/DanmuCandidateEvidenceRegistry.cs',
    'Core/DanmuDownloadPolicy.cs', 'Core/DanmuDownloadPersistencePolicy.cs',
    'Core/SingleTargetDownloadArbiter.cs', 'LibraryManagerEventsHelper.cs',
    'Model/CompositeSeasonMatch.cs', 'Model/DanmuTemporaryRangeSearchPolicy.cs',
    'Scraper/CompositeSeasonPlanner.cs'
)
foreach ($relative in $titleForbiddenFiles) {
    $text = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot $relative)
    if ($text -match 'SourceEpisode(Name|Title)') {
        throw "Presentation title leaked into an authority/fingerprint/download file: $relative"
    }
}

# Keep r6 behavior frozen at method granularity. The permitted r7 work shares
# the Controller and frontend files, so a whole-file hash would reject the
# intended title projection and busy-state changes. Method regions are matched
# by the same signature in sibling r6 and normalized to LF before hashing.
function Get-NormalizedMethodRegion([string]$path, [string]$signature, [string]$nextSignature) {
    $text = Get-Content -Raw -LiteralPath $path
    $start = $text.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Protected method signature missing: $signature ($path)" }
    $end = $text.IndexOf($nextSignature, $start + $signature.Length, [StringComparison]::Ordinal)
    if ($end -le $start) {
        throw "Protected method end signature missing: $nextSignature ($path)"
    }
    return $text.Substring($start, $end - $start) -replace "`r`n?", "`n"
}

function Get-MethodRegionHash([string]$path, [string]$signature, [string]$nextSignature) {
    $bytes = [Text.Encoding]::UTF8.GetBytes((Get-NormalizedMethodRegion $path $signature $nextSignature))
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Assert-R6MethodRegionUnchanged([string]$relative, [string]$signature, [string]$nextSignature, [string]$label) {
    $baseline = Get-MethodRegionHash (Join-Path $R6ReferenceRoot $relative) $signature $nextSignature
    $current = Get-MethodRegionHash (Join-Path $RepositoryRoot $relative) $signature $nextSignature
    if ($baseline -ne $current) {
        throw "r7 changed protected $label method: $signature"
    }
}

# Candidate evidence/detail and tracked-download behavior remain exactly r6.
# The common frontend detail component is excluded because r7 is allowed to
# reuse it for temporary-range candidates with an added stale-response gate.
$protectedControllerMethods = @(
    @{ Label = 'candidate evidence/detail'; Signature = 'private async Task<DanmuMatchCandidateDetailResult> GetMatchCandidateDetails'; Next = 'private static Task<ScraperMedia> ResolveMatchCandidateDetailsMediaAsync' },
    @{ Label = 'candidate evidence/detail'; Signature = 'private static Task<ScraperMedia> ResolveMatchCandidateDetailsMediaAsync'; Next = 'private static bool IsSafeMatchCandidateId' },
    @{ Label = 'candidate evidence/detail'; Signature = 'private static void StampCandidateEvidence'; Next = 'private static void ApplyProviderDecision(DanmuItemMatchResult result' },
    @{ Label = 'tracked download'; Signature = 'private async Task<DanmuDownloadTaskResult> StartTrackedCompositeSeasonDownload'; Next = 'private async Task<DanmuDownloadTaskResult> StartTrackedMovieDownload' },
    @{ Label = 'tracked download'; Signature = 'private async Task<DanmuDownloadTaskResult> StartTrackedSingleEpisodeDownload'; Next = 'private DanmuDownloadTaskResult FailedTarget' },
    @{ Label = 'tracked download'; Signature = 'private DanmuDownloadTaskResult CreateSingleTargetTask'; Next = 'private DanmuDownloadTaskResult QueueSingleTargetDownload' },
    @{ Label = 'tracked download'; Signature = 'private DanmuDownloadTaskResult QueueSingleTargetDownload'; Next = 'private async Task<DanmuEpisodeDownloadOutcome> AwaitSingleTargetDownload' },
    @{ Label = 'tracked download'; Signature = 'private async Task<DanmuDownloadTaskResult> RetryTrackedEpisode'; Next = 'private async Task<DanmuDownloadTaskResult> RetryTrackedMovie' },
    @{ Label = 'tracked download'; Signature = 'private DanmuDownloadTaskResult GetDownloadProgress'; Next = 'private static DanmuDownloadTaskResult Snapshot' }
)
foreach ($protected in $protectedControllerMethods) {
    Assert-R6MethodRegionUnchanged 'Core/Controllers/DanmuController.cs' $protected.Signature $protected.Next $protected.Label
}

# r7 may alter busy rendering and temporary-range presentation, but force
# snapshot, ordinary Episode picker, and download/progress paths stay frozen.
$protectedFrontendMethods = @(
    @{ Label = 'force snapshot'; Signature = 'function snapshotForceRefresh'; Next = 'function recoverPreDownload' },
    @{ Label = 'download submission'; Signature = 'async function submitSeriesSelections'; Next = 'async function renderDownloadProgress' },
    @{ Label = 'download progress'; Signature = 'async function renderDownloadProgress'; Next = 'function appendCompositeMappingDetails' },
    @{ Label = 'Episode picker'; Signature = 'function renderEpisodeSourcePicker'; Next = 'async function resolveSelectedCandidateDetail' },
    @{ Label = 'Episode picker'; Signature = 'async function resolveSelectedCandidateDetail'; Next = 'function renderItemCandidatePicker' },
    @{ Label = 'Episode progress'; Signature = 'async function renderSingleTargetProgress'; Next = 'function renderInitialSearchFailure' }
)
foreach ($protected in $protectedFrontendMethods) {
    Assert-R6MethodRegionUnchanged 'Frontend/DanmuSmartMatch.CustomCssJS.js' $protected.Signature $protected.Next $protected.Label
}

$controller = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot 'Core/Controllers/DanmuController.cs')
foreach ($required in @(
    'TryResolve(selection.SelectionEvidenceToken',
    'selectionEvidence.MatchScore',
    'selectionEvidence.ScoreOrigin',
    'context, canonicalSelections, plan)'
)) {
    if (-not $controller.Contains($required)) { throw "Protected evidence/fingerprint contract missing: $required" }
}

Write-Output '2.0.3r7 narrow-delta scope check passed against the verified 2.0.3r6 workspace.'
