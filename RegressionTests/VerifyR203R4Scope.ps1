param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$Baseline = '1604f56974bebc37d067b1e67db65d39bf3b8415'
)

$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $RepositoryRoot
try {
    if ((git cat-file -t $Baseline).Trim() -ne 'commit') {
        throw "Required frozen r3 baseline is unavailable: $Baseline"
    }
    $changed = @(
        @(git diff --name-only --diff-filter=ACDMRTUXB $Baseline --) +
        @(git ls-files --others --exclude-standard) |
        ForEach-Object { $_.Replace('\', '/') } | Sort-Object -Unique
    )
    $allowedPatterns = @(
        '^Core/Controllers/DanmuController\.cs$',
        '^Core/Season(PlanGenerationCoordinator|PlanningContext|TargetPlanningCoordinator)\.cs$',
        '^Emby\.Plugin\.Danmu\.csproj$',
        '^Frontend/DanmuSmartMatch\.(CustomCssJS\.js|RegressionTests\.js)$',
        '^LibraryManagerEventsHelper\.cs$',
        '^Model/(CompositeSeasonMatch|CompositeSeasonOwnership|DanmuMatchResult)\.cs$',
        '^Scraper/CompositeSeason(MatchService|Planner)\.cs$',
        '^RegressionTests/.+$',
        '^artifacts/2\.0\.3r4/.+$'
    )
    $unexpected = @($changed | Where-Object {
        $path = $_
        -not ($allowedPatterns | Where-Object { $path -match $_ })
    })
    if ($unexpected.Count) { throw "Unexpected r4 files: $($unexpected -join ', ')" }

    $forbiddenFiles = @(
        'Scraper/DanmuSeasonSegmentPlanner.cs',
        'Scraper/DanmuSeasonSegmentPlanBuilder.cs',
        'Scraper/DanmuSeasonCollectionPlanner.cs',
        'Scraper/DanmuSeasonCollectionMatcher.cs',
        'Scraper/DanmuTitleClauseExtractor.cs'
    )
    $present = @($forbiddenFiles | Where-Object { Test-Path -LiteralPath $_ })
    if ($present.Count) { throw "Forbidden experimental/dynamic files: $($present -join ', ')" }

    [xml]$project = Get-Content -Raw Emby.Plugin.Danmu.csproj
    $properties = $project.Project.PropertyGroup | Where-Object AssemblyVersion | Select-Object -First 1
    if ($properties.TargetFramework -ne 'netstandard2.0' -or $properties.LangVersion -ne '8.0' -or
        $properties.AssemblyVersion -ne '2.0.3.0' -or $properties.FileVersion -ne '2.0.3.4' -or
        $properties.InformationalVersion -ne '2.0.3r4') {
        throw 'r4 target/version contract is not exact.'
    }
    $frontend = Get-Content -Raw Frontend/DanmuSmartMatch.CustomCssJS.js
    if ([regex]::Matches($frontend, '__embyDanmuSmartMenuV20').Count -ne 1 -or
        $frontend.Contains('__embyDanmuSmartMenuV19')) {
        throw 'The paired frontend must contain V20 exactly once and no V19.'
    }
    Write-Output "2.0.3r4 scope verification passed against $Baseline ($($changed.Count) changed files)."
}
finally { Pop-Location }
