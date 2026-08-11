param(
    [string]$Baseline = "v2.0.2r4"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $trackedChanges = @(git diff --name-only --diff-filter=ACMRTUXB $Baseline --)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to compare the worktree with $Baseline."
    }
    $untrackedChanges = @(git ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to enumerate untracked worktree files."
    }
    $changed = @($trackedChanges + $untrackedChanges | Sort-Object -Unique)

    $allowed = @(
        '^Core/CompositeSeason[^/]*\.cs$',
        '^Core/Controllers/DanmuController\.cs$',
        '^Emby\.Plugin\.Danmu\.csproj$',
        '^Frontend/DanmuSmartMatch\.(CustomCssJS|RegressionTests)\.js$',
        '^LibraryManagerEventsHelper\.cs$',
        '^Model/(CompositeSeasonMatch|DanmuMatchResult)\.cs$',
        '^README\.md$',
        '^RegressionTests/(CompositeSeason[^/]*\.cs|Emby\.Plugin\.Danmu\.RegressionTests\.csproj|Program\.cs|VerifyR203R1Scope\.ps1)$',
        '^Scraper/CompositeSeason[^/]*\.cs$',
        '^Scraper/DanmuProviderIdResolver\.cs$',
        '^Scraper/Dandan/(Dandan|DandanApi|DandanSeasonEpisodeMapper)\.cs$',
        '^Scraper/Entity/ScraperMedia\.cs$',
        '^ServiceRegistrator\.cs$',
        '^UPDATE\.md$',
        '^artifacts/2\.0\.3r1/',
        '^openspec/changes/release-2-0-3-r1-composite-season-matching/'
    )

    $unexpected = @($changed | Where-Object {
        $path = $_
        -not ($allowed | Where-Object { $path -match $_ })
    })
    if ($unexpected.Count -gt 0) {
        throw "Unexpected 2.0.3r1 file(s): $($unexpected -join ', ')"
    }

    $forbiddenFiles = @(
        'Scraper/DanmuSeasonSegmentPlanner.cs',
        'Scraper/DanmuSeasonSegmentPlanBuilder.cs',
        'Scraper/DanmuSeasonCollectionPlanner.cs',
        'Scraper/DanmuSeasonCollectionMatcher.cs'
    )
    $presentForbidden = @($forbiddenFiles | Where-Object { Test-Path -LiteralPath $_ })
    if ($presentForbidden.Count -gt 0) {
        throw "Experimental implementation file(s) are forbidden: $($presentForbidden -join ', ')"
    }

    $sourceFiles = @($changed | Where-Object { $_ -match '\.(cs|js)$' -and (Test-Path -LiteralPath $_) })
    if ($sourceFiles.Count -gt 0) {
        $forbiddenSymbols = '(?i)DanmuSeasonSegment|DanmuSeasonCollection|SegmentSelections|CollectionSelections|temporary-season|unmatched-temp|direct-temp'
        $hits = @(Select-String -Path $sourceFiles -Pattern $forbiddenSymbols)
        if ($hits.Count -gt 0) {
            throw "Experimental symbol(s) detected: $($hits | ForEach-Object { $_.Path + ':' + $_.LineNumber } | Sort-Object -Unique -join ', ')"
        }
    }

    Write-Output "2.0.3r1 scope verification passed against $Baseline ($($changed.Count) changed files)."
}
finally {
    Pop-Location
}
