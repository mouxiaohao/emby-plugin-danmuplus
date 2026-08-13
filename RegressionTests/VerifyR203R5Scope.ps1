param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$Baseline = '5f980931370343af403fa4a3c3a011e747176abd'
)

$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $RepositoryRoot
try {
    if ((git cat-file -t $Baseline).Trim() -ne 'commit') {
        throw "Required frozen deployed r4 baseline is unavailable: $Baseline"
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
        '^RegressionTests/(BoundedSearchPolicy/Program\.cs|CompositeSeasonPlannerTests\.cs|Emby\.Plugin\.Danmu\.RegressionTests\.csproj|EpisodeSelectionPolicy/Program\.cs|Program\.cs|R4GenerationPolicyTests\.cs|R4IdentifierMetamorphic/Program\.cs|TemporaryRangePolicy/Program\.cs|VerifyR203R5Scope\.ps1)$',
        '^RegressionTests/R5TargetSeasonScope/(Program\.cs|R5TargetSeasonScope\.csproj)$',
        '^openspec/changes/release-2-0-3-r5-ignore-cross-season-episodes/.+$',
        '^openspec/config\.yaml$',
        '^openspec/specs/(parent-season-aware-episode-mapping|season-danmu-matching|season-episode-scope-filtering|smart-match-error-and-presentation)/spec\.md$',
        '^artifacts/2\.0\.3r5/(DanmuSmartMatch\.CustomCssJS\.js|Emby\.Plugin\.Danmu\.dll|VERIFICATION\.md|restart_emby\.sh|update_customcssjs\.py)$',
        '^artifacts/2\.0\.3r5/evidence/(predeployment-blocked|deployment-readonly-acceptance|fixture-index-blocked-cleanup)-20260813\.md$'
    )
    $unexpected = @($changed | Where-Object {
        $path = $_
        -not ($allowedPatterns | Where-Object { $path -match $_ })
    })
    if ($unexpected.Count) { throw "Unexpected r5 files: $($unexpected -join ', ')" }

    $forbiddenFiles = @(
        'Scraper/DanmuSeasonSegmentPlanner.cs',
        'Scraper/DanmuSeasonSegmentPlanBuilder.cs',
        'Scraper/DanmuSeasonCollectionPlanner.cs',
        'Scraper/DanmuSeasonCollectionMatcher.cs',
        'Scraper/DanmuTitleClauseExtractor.cs'
    )
    $present = @($forbiddenFiles | Where-Object { Test-Path -LiteralPath $_ })
    $experimentalChanged = @($changed | Where-Object {
        $_ -match '(?i)(^|/)(experimental|experiments?)(/|$)' -or
        $_ -match '(?i)(experiment|prototype|dynamic.?title)'
    })
    if ($present.Count -or $experimentalChanged.Count) {
        throw "Forbidden experimental files: $(($present + $experimentalChanged | Sort-Object -Unique) -join ', ')"
    }

    [xml]$project = Get-Content -Raw Emby.Plugin.Danmu.csproj
    $properties = $project.Project.PropertyGroup | Where-Object AssemblyVersion | Select-Object -First 1
    if ($properties.TargetFramework -ne 'netstandard2.0' -or $properties.LangVersion -ne '8.0' -or
        $properties.AssemblyVersion -ne '2.0.3.0' -or $properties.FileVersion -ne '2.0.3.5' -or
        $properties.InformationalVersion -ne '2.0.3r5') {
        throw 'r5 target/version contract is not exact.'
    }

    $frontend = Get-Content -Raw Frontend/DanmuSmartMatch.CustomCssJS.js
    if ([regex]::Matches($frontend, '__embyDanmuSmartMenuV21').Count -ne 1 -or
        $frontend.Contains('__embyDanmuSmartMenuV20') -or
        $frontend.Contains('__embyDanmuSmartMenuV19')) {
        throw 'The paired frontend must contain V21 exactly once and no V20/V19 marker.'
    }
    $protocol = Get-Content -Raw Core/SeasonPlanGenerationCoordinator.cs
    if ([regex]::Matches($protocol, 'public const int CurrentVersion\s*=\s*21\s*;').Count -ne 1 -or
        $protocol -match 'public const int CurrentVersion\s*=\s*20\s*;') {
        throw 'The batch protocol must declare V21 exactly once and no V20 current version.'
    }

    $mainParent = Get-Content -Raw openspec/specs/parent-season-aware-episode-mapping/spec.md
    $mainSeason = Get-Content -Raw openspec/specs/season-danmu-matching/spec.md
    $mainScope = Get-Content -Raw openspec/specs/season-episode-scope-filtering/spec.md
    $deltaParent = Get-Content -Raw openspec/changes/release-2-0-3-r5-ignore-cross-season-episodes/specs/parent-season-aware-episode-mapping/spec.md
    $deltaSeason = Get-Content -Raw openspec/changes/release-2-0-3-r5-ignore-cross-season-episodes/specs/season-danmu-matching/spec.md
    $deltaScope = Get-Content -Raw openspec/changes/release-2-0-3-r5-ignore-cross-season-episodes/specs/season-episode-scope-filtering/spec.md
    $effectiveSpecs = $mainParent + $mainSeason + $mainScope + $deltaParent + $deltaSeason + $deltaScope
    $obsoleteNormativePhrases = @(
        'Episodes from another parent season or an unknown parent season SHALL require an explicit/high-confidence supplemental selection',
        'the seven S00 Episodes SHALL form exactly one unmatched temporary run',
        'the result SHALL contain a 12-Episode main group and one seven-Episode special run',
        'shared planner SHALL keep those Episodes visible as separate logical-season runs'
    )
    $obsolete = @($obsoleteNormativePhrases | Where-Object { $effectiveSpecs.Contains($_) })
    if ($obsolete.Count) { throw "Obsolete normal-Season foreign supplemental requirement remains: $($obsolete -join '; ')" }
    if (-not $mainScope.Contains('out-of-scope Episodes SHALL never become temporary seasons') -or
        -not $mainParent.Contains('MUST NOT become mappings, unmatched runs, temporary seasons, supplemental selections, downloads, or completeness inputs') -or
        -not $mainSeason.Contains('SHALL exclude those Episodes before scoring and mapping')) {
        throw 'Effective main specs do not positively seal the r5 foreign/unknown exclusion contract.'
    }

    Write-Output "2.0.3r5 scope/spec verification passed against deployed r4 $Baseline ($($changed.Count) changed files)."
}
finally { Pop-Location }
