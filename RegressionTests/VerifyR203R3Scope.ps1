param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$Baseline = '48fdaa986b5c10eca73bb692e0fe63ef123c2935'
)

$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $RepositoryRoot
try {
    if ((git cat-file -t $Baseline).Trim() -ne 'commit') {
        throw "Required clean r2 baseline is unavailable: $Baseline"
    }
    $changed = @(
        @(git diff --name-only --diff-filter=ACDMRTUXB $Baseline --) +
        @(git ls-files --others --exclude-standard) |
        ForEach-Object { $_.Replace('\', '/') } | Sort-Object -Unique
    )
    $allowedPatterns = @(
        '^Core/(BoundedSearchPolicy|SearchOperationRegistry|DanmuCandidateEvidenceRegistry)\.cs$',
        '^Core/Controllers/Danmu(Controller|DispatchOption)\.cs$',
        '^Emby\.Plugin\.Danmu\.csproj$',
        '^Frontend/DanmuSmartMatch\.(CustomCssJS\.js|RegressionTests\.js)$',
        '^LibraryManagerEventsHelper\.cs$',
        '^Model/(CompositeSeasonMatch|DanmuMatchResult|DanmuTemporaryRangeSearchPolicy)\.cs$',
        '^Scraper/(CompositeSeason.*|DanmuExactEpisodeSelectionHelper|DanmuMatchScorer|DanmuMatchSearchEngine|DanmuProviderIdResolver|DanmuSeasonSearchTermPolicy|DanmuCandidateEligibilityPolicy)\.cs$',
        '^RegressionTests/.+$',
        '^artifacts/2\.0\.3r3/.+$'
    )
    $unexpected = @($changed | Where-Object {
        $path = $_
        -not ($allowedPatterns | Where-Object { $path -match $_ })
    })
    if ($unexpected.Count) { throw "Unexpected r3 files: $($unexpected -join ', ')" }

    $forbiddenFiles = @(
        'Scraper/DanmuSeasonSegmentPlanner.cs',
        'Scraper/DanmuSeasonSegmentPlanBuilder.cs',
        'Scraper/DanmuSeasonCollectionPlanner.cs',
        'Scraper/DanmuSeasonCollectionMatcher.cs',
        'Scraper/DanmuTitleClauseExtractor.cs'
    )
    $present = @($forbiddenFiles | Where-Object { Test-Path -LiteralPath $_ })
    if ($present.Count) { throw "Forbidden experimental/dynamic files: $($present -join ', ')" }

    $production = @(Get-ChildItem Configuration,Core,Frontend,Model,Scraper -Recurse -File |
        Where-Object Extension -in '.cs','.js')
    $forbiddenSymbols =
        'DanmuSeasonSegment|DanmuSeasonCollection|DanmuTitleClauseExtractor|ExtractProviderAliases|ExtractTitleClauses|AliasOnly|aliasDiscovered'
    $hits = @($production | Select-String -Pattern $forbiddenSymbols)
    if ($hits.Count) { throw 'Experimental segment/collection or dynamic-name symbol detected.' }

    [xml]$project = Get-Content -Raw Emby.Plugin.Danmu.csproj
    $properties = $project.Project.PropertyGroup | Where-Object AssemblyVersion | Select-Object -First 1
    if ($properties.TargetFramework -ne 'netstandard2.0' -or $properties.LangVersion -ne '8.0' -or
        $properties.AssemblyVersion -ne '2.0.3.0' -or $properties.FileVersion -ne '2.0.3.3' -or
        $properties.InformationalVersion -ne '2.0.3r3') {
        throw 'r3 target/version contract is not exact.'
    }
    $frontend = Get-Content -Raw Frontend/DanmuSmartMatch.CustomCssJS.js
    if ([regex]::Matches($frontend, '__embyDanmuSmartMenuV19').Count -ne 1 -or
        $frontend.Contains('__embyDanmuSmartMenuV18')) {
        throw 'The paired frontend must contain V19 exactly once and no V18.'
    }
    Write-Output "2.0.3r3 scope verification passed against $Baseline ($($changed.Count) changed files)."
}
finally { Pop-Location }
