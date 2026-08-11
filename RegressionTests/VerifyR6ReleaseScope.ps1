param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $RepositoryRoot

$r6Tree = '32fa2af0e97e12cdb5837b5874991d65137bf078'
if ((git cat-file -t $r6Tree).Trim() -ne 'tree') {
    throw "Required r6 baseline tree is unavailable: $r6Tree"
}

$approvedFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
@(
    'Configuration/PluginConfiguration.cs',
    'Emby.Plugin.Danmu.csproj',
    'Frontend/DanmuSmartMatch.CustomCssJS.js',
    'Frontend/DanmuSmartMatch.RegressionTests.js',
    'README.md',
    'RegressionTests/VerifyR6ReleaseScope.ps1'
) | ForEach-Object { [void]$approvedFiles.Add($_) }
$approvedPrefixes = @(
    'artifacts/2.0.2r1/',
    'openspec/changes/release-2-0-2-r1-smart-match-dialog/'
)
function Test-ApprovedCandidatePath([string]$path) {
    if ($approvedFiles.Contains($path)) { return $true }
    return @($approvedPrefixes | Where-Object { $path.StartsWith($_, [System.StringComparison]::Ordinal) }).Count -gt 0
}

$violations = [System.Collections.Generic.List[string]]::new()
$temporaryIndex = Join-Path ([IO.Path]::GetTempPath()) ('danmu-r6-scope-' + [guid]::NewGuid().ToString('N'))
$previousIndex = $env:GIT_INDEX_FILE
try {
    # A private index populated from the exact tree makes Git compare every
    # baseline path against the actual worktree, including baseline files that
    # are untracked by the recovery branch.  It never changes the real index.
    $env:GIT_INDEX_FILE = $temporaryIndex
    git read-tree $r6Tree
    git update-index --really-refresh *> $null
    foreach ($path in @(git diff-files --name-only)) {
        if (-not (Test-ApprovedCandidatePath $path)) { $violations.Add("modified-or-deleted: $path") }
    }

    # The same private index treats every baseline path as tracked, so this
    # catches every additional non-ignored file rather than relying on git diff.
    foreach ($path in @(git ls-files --others --exclude-standard)) {
        if (-not (Test-ApprovedCandidatePath $path)) { $violations.Add("untracked: $path") }
    }
}
finally {
    if ($null -eq $previousIndex) {
        [Environment]::SetEnvironmentVariable('GIT_INDEX_FILE', $null, 'Process')
    }
    else {
        $env:GIT_INDEX_FILE = $previousIndex
    }
    [IO.File]::Delete($temporaryIndex)
}
if ($violations.Count -gt 0) {
    throw "r6 release scope violations relative to ${r6Tree}: $($violations -join ', ')"
}

[xml]$project = Get-Content -Raw -LiteralPath 'Emby.Plugin.Danmu.csproj'
$properties = $project.Project.PropertyGroup | Where-Object { $_.AssemblyVersion } | Select-Object -First 1
if ($properties.AssemblyVersion -ne '2.0.2.0' -or
    $properties.FileVersion -ne '2.0.2.1' -or
    $properties.InformationalVersion -ne '2.0.2r1') {
    throw '2.0.2r1 project version contract is not exact.'
}
if (-not (Select-String -LiteralPath 'Configuration/PluginConfiguration.cs' -Pattern 'Version \{ get; \} = "2\.0\.2r1"' -Quiet) -or
    -not (Select-String -LiteralPath 'Frontend/DanmuSmartMatch.CustomCssJS.js' -Pattern '__embyDanmuSmartMenuV12' -Quiet)) {
    throw '2.0.2r1 configuration or V12 frontend marker is missing.'
}

# r6 already has provider download *segments*.  Only reject the later seasonal
# segment/collection protocol names, not provider-internal comment chunking.
$forbidden = '(?i)DanmuSeasonSegment|DanmuSeasonCollection|SegmentSelections|CollectionSelections|temporary-season|unmatched-temp|direct-temp'
$scanPaths = @('Frontend', 'Configuration', 'Core', 'Model', 'Scraper', 'LibraryManagerEventsHelper.cs')
$matches = @(Get-ChildItem -Path $scanPaths -Recurse -File -ErrorAction SilentlyContinue |
    Select-String -Pattern $forbidden)
if ($matches.Count -gt 0) {
    $locations = $matches | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "r7/r8 season segment or collection symbols are forbidden: $($locations -join ', ')"
}

Write-Output 'r6 release scope check passed.'
