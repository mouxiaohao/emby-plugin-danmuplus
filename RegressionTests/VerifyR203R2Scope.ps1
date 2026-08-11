param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$Baseline = 'ca58ad389d2ae1c7d1660ab23d78efb297fbf3ea'
)

$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $RepositoryRoot
try {
    if ((git cat-file -t $Baseline).Trim() -ne 'commit') {
        throw "Required 2.0.3r1 baseline commit is unavailable: $Baseline"
    }

    $trackedChanges = @(git diff --name-only --diff-filter=ACDMRTUXB $Baseline --)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to compare the worktree with $Baseline."
    }
    $untrackedChanges = @(git ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to enumerate untracked worktree files.'
    }
    $changed = @($trackedChanges + $untrackedChanges |
        ForEach-Object { $_.Replace('\', '/') } |
        Sort-Object -Unique)

    $approvedFiles = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    @(
        '.gitattributes',
        'Core/BoundedSearchPolicy.cs',
        'Core/Controllers/DanmuController.cs',
        'Core/Controllers/DanmuDispatchOption.cs',
        'Core/SearchOperationRegistry.cs',
        'Emby.Plugin.Danmu.csproj',
        'Frontend/DanmuSmartMatch.CustomCssJS.js',
        'Frontend/DanmuSmartMatch.RegressionTests.js',
        'LibraryManagerEventsHelper.cs',
        'Model/CompositeSeasonMatch.cs',
        'Model/DanmuMatchResult.cs',
        'Model/DanmuTemporaryRangeSearchPolicy.cs',
        'RegressionTests/BoundedSearchFoundationTests.cs',
        'RegressionTests/BoundedSearchPolicy/BoundedSearchPolicy.csproj',
        'RegressionTests/BoundedSearchPolicy/Program.cs',
        'RegressionTests/CompositeSeasonPlannerTests.cs',
        'RegressionTests/Emby.Plugin.Danmu.RegressionTests.csproj',
        'RegressionTests/EpisodeSelectionPolicy/EpisodeSelectionPolicy.csproj',
        'RegressionTests/EpisodeSelectionPolicy/Program.cs',
        'RegressionTests/Program.cs',
        'RegressionTests/SearchTermPolicy/Program.cs',
        'RegressionTests/SearchTermPolicy/SearchTermPolicy.csproj',
        'RegressionTests/TemporaryRangePolicy/Program.cs',
        'RegressionTests/TemporaryRangePolicy/TemporaryRangePolicy.csproj',
        'RegressionTests/VerifyR203R2Scope.ps1',
        'Scraper/AbstractApi.cs',
        'Scraper/AbstractScraper.cs',
        'Scraper/Bilibili/Bilibili.cs',
        'Scraper/Bilibili/BilibiliApi.cs',
        'Scraper/CompositeSeasonMatchService.cs',
        'Scraper/CompositeSeasonPlanner.cs',
        'Scraper/Dandan/Dandan.cs',
        'Scraper/Dandan/DandanApi.cs',
        'Scraper/DanmuExactEpisodeSelectionHelper.cs',
        'Scraper/DanmuMatchScorer.cs',
        'Scraper/DanmuMatchSearchEngine.cs',
        'Scraper/DanmuProviderIdResolver.cs',
        'Scraper/DanmuTitleClauseExtractor.cs',
        'Scraper/Iqiyi/Iqiyi.cs',
        'Scraper/Iqiyi/IqiyiApi.cs',
        'Scraper/Mgtv/Mgtv.cs',
        'Scraper/Mgtv/MgtvApi.cs',
        'Scraper/Tencent/Tencent.cs',
        'Scraper/Tencent/TencentApi.cs',
        'Scraper/Youku/Youku.cs',
        'Scraper/Youku/YoukuApi.cs',
        'artifacts/2.0.3r2/DanmuSmartMatch.CustomCssJS.js',
        'artifacts/2.0.3r2/Emby.Plugin.Danmu.dll',
        'artifacts/2.0.3r2/VERIFICATION.md',
        'artifacts/2.0.3r2/restart_emby.sh',
        'artifacts/2.0.3r2/update_customcssjs.py'
    ) | ForEach-Object { [void]$approvedFiles.Add($_) }

    $unexpected = @($changed | Where-Object { -not $approvedFiles.Contains($_) })
    if ($unexpected.Count -gt 0) {
        throw "Unexpected 2.0.3r2 file(s): $($unexpected -join ', ')"
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

    $productionPaths = @(
        'Configuration', 'Core', 'Frontend', 'Model', 'Scraper',
        'LibraryManagerEventsHelper.cs'
    )
    $productionFiles = @(Get-ChildItem -Path $productionPaths -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in @('.cs', '.js') })
    $forbiddenExperimentalSymbols =
        '(?i)DanmuSeasonSegment|DanmuSeasonCollection|SegmentSelections|CollectionSelections|unmatched-temp|direct-temp'
    $experimentalHits = @($productionFiles | Select-String -Pattern $forbiddenExperimentalSymbols)
    if ($experimentalHits.Count -gt 0) {
        $locations = @($experimentalHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" } |
            Sort-Object -Unique)
        throw "Experimental segment/collection symbols are forbidden: $($locations -join ', ')"
    }

    if (Test-Path -LiteralPath 'Scraper/DanmuTitleClauseExtractor.cs') {
        throw 'The dynamic title-clause extractor must remain deleted in 2.0.3r2.'
    }
    $dynamicNameHits = @($productionFiles | Select-String -Pattern 'DanmuTitleClauseExtractor|ExtractProviderAliases|ExtractTitleClauses|AliasOnly|aliasDiscovered')
    if ($dynamicNameHits.Count -gt 0) {
        $locations = @($dynamicNameHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" } |
            Sort-Object -Unique)
        throw "Dynamic-name production symbol(s) detected: $($locations -join ', ')"
    }

    [xml]$project = Get-Content -Raw -LiteralPath 'Emby.Plugin.Danmu.csproj'
    $properties = $project.Project.PropertyGroup |
        Where-Object { $_.AssemblyVersion } | Select-Object -First 1
    if ($properties.TargetFramework -ne 'netstandard2.0' -or
        $properties.LangVersion -ne '8.0') {
        throw 'The release project must target netstandard2.0 with C# 8.0.'
    }
    if ($properties.AssemblyVersion -ne '2.0.3.0' -or
        $properties.FileVersion -ne '2.0.3.2' -or
        $properties.InformationalVersion -ne '2.0.3r2') {
        throw 'The 2.0.3r2 assembly/file/informational version contract is not exact.'
    }

    $frontend = Get-Content -Raw -LiteralPath 'Frontend/DanmuSmartMatch.CustomCssJS.js'
    if ([regex]::Matches($frontend, '__embyDanmuSmartMenuV18').Count -ne 1) {
        throw 'The paired frontend must contain the V18 installation marker exactly once.'
    }

    $boundedPolicy = Get-Content -Raw -LiteralPath 'Core/BoundedSearchPolicy.cs'
    $boundedContracts = @(
        'providerCallTimeout ?? TimeSpan.FromSeconds(10)',
        'interactiveOperationTimeout ?? TimeSpan.FromSeconds(30)',
        'automaticOperationTimeout ?? TimeSpan.FromSeconds(45)',
        'int maximumConcurrentProviders = 3',
        'new SemaphoreSlim(1, 1)'
    )
    $missingBoundedContracts = @($boundedContracts | Where-Object {
        $boundedPolicy.IndexOf($_, [System.StringComparison]::Ordinal) -lt 0
    })
    if ($missingBoundedContracts.Count -gt 0) {
        throw "Bounded-search production constant(s) missing: $($missingBoundedContracts -join ', ')"
    }

    Write-Output "2.0.3r2 scope verification passed against $Baseline ($($changed.Count) changed files)."
}
finally {
    Pop-Location
}
