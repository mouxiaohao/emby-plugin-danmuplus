param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $RepositoryRoot

$r2Commit = '3a41be8'
if ((git cat-file -t $r2Commit).Trim() -ne 'commit') {
    throw "Required 2.0.2r2 baseline commit is unavailable: $r2Commit"
}

$approvedFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
@(
    'Configuration/PluginConfiguration.cs',
    'Core/Controllers/DanmuController.cs',
    'Core/ProviderWriteGenerationTracker.cs',
    'Emby.Plugin.Danmu.csproj',
    'Frontend/DanmuSmartMatch.CustomCssJS.js',
    'Frontend/DanmuSmartMatch.RegressionTests.js',
    'LibraryManagerEventsHelper.cs',
    'Model/DanmuMatchResult.cs',
    'README.md',
    'RegressionTests/Program.cs',
    'RegressionTests/VerifyR3ReleaseScope.ps1',
    'Scraper/Bilibili/Bilibili.cs',
    'Scraper/Bilibili/BilibiliPgcIdPolicy.cs',
    'Scraper/Bilibili/ExternalId/EpisodeExternalId.cs',
    'Scraper/Bilibili/ExternalId/MovieExternalId.cs',
    'Scraper/Bilibili/ExternalId/SeasonExternalId.cs',
    'Scraper/Dandan/Dandan.cs',
    'Scraper/Dandan/DandanSeasonEpisodeMapper.cs',
    'Scraper/DanmuMatchSearchEngine.cs',
    'Scraper/DanmuMovieMatchHelper.cs',
    'Scraper/DanmuProviderIdResolver.cs',
    'Scraper/DanmuTitleClauseExtractor.cs',
    'Scraper/Mgtv/ExternalId/SeasonExternalId.cs'
) | ForEach-Object { [void]$approvedFiles.Add($_) }
$approvedPrefixes = @(
    'artifacts/2.0.2r3/',
    'openspec/changes/fix-anime-season-smart-match/'
)
function Test-ApprovedCandidatePath([string]$path) {
    if ($approvedFiles.Contains($path)) { return $true }
    return @($approvedPrefixes | Where-Object { $path.StartsWith($_, [System.StringComparison]::Ordinal) }).Count -gt 0
}

$violations = [System.Collections.Generic.List[string]]::new()
$temporaryIndex = Join-Path ([IO.Path]::GetTempPath()) ('danmu-r3-scope-' + [guid]::NewGuid().ToString('N'))
$previousIndex = $env:GIT_INDEX_FILE
try {
    $env:GIT_INDEX_FILE = $temporaryIndex
    git read-tree $r2Commit
    git update-index --really-refresh *> $null
    foreach ($path in @(git diff-files --name-only)) {
        if (-not (Test-ApprovedCandidatePath $path)) { $violations.Add("modified-or-deleted: $path") }
    }
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
    throw "2.0.2r3 release scope violations relative to ${r2Commit}: $($violations -join ', ')"
}

[xml]$project = Get-Content -Raw -LiteralPath 'Emby.Plugin.Danmu.csproj'
$properties = $project.Project.PropertyGroup | Where-Object { $_.AssemblyVersion } | Select-Object -First 1
if ($properties.AssemblyVersion -ne '2.0.2.0' -or
    $properties.FileVersion -ne '2.0.2.3' -or
    $properties.InformationalVersion -ne '2.0.2r3') {
    throw '2.0.2r3 project version contract is not exact.'
}
if (-not (Select-String -LiteralPath 'Configuration/PluginConfiguration.cs' -Pattern 'Version \{ get; \} = "2\.0\.2r3"' -Quiet) -or
    -not (Select-String -LiteralPath 'Frontend/DanmuSmartMatch.CustomCssJS.js' -Pattern '__embyDanmuSmartMenuV14' -Quiet)) {
    throw '2.0.2r3 configuration or V14 frontend marker is missing.'
}

Write-Output '2.0.2r3 release scope check passed.'
