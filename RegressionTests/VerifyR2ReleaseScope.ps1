param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $RepositoryRoot

$r1Commit = 'bd748e7'
if ((git cat-file -t $r1Commit).Trim() -ne 'commit') {
    throw "Required 2.0.2r1 baseline commit is unavailable: $r1Commit"
}

$approvedFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
@(
    'Configuration/PluginConfiguration.cs',
    'Core/Controllers/DanmuController.cs',
    'Emby.Plugin.Danmu.csproj',
    'Frontend/DanmuSmartMatch.CustomCssJS.js',
    'Frontend/DanmuSmartMatch.RegressionTests.js',
    'README.md',
    'RegressionTests/Program.cs',
    'RegressionTests/VerifyR2ReleaseScope.ps1',
    'Scraper/Bilibili/Bilibili.cs',
    'Scraper/Bilibili/BilibiliApi.cs',
    'Scraper/Bilibili/Entity/Video.cs',
    'Scraper/Bilibili/Entity/VideoPart.cs',
    'Scraper/Bilibili/Entity/VideoSeasonDetail.cs',
    'Scraper/Bilibili/Entity/VideoUgcSeason.cs',
    'Scraper/Dandan/Dandan.cs',
    'Scraper/DanmuProviderIdResolver.cs',
    'Scraper/Entity/ScraperMedia.cs',
    'Scraper/Iqiyi/Iqiyi.cs',
    'Scraper/Mgtv/Mgtv.cs',
    'Scraper/Tencent/Tencent.cs',
    'Scraper/Youku/Youku.cs'
) | ForEach-Object { [void]$approvedFiles.Add($_) }
$approvedPrefixes = @(
    'artifacts/2.0.2r2/',
    'openspec/changes/release-2-0-2-r2-provider-id-metadata/'
)
function Test-ApprovedCandidatePath([string]$path) {
    if ($approvedFiles.Contains($path)) { return $true }
    return @($approvedPrefixes | Where-Object { $path.StartsWith($_, [System.StringComparison]::Ordinal) }).Count -gt 0
}

$violations = [System.Collections.Generic.List[string]]::new()
$temporaryIndex = Join-Path ([IO.Path]::GetTempPath()) ('danmu-r2-scope-' + [guid]::NewGuid().ToString('N'))
$previousIndex = $env:GIT_INDEX_FILE
try {
    $env:GIT_INDEX_FILE = $temporaryIndex
    git read-tree $r1Commit
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
    throw "2.0.2r2 release scope violations relative to ${r1Commit}: $($violations -join ', ')"
}

[xml]$project = Get-Content -Raw -LiteralPath 'Emby.Plugin.Danmu.csproj'
$properties = $project.Project.PropertyGroup | Where-Object { $_.AssemblyVersion } | Select-Object -First 1
if ($properties.AssemblyVersion -ne '2.0.2.0' -or
    $properties.FileVersion -ne '2.0.2.2' -or
    $properties.InformationalVersion -ne '2.0.2r2') {
    throw '2.0.2r2 project version contract is not exact.'
}
if (-not (Select-String -LiteralPath 'Configuration/PluginConfiguration.cs' -Pattern 'Version \{ get; \} = "2\.0\.2r2"' -Quiet) -or
    -not (Select-String -LiteralPath 'Frontend/DanmuSmartMatch.CustomCssJS.js' -Pattern '__embyDanmuSmartMenuV13' -Quiet)) {
    throw '2.0.2r2 configuration or V13 frontend marker is missing.'
}

Write-Output '2.0.2r2 release scope check passed.'
