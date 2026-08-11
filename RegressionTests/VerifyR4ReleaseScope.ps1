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
    'dist/Emby.Plugin.Danmu.dll',
    'dist/emby-plugin-danmuplus-2.0.1-r5-source.zip',
    'dist/emby-plugin-danmuplus-2.0.2r4-source.zip',
    'Emby.Plugin.Danmu.csproj',
    'Frontend/DanmuSmartMatch.CustomCssJS.js',
    'Frontend/DanmuSmartMatch.RegressionTests.js',
    'LibraryManagerEventsHelper.cs',
    'Model/DanmuMatchResult.cs',
    'openspec/changes/add-official-dandan-cors-option/tasks.md',
    'README.md',
    'RegressionTests/Program.cs',
    'RegressionTests/VerifyR3ReleaseScope.ps1',
    'RegressionTests/VerifyR4ReleaseScope.ps1',
    'Scraper/Bilibili/Bilibili.cs',
    'Scraper/Bilibili/BilibiliPgcIdPolicy.cs',
    'Scraper/Bilibili/ExternalId/EpisodeExternalId.cs',
    'Scraper/Bilibili/ExternalId/MovieExternalId.cs',
    'Scraper/Bilibili/ExternalId/SeasonExternalId.cs',
    'Scraper/Dandan/Dandan.cs',
    'Scraper/Dandan/DandanSeasonEpisodeMapper.cs',
    'Scraper/DanmuMatchScorer.cs',
    'Scraper/DanmuMatchSearchEngine.cs',
    'Scraper/DanmuMovieMatchHelper.cs',
    'Scraper/DanmuProviderIdResolver.cs',
    'Scraper/DanmuProviderIdWritePolicy.cs',
    'Scraper/DanmuTitleClauseExtractor.cs',
    'Scraper/Mgtv/ExternalId/SeasonExternalId.cs',
    'UPDATE.md'
) | ForEach-Object { [void]$approvedFiles.Add($_) }

$approvedPrefixes = @(
    'artifacts/2.0.2r3/',
    'artifacts/2.0.2r4/',
    'openspec/changes/fix-anime-season-smart-match/',
    'openspec/changes/extend-r4-alias-and-provider-id-policy/',
    'releases/v2.0.1-r5/'
)

function Test-ApprovedCandidatePath([string]$path) {
    if ($approvedFiles.Contains($path)) { return $true }
    return @($approvedPrefixes | Where-Object {
        $path.StartsWith($_, [System.StringComparison]::Ordinal)
    }).Count -gt 0
}

$violations = [System.Collections.Generic.List[string]]::new()
$temporaryIndex = Join-Path ([IO.Path]::GetTempPath()) ('danmu-r4-scope-' + [guid]::NewGuid().ToString('N'))
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
    throw "2.0.2r4 release scope violations relative to ${r2Commit}: $($violations -join ', ')"
}

[xml]$project = Get-Content -Raw -LiteralPath 'Emby.Plugin.Danmu.csproj'
$properties = $project.Project.PropertyGroup | Where-Object { $_.AssemblyVersion } | Select-Object -First 1
if ($properties.AssemblyVersion -ne '2.0.2.0' -or
    $properties.FileVersion -ne '2.0.2.4' -or
    $properties.InformationalVersion -ne '2.0.2r4') {
    throw '2.0.2r4 project version contract is not exact.'
}
if (-not (Select-String -LiteralPath 'Configuration/PluginConfiguration.cs' -Pattern 'Version \{ get; \} = "2\.0\.2r4"' -Quiet) -or
    -not (Select-String -LiteralPath 'Frontend/DanmuSmartMatch.CustomCssJS.js' -Pattern '__embyDanmuSmartMenuV15' -Quiet)) {
    throw '2.0.2r4 configuration or V15 frontend marker is missing.'
}

Write-Output '2.0.2r4 release scope check passed.'
