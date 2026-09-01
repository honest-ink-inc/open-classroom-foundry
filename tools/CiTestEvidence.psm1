# SPDX-License-Identifier: GPL-3.0-or-later
Set-StrictMode -Version Latest

function Assert-CiPathContained {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ContainmentRoot,

        [Parameter(Mandatory)]
        [string]$Path,

        [string]$Description = "CI evidence path"
    )

    $resolvedRoot = [IO.Path]::GetFullPath($ContainmentRoot)
    $resolvedPath = [IO.Path]::GetFullPath($Path)
    if (Test-Path -LiteralPath $resolvedRoot) {
        $rootAttributes = [IO.File]::GetAttributes($resolvedRoot)
        if (($rootAttributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Description containment root is a reparse point: $resolvedRoot"
        }
    }

    $relativePath = [IO.Path]::GetRelativePath($resolvedRoot, $resolvedPath)
    if ([IO.Path]::IsPathRooted($relativePath) -or
        $relativePath -eq ".." -or
        $relativePath.StartsWith("..\", [StringComparison]::Ordinal) -or
        $relativePath.StartsWith("../", [StringComparison]::Ordinal)) {
        throw "$Description escapes its containment root: $resolvedPath"
    }

    if ($relativePath -eq ".") {
        return
    }

    $currentPath = $resolvedRoot
    foreach ($segment in $relativePath.Split(
            [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar),
            [StringSplitOptions]::RemoveEmptyEntries)) {
        $currentPath = Join-Path $currentPath $segment
        if (-not (Test-Path -LiteralPath $currentPath)) {
            # A descendant cannot exist below the first missing component. The
            # caller validates again after creating or receiving the path.
            break
        }

        $attributes = [IO.File]::GetAttributes($currentPath)
        if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Description crosses a reparse point: $currentPath"
        }
    }
}

function Get-CiStringSha256 {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Value
    )

    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Value)
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}

function ConvertFrom-CiNulList {
    param([AllowEmptyString()][string]$Value)

    if (-not [string]::IsNullOrEmpty($Value)) {
        $Value.Split([char[]]@([char]0), [StringSplitOptions]::RemoveEmptyEntries)
    }
}

function Get-CiRepositoryState {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$RepositoryRoot)

    $resolvedRepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
    Assert-CiPathContained `
        -ContainmentRoot $resolvedRepositoryRoot `
        -Path $resolvedRepositoryRoot `
        -Description "repository source-identity root"

    $commit = (& git -C $resolvedRepositoryRoot rev-parse --verify HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-fA-F]{40}$') {
        throw "The repository commit could not be resolved."
    }
    $commit = $commit.ToLowerInvariant()

    $statusParts = @(& git -C $resolvedRepositoryRoot status --porcelain=v1 -z --untracked-files=all)
    if ($LASTEXITCODE -ne 0) {
        throw "The repository working-tree status could not be measured."
    }
    $statusBytes = $statusParts -join "`n"
    $statusEntries = @(ConvertFrom-CiNulList -Value $statusBytes)

    $trackedParts = @(& git -C $resolvedRepositoryRoot ls-files -z --cached)
    if ($LASTEXITCODE -ne 0) {
        throw "The tracked source inventory could not be measured."
    }
    $untrackedParts = @(& git -C $resolvedRepositoryRoot ls-files -z --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) {
        throw "The untracked source inventory could not be measured."
    }

    [string[]]$trackedPaths = @(ConvertFrom-CiNulList -Value ($trackedParts -join "`n"))
    [string[]]$untrackedPaths = @(ConvertFrom-CiNulList -Value ($untrackedParts -join "`n"))
    [Array]::Sort($trackedPaths, [StringComparer]::Ordinal)
    [Array]::Sort($untrackedPaths, [StringComparer]::Ordinal)

    $manifest = [Collections.Generic.List[string]]::new()
    $entries = @(
        @($trackedPaths | ForEach-Object { [pscustomobject]@{ Kind = "tracked"; Path = $_ } }) +
        @($untrackedPaths | ForEach-Object { [pscustomobject]@{ Kind = "untracked"; Path = $_ } }))
    foreach ($entry in $entries) {
        $fullPath = [IO.Path]::GetFullPath((Join-Path $resolvedRepositoryRoot $entry.Path))
        Assert-CiPathContained `
            -ContainmentRoot $resolvedRepositoryRoot `
            -Path $fullPath `
            -Description "repository source identity file"
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            $file = Get-Item -LiteralPath $fullPath -Force
            $hash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
            $manifest.Add("$($entry.Kind)`0$($entry.Path)`0$($file.Length)`0$hash")
        }
        else {
            $manifest.Add("$($entry.Kind)`0$($entry.Path)`0MISSING")
        }
    }

    [pscustomobject]@{
        Commit = $commit
        Dirty = $statusBytes.Length -gt 0
        StatusEntryCount = $statusEntries.Count
        StatusSha256 = Get-CiStringSha256 -Value $statusBytes
        SourceFileCount = $manifest.Count
        SourceContentSha256 = Get-CiStringSha256 -Value ($manifest -join "`n")
    }
}

function Get-CiTestAssemblySnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object[]]$Suites,
        [Parameter(Mandatory)][string]$RepositoryRoot)

    @($Suites | ForEach-Object {
            if ([string]::IsNullOrWhiteSpace([string]$_.TestAssemblyOutputRoot)) {
                throw "The test suite does not record its Release TFM output root: $($_.ProjectPath)"
            }
            Assert-CiPathContained `
                -ContainmentRoot $RepositoryRoot `
                -Path $_.TestAssemblyOutputRoot `
                -Description "Release test-assembly output root"
            Assert-CiPathContained `
                -ContainmentRoot $_.TestAssemblyOutputRoot `
                -Path $_.TestAssemblyPath `
                -Description "Release test assembly"
            if (-not (Test-Path -LiteralPath $_.TestAssemblyPath -PathType Leaf)) {
                throw "The Release test assembly does not exist; build before running tests: $($_.TestAssemblyPath)"
            }

            $assembly = Get-Item -LiteralPath $_.TestAssemblyPath -Force
            [pscustomobject]@{
                SuiteName = $_.SuiteName
                ProjectPath = $_.ProjectPath
                AssemblyPath = [IO.Path]::GetRelativePath($RepositoryRoot, $_.TestAssemblyPath)
                Length = $assembly.Length
                Sha256 = (Get-FileHash -LiteralPath $_.TestAssemblyPath -Algorithm SHA256).Hash
            }
        } | Sort-Object -Property ProjectPath)
}

function Get-CiTestAssemblyIdentityErrors {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object[]]$Before,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$After)

    $afterByProject = @{}
    foreach ($entry in $After) {
        $afterByProject[$entry.ProjectPath] = $entry
    }
    foreach ($entry in $Before) {
        if (-not $afterByProject.ContainsKey($entry.ProjectPath)) {
            "Release test assembly disappeared after execution: $($entry.ProjectPath)"
            continue
        }
        $afterEntry = $afterByProject[$entry.ProjectPath]
        if ($entry.Length -ne $afterEntry.Length -or
            -not [string]::Equals($entry.Sha256, $afterEntry.Sha256, [StringComparison]::OrdinalIgnoreCase)) {
            "Release test assembly changed during execution: $($entry.ProjectPath)"
        }
    }
    if ($Before.Count -ne $After.Count) {
        "The Release test-assembly inventory count changed during execution."
    }
}

function Read-CiXmlDocument {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$ContainmentRoot,

        [Parameter(Mandatory)]
        [string]$Description
    )

    Assert-CiPathContained -ContainmentRoot $ContainmentRoot -Path $Path -Description $Description
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description does not exist: $Path"
    }

    $file = Get-Item -LiteralPath $Path -Force
    if ($file.Length -le 0 -or $file.Length -gt 268435456) {
        throw "$Description has an invalid byte length ($($file.Length)): $Path"
    }

    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = 268435456
    $reader = [Xml.XmlReader]::Create($file.FullName, $settings)
    try {
        $document = [Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
        $document
    }
    finally {
        $reader.Dispose()
    }
}

function Get-DirectCoveragePaths {
    param(
        [Parameter(Mandatory)]
        [string]$TestResultsRoot,

        [Parameter(Mandatory)]
        [string]$ContainmentRoot
    )

    Assert-CiPathContained `
        -ContainmentRoot $ContainmentRoot `
        -Path $TestResultsRoot `
        -Description "TestResults root"
    if (-not (Test-Path -LiteralPath $TestResultsRoot -PathType Container)) {
        return
    }

    foreach ($coverageDirectory in Get-ChildItem -LiteralPath $TestResultsRoot -Force -Directory) {
        Assert-CiPathContained `
            -ContainmentRoot $ContainmentRoot `
            -Path $coverageDirectory.FullName `
            -Description "coverage collector directory"
        $coveragePath = Join-Path $coverageDirectory.FullName "coverage.cobertura.xml"
        Assert-CiPathContained `
            -ContainmentRoot $ContainmentRoot `
            -Path $coveragePath `
            -Description "coverage report"
        if (Test-Path -LiteralPath $coveragePath -PathType Leaf) {
            [IO.Path]::GetFullPath($coveragePath)
        }
    }
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$ContainmentRoot,

        [string]$Description = "evidence file"
    )

    Assert-CiPathContained -ContainmentRoot $ContainmentRoot -Path $Path -Description $Description
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-CiProjectFilesUnderTests {
    param(
        [Parameter(Mandatory)]
        [string]$TestsRoot,

        [Parameter(Mandatory)]
        [string]$RepositoryRoot
    )

    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push([IO.Path]::GetFullPath($TestsRoot))
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        Assert-CiPathContained `
            -ContainmentRoot $RepositoryRoot `
            -Path $directory `
            -Description "test-project inventory directory"

        foreach ($childDirectory in Get-ChildItem -LiteralPath $directory -Force -Directory) {
            # These are the repository's ignored build/evidence roots, not
            # source project locations. Do not rediscover generated *proj files.
            if ($childDirectory.Name -in @("bin", "obj", "TestResults")) {
                continue
            }

            Assert-CiPathContained `
                -ContainmentRoot $RepositoryRoot `
                -Path $childDirectory.FullName `
                -Description "test-project inventory directory"
            $pending.Push($childDirectory.FullName)
        }

        foreach ($file in Get-ChildItem -LiteralPath $directory -Force -File) {
            if (-not $file.Name.EndsWith("proj", [StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            Assert-CiPathContained `
                -ContainmentRoot $RepositoryRoot `
                -Path $file.FullName `
                -Description "test-project inventory file"
            [IO.Path]::GetFullPath($file.FullName)
        }
    }
}

function Get-EvaluatedProjectClassification {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath
    )

    $evaluationOutput = @(& dotnet msbuild $ProjectPath `
            -nologo `
            -verbosity:quiet `
            -tlp:off `
            -property:Configuration=Release `
            -getProperty:IsTestProject `
            -getProperty:Configuration `
            -getProperty:MSBuildProjectExtension `
            -getProperty:MSBuildProjectName `
            -getProperty:TargetFramework `
            -getProperty:TargetFrameworks `
            -getProperty:TargetDir `
            -getProperty:TargetPath 2>&1)
    $evaluationExitCode = $LASTEXITCODE
    $evaluationText = ($evaluationOutput | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
    if ($evaluationExitCode -ne 0) {
        throw "MSBuild could not evaluate the solution project '$ProjectPath' " +
            "(exit $evaluationExitCode): $evaluationText"
    }

    try {
        $evaluation = $evaluationText | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        throw "MSBuild returned unreadable project classification for '$ProjectPath': " +
            $_.Exception.Message
    }

    $properties = $evaluation.Properties
    if ($null -eq $properties) {
        throw "MSBuild omitted project classification properties for '$ProjectPath'."
    }

    [pscustomobject]@{
        IsTestProject = [string]::Equals(
            [string]$properties.IsTestProject,
            "true",
            [StringComparison]::OrdinalIgnoreCase)
        Configuration = [string]$properties.Configuration
        ProjectExtension = [string]$properties.MSBuildProjectExtension
        ProjectName = [string]$properties.MSBuildProjectName
        TargetFramework = [string]$properties.TargetFramework
        TargetFrameworks = [string]$properties.TargetFrameworks
        TargetDir = [string]$properties.TargetDir
        TargetPath = [string]$properties.TargetPath
    }
}

function Get-CiTestSuiteInventory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryRoot,

        [string]$SolutionPath,

        [string]$TrxFileName = "test-results.trx"
    )

    if ([string]::IsNullOrWhiteSpace($TrxFileName) -or
        -not [string]::Equals(
            [IO.Path]::GetFileName($TrxFileName),
            $TrxFileName,
            [StringComparison]::Ordinal) -or
        -not $TrxFileName.EndsWith(".trx", [StringComparison]::OrdinalIgnoreCase)) {
        throw "The TRX filename must be a leaf .trx name: $TrxFileName"
    }

    $resolvedRepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
    $testsRoot = [IO.Path]::GetFullPath((Join-Path $resolvedRepositoryRoot "tests"))
    Assert-CiPathContained `
        -ContainmentRoot $resolvedRepositoryRoot `
        -Path $testsRoot `
        -Description "repository tests root"
    if (-not (Test-Path -LiteralPath $testsRoot -PathType Container)) {
        throw "The repository tests root does not exist: $testsRoot"
    }

    if ([string]::IsNullOrWhiteSpace($SolutionPath)) {
        $SolutionPath = Join-Path $resolvedRepositoryRoot "OpenClassroomFoundry.slnx"
    }

    $resolvedSolutionPath = [IO.Path]::GetFullPath($SolutionPath)
    Assert-CiPathContained `
        -ContainmentRoot $resolvedRepositoryRoot `
        -Path $resolvedSolutionPath `
        -Description "solution inventory"
    if (-not (Test-Path -LiteralPath $resolvedSolutionPath -PathType Leaf)) {
        throw "The solution inventory does not exist: $resolvedSolutionPath"
    }

    $solution = Read-CiXmlDocument `
        -Path $resolvedSolutionPath `
        -ContainmentRoot $resolvedRepositoryRoot `
        -Description "solution inventory"
    $inventory = @()
    $solutionProjectPaths = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    $testAssemblyPaths = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    $testAssemblyOwnerByPath = @{}
    foreach ($projectNode in @($solution.SelectNodes("//*[local-name()='Project']"))) {
        $declaredPath = [string]$projectNode.Path
        if ([string]::IsNullOrWhiteSpace($declaredPath)) {
            throw "The solution contains a Project node without a path."
        }

        $normalizedPath = $declaredPath.Replace('\', '/')
        if (-not $normalizedPath.EndsWith(".csproj", [StringComparison]::OrdinalIgnoreCase)) {
            throw "The solution contains an unsupported project kind; only .csproj is inventoried: $declaredPath"
        }

        if ([IO.Path]::IsPathRooted($declaredPath)) {
            throw "The solution project path must be repository-relative: $declaredPath"
        }

        $projectPath = [IO.Path]::GetFullPath((Join-Path $resolvedRepositoryRoot $declaredPath))
        Assert-CiPathContained `
            -ContainmentRoot $resolvedRepositoryRoot `
            -Path $projectPath `
            -Description "solution project path"
        if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
            throw "The solution project does not exist: $declaredPath"
        }

        if (-not $solutionProjectPaths.Add($projectPath)) {
            throw "The solution repeats a project path: $declaredPath"
        }

        $relativeToTests = [IO.Path]::GetRelativePath($testsRoot, $projectPath)
        $isUnderTests = -not ([IO.Path]::IsPathRooted($relativeToTests) -or
            $relativeToTests -eq ".." -or
            $relativeToTests.StartsWith("..\", [StringComparison]::Ordinal) -or
            $relativeToTests.StartsWith("../", [StringComparison]::Ordinal))
        $declaresTestsPath = $normalizedPath.StartsWith(
            "tests/",
            [StringComparison]::OrdinalIgnoreCase)
        if ($declaresTestsPath -and -not $isUnderTests) {
            throw "The solution test-project path escapes the repository tests root: $declaredPath"
        }

        if ($isUnderTests -and -not $declaresTestsPath) {
            throw "The solution test-project path must use the canonical tests/ prefix: $declaredPath"
        }

        $classification = Get-EvaluatedProjectClassification -ProjectPath $projectPath
        if (-not [string]::Equals(
                $classification.ProjectExtension,
                ".csproj",
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "MSBuild classified an unsupported solution project kind: $declaredPath"
        }

        if (-not $isUnderTests) {
            if ($classification.IsTestProject) {
                throw "A solution test project is outside the repository tests root: $declaredPath"
            }

            continue
        }

        if (-not $classification.IsTestProject) {
            throw "A solution project beneath tests did not evaluate IsTestProject=true: $declaredPath"
        }

        $projectDirectory = [IO.Path]::GetDirectoryName($projectPath)
        if (-not [string]::Equals(
                $classification.Configuration,
                "Release",
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "A solution test project did not evaluate Configuration=Release: $declaredPath " +
                "(evaluated '$($classification.Configuration)')."
        }

        if (-not [string]::IsNullOrWhiteSpace($classification.TargetFrameworks)) {
            throw "A solution test project uses TargetFrameworks, whose Release output is ambiguous " +
                "without an inner-build evaluation: $declaredPath ($($classification.TargetFrameworks))"
        }

        if ([string]::IsNullOrWhiteSpace($classification.TargetFramework)) {
            throw "MSBuild omitted the single Release TargetFramework for solution test project: $declaredPath"
        }

        if ([string]::IsNullOrWhiteSpace($classification.TargetDir)) {
            throw "MSBuild omitted the Release TargetDir for solution test project: $declaredPath"
        }

        if ([string]::IsNullOrWhiteSpace($classification.TargetPath)) {
            throw "MSBuild omitted the Release TargetPath for solution test project: $declaredPath"
        }

        $releaseOutputRoot = [IO.Path]::GetFullPath((Join-Path $projectDirectory "bin\Release"))
        Assert-CiPathContained `
            -ContainmentRoot $resolvedRepositoryRoot `
            -Path $releaseOutputRoot `
            -Description "literal Release output root"
        $testAssemblyOutputRoot = [IO.Path]::GetFullPath(
            (Join-Path $releaseOutputRoot $classification.TargetFramework))
        Assert-CiPathContained `
            -ContainmentRoot $releaseOutputRoot `
            -Path $testAssemblyOutputRoot `
            -Description "single-TFM Release output root"
        $targetDirectory = if ([IO.Path]::IsPathRooted($classification.TargetDir)) {
            [IO.Path]::GetFullPath($classification.TargetDir)
        }
        else {
            [IO.Path]::GetFullPath((Join-Path $projectDirectory $classification.TargetDir))
        }
        Assert-CiPathContained `
            -ContainmentRoot $testAssemblyOutputRoot `
            -Path $targetDirectory `
            -Description "Release TargetDir"

        $testAssemblyPath = if ([IO.Path]::IsPathRooted($classification.TargetPath)) {
            [IO.Path]::GetFullPath($classification.TargetPath)
        }
        else {
            [IO.Path]::GetFullPath((Join-Path $projectDirectory $classification.TargetPath))
        }
        Assert-CiPathContained `
            -ContainmentRoot $releaseOutputRoot `
            -Path $testAssemblyPath `
            -Description "Release test assembly"
        Assert-CiPathContained `
            -ContainmentRoot $targetDirectory `
            -Path $testAssemblyPath `
            -Description "Release test assembly TargetDir binding"
        if (-not $testAssemblyPath.EndsWith(".dll", [StringComparison]::OrdinalIgnoreCase)) {
            throw "A solution test project's Release TargetPath is not a DLL: $declaredPath"
        }
        if (-not $testAssemblyPaths.Add($testAssemblyPath)) {
            throw "Solution test projects '$($testAssemblyOwnerByPath[$testAssemblyPath])' and " +
                "'$declaredPath' resolve to the same normalized Release TargetPath: $testAssemblyPath"
        }
        $testAssemblyOwnerByPath[$testAssemblyPath] = $declaredPath

        $suiteName = [IO.Path]::GetFileName($projectDirectory)
        if ([string]::IsNullOrWhiteSpace($suiteName)) {
            throw "The solution test project has no usable suite name: $declaredPath"
        }

        $testResultsRoot = Join-Path $projectDirectory "TestResults"
        Assert-CiPathContained `
            -ContainmentRoot $resolvedRepositoryRoot `
            -Path $testResultsRoot `
            -Description "TestResults root"
        $inventory += [pscustomobject]@{
            SuiteName = $suiteName
            ProjectPath = $normalizedPath
            ProjectFullPath = $projectPath
            ProjectDirectory = $projectDirectory
            RepositoryRoot = $resolvedRepositoryRoot
            TestResultsRoot = $testResultsRoot
            TrxPath = Join-Path $testResultsRoot $TrxFileName
            TrxFileName = $TrxFileName
            EvidenceStem = $suiteName
            Configuration = $classification.Configuration
            TargetFramework = $classification.TargetFramework
            TargetDirectory = $targetDirectory
            ReleaseOutputRoot = $releaseOutputRoot
            TestAssemblyOutputRoot = $testAssemblyOutputRoot
            TestAssemblyPath = $testAssemblyPath
        }
    }

    $discoveredTestProjects = @(Get-CiProjectFilesUnderTests `
            -TestsRoot $testsRoot `
            -RepositoryRoot $resolvedRepositoryRoot |
            Sort-Object -Unique)
    foreach ($discoveredProject in $discoveredTestProjects) {
        $relativeProject = [IO.Path]::GetRelativePath(
            $resolvedRepositoryRoot,
            $discoveredProject).Replace('\', '/')
        if (-not $discoveredProject.EndsWith(".csproj", [StringComparison]::OrdinalIgnoreCase)) {
            throw "The repository tests tree contains an unsupported project kind: $relativeProject"
        }

        if (-not $solutionProjectPaths.Contains($discoveredProject)) {
            throw "A repository test project is omitted from the solution inventory: $relativeProject"
        }
    }

    if ($inventory.Count -eq 0) {
        throw "The solution contains no test projects beneath the repository tests root."
    }

    if ($discoveredTestProjects.Count -ne $inventory.Count) {
        throw "The solution test-project inventory does not exactly match the repository tests tree " +
            "($($inventory.Count) solution test projects; $($discoveredTestProjects.Count) discovered project files)."
    }

    $duplicateProjects = @($inventory |
            Group-Object -Property ProjectFullPath |
            Where-Object Count -ne 1)
    if ($duplicateProjects.Count -gt 0) {
        throw "The solution repeats a test-project path: $($duplicateProjects[0].Name)"
    }

    $duplicateEvidenceStems = @($inventory |
            Group-Object -Property EvidenceStem |
            Where-Object Count -ne 1)
    if ($duplicateEvidenceStems.Count -gt 0) {
        throw "Test suites would collide in the evidence snapshot: $($duplicateEvidenceStems[0].Name)"
    }

    @($inventory | Sort-Object -Property ProjectPath)
}

function Get-CiTestEvidenceBaseline {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object[]]$Suites
    )

    foreach ($suite in $Suites) {
        Assert-CiPathContained `
            -ContainmentRoot $suite.RepositoryRoot `
            -Path $suite.TestResultsRoot `
            -Description "TestResults root"
        [pscustomobject]@{
            SuiteName = $suite.SuiteName
            ProjectPath = $suite.ProjectPath
            TrxSha256 = Get-Sha256 `
                -Path $suite.TrxPath `
                -ContainmentRoot $suite.RepositoryRoot `
                -Description "expected TRX"
            DirectCoveragePaths = @(Get-DirectCoveragePaths `
                    -TestResultsRoot $suite.TestResultsRoot `
                    -ContainmentRoot $suite.RepositoryRoot |
                    Sort-Object -Unique)
        }
    }
}

function Get-RequiredCounter {
    param(
        [Parameter(Mandatory)]
        [Xml.XmlElement]$Counters,

        [Parameter(Mandatory)]
        [string]$Name
    )

    $attribute = $Counters.Attributes[$Name]
    [long]$value = 0
    if ($null -eq $attribute -or
        -not [long]::TryParse(
            $attribute.Value,
            [Globalization.NumberStyles]::None,
            [Globalization.CultureInfo]::InvariantCulture,
            [ref]$value) -or
        $value -lt 0) {
        throw "TRX counter '$Name' is missing or is not a nonnegative integer."
    }

    $value
}

function Test-CiTrxFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$ContainmentRoot,

        [string]$ExpectedTestAssemblyPath
    )

    try {
        $document = Read-CiXmlDocument `
            -Path $Path `
            -ContainmentRoot $ContainmentRoot `
            -Description "TRX evidence"
        if ($document.DocumentElement.LocalName -ne "TestRun") {
            throw "TRX root element is not TestRun."
        }

        if (-not [string]::Equals(
                $document.DocumentElement.NamespaceURI,
                "http://microsoft.com/schemas/VisualStudio/TeamTest/2010",
                [StringComparison]::Ordinal)) {
            throw "TRX does not use the Visual Studio Team Test 2010 namespace."
        }

        $summaries = @($document.SelectNodes(
                "/*[local-name()='TestRun']/*[local-name()='ResultSummary']"))
        if ($summaries.Count -ne 1 -or
            -not [string]::Equals(
                [string]$summaries[0].Attributes["outcome"].Value,
                "Completed",
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "TRX must contain exactly one completed ResultSummary."
        }

        $counterNodes = @($summaries[0].SelectNodes("./*[local-name()='Counters']"))
        if ($counterNodes.Count -ne 1) {
            throw "TRX must contain exactly one Counters element."
        }

        $counters = [Xml.XmlElement]$counterNodes[0]
        $total = Get-RequiredCounter -Counters $counters -Name "total"
        $executed = Get-RequiredCounter -Counters $counters -Name "executed"
        $passed = Get-RequiredCounter -Counters $counters -Name "passed"
        if ($total -le 0 -or $executed -le 0 -or $passed -le 0) {
            throw "TRX total, executed, and passed counters must all be nonzero."
        }

        if ($executed -ne $total -or $passed -ne $total) {
            throw "TRX is not an all-passed complete run (total=$total, executed=$executed, passed=$passed)."
        }

        $rejectedCounters = @(
            "failed",
            "error",
            "timeout",
            "aborted",
            "inconclusive",
            "passedButRunAborted",
            "notRunnable",
            "notExecuted",
            "disconnected",
            "warning",
            "completed",
            "inProgress",
            "pending")
        foreach ($counterName in $rejectedCounters) {
            $counterValue = Get-RequiredCounter -Counters $counters -Name $counterName
            if ($counterValue -ne 0) {
                throw "TRX counter '$counterName' must be zero but was $counterValue."
            }
        }

        $results = @($document.SelectNodes(
                "/*[local-name()='TestRun']/*[local-name()='Results']/*[local-name()='UnitTestResult']"))
        if ($results.Count -ne $total) {
            throw "TRX result count $($results.Count) does not equal its total counter $total."
        }

        $resultContainers = @($document.SelectNodes(
                "/*[local-name()='TestRun']/*[local-name()='Results']"))
        if ($resultContainers.Count -ne 1) {
            throw "TRX must contain exactly one Results element."
        }

        $definitionContainers = @($document.SelectNodes(
                "/*[local-name()='TestRun']/*[local-name()='TestDefinitions']"))
        $definitions = @($document.SelectNodes(
                "/*[local-name()='TestRun']/*[local-name()='TestDefinitions']/*[local-name()='UnitTest']"))
        if ($definitionContainers.Count -ne 1 -or $definitions.Count -ne $total) {
            throw "TRX must contain exactly one TestDefinitions element and one UnitTest definition per result."
        }

        $definitionIds = [Collections.Generic.HashSet[string]]::new(
            [StringComparer]::OrdinalIgnoreCase)
        $expectedAssembly = if ([string]::IsNullOrWhiteSpace($ExpectedTestAssemblyPath)) {
            $null
        }
        else {
            [IO.Path]::GetFullPath($ExpectedTestAssemblyPath)
        }
        foreach ($definition in $definitions) {
            $definitionId = [string]$definition.Attributes["id"].Value
            if ([string]::IsNullOrWhiteSpace($definitionId) -or
                -not $definitionIds.Add($definitionId)) {
                throw "TRX UnitTest definitions require unique nonempty ids."
            }

            if ($null -ne $expectedAssembly) {
                $storage = [string]$definition.Attributes["storage"].Value
                if ([string]::IsNullOrWhiteSpace($storage) -or
                    -not [IO.Path]::IsPathRooted($storage) -or
                    -not [string]::Equals(
                        [IO.Path]::GetFullPath($storage),
                        $expectedAssembly,
                        [StringComparison]::OrdinalIgnoreCase)) {
                    throw "TRX UnitTest storage does not bind to the expected Release test assembly."
                }
            }
        }

        $resultIds = [Collections.Generic.HashSet[string]]::new(
            [StringComparer]::OrdinalIgnoreCase)
        foreach ($result in $results) {
            $resultId = [string]$result.Attributes["testId"].Value
            if ([string]::IsNullOrWhiteSpace($resultId) -or
                -not $resultIds.Add($resultId) -or
                -not $definitionIds.Contains($resultId)) {
                throw "TRX results require unique nonempty testIds matching their UnitTest definitions."
            }
        }

        $nonPassed = @($results | Where-Object {
                -not [string]::Equals(
                    [string]$_.Attributes["outcome"].Value,
                    "Passed",
                    [StringComparison]::OrdinalIgnoreCase)
            })
        if ($nonPassed.Count -ne 0) {
            throw "TRX contains $($nonPassed.Count) non-passed UnitTestResult elements."
        }

        [pscustomobject]@{
            Valid = $true
            Error = $null
            Total = $total
            Executed = $executed
            Passed = $passed
        }
    }
    catch {
        [pscustomobject]@{
            Valid = $false
            Error = $_.Exception.Message
            Total = $null
            Executed = $null
            Passed = $null
        }
    }
}

function Test-CiCoberturaFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$ContainmentRoot
    )

    try {
        $document = Read-CiXmlDocument `
            -Path $Path `
            -ContainmentRoot $ContainmentRoot `
            -Description "Cobertura evidence"
        $coverage = $document.DocumentElement
        if ($coverage.LocalName -ne "coverage") {
            throw "Cobertura root element is not coverage."
        }

        [long]$linesValid = 0
        [long]$linesCovered = 0
        [double]$lineRate = 0
        if (-not [long]::TryParse(
                [string]$coverage.Attributes["lines-valid"].Value,
                [Globalization.NumberStyles]::None,
                [Globalization.CultureInfo]::InvariantCulture,
                [ref]$linesValid) -or
            $linesValid -le 0) {
            throw "Cobertura lines-valid must be a positive integer."
        }

        if (-not [long]::TryParse(
                [string]$coverage.Attributes["lines-covered"].Value,
                [Globalization.NumberStyles]::None,
                [Globalization.CultureInfo]::InvariantCulture,
                [ref]$linesCovered) -or
            $linesCovered -lt 0 -or
            $linesCovered -gt $linesValid) {
            throw "Cobertura lines-covered must be between zero and lines-valid."
        }

        if (-not [double]::TryParse(
                [string]$coverage.Attributes["line-rate"].Value,
                [Globalization.NumberStyles]::Float,
                [Globalization.CultureInfo]::InvariantCulture,
                [ref]$lineRate) -or
            -not [double]::IsFinite($lineRate) -or
            $lineRate -lt 0 -or
            $lineRate -gt 1) {
            throw "Cobertura line-rate must be a finite value from zero through one."
        }

        $packages = @($coverage.SelectNodes("./*[local-name()='packages']/*[local-name()='package']"))
        $classes = @($coverage.SelectNodes(
                "./*[local-name()='packages']/*[local-name()='package']/*[local-name()='classes']/*[local-name()='class']"))
        $lines = @($coverage.SelectNodes(
                ".//*[local-name()='class']/*[local-name()='lines']/*[local-name()='line']"))
        if ($packages.Count -eq 0 -or $classes.Count -eq 0 -or $lines.Count -eq 0) {
            throw "Cobertura must contain at least one package, class, and line."
        }

        [long]$coveredLineElements = 0
        foreach ($line in $lines) {
            [long]$lineNumber = 0
            [long]$hits = 0
            if (-not [long]::TryParse(
                    [string]$line.Attributes["number"].Value,
                    [Globalization.NumberStyles]::None,
                    [Globalization.CultureInfo]::InvariantCulture,
                    [ref]$lineNumber) -or
                $lineNumber -le 0 -or
                -not [long]::TryParse(
                    [string]$line.Attributes["hits"].Value,
                    [Globalization.NumberStyles]::None,
                    [Globalization.CultureInfo]::InvariantCulture,
                    [ref]$hits) -or
                $hits -lt 0) {
                throw "Cobertura line entries require a positive number and nonnegative hits."
            }

            if ($hits -gt 0) {
                $coveredLineElements++
            }
        }

        if ($lines.Count -ne $linesValid -or $coveredLineElements -ne $linesCovered) {
            throw "Cobertura root line counters do not match its class line entries."
        }

        $calculatedLineRate = $linesCovered / [double]$linesValid
        if ([Math]::Abs($calculatedLineRate - $lineRate) -gt 0.0001) {
            throw "Cobertura line-rate does not match lines-covered divided by lines-valid."
        }

        [pscustomobject]@{
            Valid = $true
            Error = $null
            LinesValid = $linesValid
            LinesCovered = $linesCovered
            LineRate = $lineRate
        }
    }
    catch {
        [pscustomobject]@{
            Valid = $false
            Error = $_.Exception.Message
            LinesValid = $null
            LinesCovered = $null
            LineRate = $null
        }
    }
}

function Get-CiTestEvidenceDelta {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object[]]$Suites,

        [Parameter(Mandatory)]
        [object[]]$Baseline
    )

    $baselineByProject = @{}
    foreach ($entry in $Baseline) {
        if ($baselineByProject.ContainsKey($entry.ProjectPath)) {
            throw "The evidence baseline repeats a test project: $($entry.ProjectPath)"
        }

        $baselineByProject.Add($entry.ProjectPath, $entry)
    }

    foreach ($suite in $Suites) {
        if (-not $baselineByProject.ContainsKey($suite.ProjectPath)) {
            throw "The evidence baseline omits a test project: $($suite.ProjectPath)"
        }

        Assert-CiPathContained `
            -ContainmentRoot $suite.RepositoryRoot `
            -Path $suite.TestResultsRoot `
            -Description "TestResults root"
        $suiteBaseline = $baselineByProject[$suite.ProjectPath]
        $currentTrxSha256 = Get-Sha256 `
            -Path $suite.TrxPath `
            -ContainmentRoot $suite.RepositoryRoot `
            -Description "expected TRX"
        $trxIsCurrent = $null -ne $currentTrxSha256 -and
            -not [string]::Equals(
                [string]$currentTrxSha256,
                [string]$suiteBaseline.TrxSha256,
                [StringComparison]::OrdinalIgnoreCase)
        $trxValidation = if ($trxIsCurrent) {
            Test-CiTrxFile `
                -Path $suite.TrxPath `
                -ContainmentRoot $suite.RepositoryRoot `
                -ExpectedTestAssemblyPath $suite.TestAssemblyPath
        }
        else { $null }

        $baselineCoveragePaths = [Collections.Generic.HashSet[string]]::new(
            [StringComparer]::OrdinalIgnoreCase)
        foreach ($coveragePath in @($suiteBaseline.DirectCoveragePaths)) {
            [void]$baselineCoveragePaths.Add([IO.Path]::GetFullPath([string]$coveragePath))
        }

        $newCoveragePaths = @(Get-DirectCoveragePaths `
                -TestResultsRoot $suite.TestResultsRoot `
                -ContainmentRoot $suite.RepositoryRoot |
                Where-Object { -not $baselineCoveragePaths.Contains($_) } |
                Sort-Object -Unique)
        $newCoverageEvidence = @($newCoveragePaths | ForEach-Object {
                [pscustomobject]@{
                    Path = $_
                    Sha256 = Get-Sha256 `
                        -Path $_ `
                        -ContainmentRoot $suite.RepositoryRoot `
                        -Description "coverage report"
                    Validation = Test-CiCoberturaFile `
                        -Path $_ `
                        -ContainmentRoot $suite.RepositoryRoot
                }
            })
        [pscustomobject]@{
            SuiteName = $suite.SuiteName
            ProjectPath = $suite.ProjectPath
            EvidenceStem = $suite.EvidenceStem
            RepositoryRoot = $suite.RepositoryRoot
            TrxPath = if ($trxIsCurrent) { $suite.TrxPath } else { $null }
            TrxSha256 = if ($trxIsCurrent) { $currentTrxSha256 } else { $null }
            TrxIsCurrent = $trxIsCurrent
            TrxValidation = $trxValidation
            NewCoveragePaths = $newCoveragePaths
            NewCoverageEvidence = $newCoverageEvidence
        }
    }
}

function Get-CiTestEvidenceCompletenessErrors {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object[]]$EvidenceDelta
    )

    foreach ($suiteEvidence in $EvidenceDelta) {
        $trxCount = if ($suiteEvidence.TrxIsCurrent) { 1 } else { 0 }
        $coverageCount = @($suiteEvidence.NewCoverageEvidence).Count
        $problems = @()
        if ($trxCount -ne 1 -or $coverageCount -ne 1) {
            $problems += "expected one current TRX and one new direct coverage file; " +
                "found $trxCount TRX and $coverageCount coverage files"
        }

        if ($trxCount -eq 1 -and -not $suiteEvidence.TrxValidation.Valid) {
            $problems += "TRX validation failed: $($suiteEvidence.TrxValidation.Error)"
        }

        foreach ($coverageEvidence in @($suiteEvidence.NewCoverageEvidence)) {
            if (-not $coverageEvidence.Validation.Valid) {
                $problems += "Cobertura validation failed for '$($coverageEvidence.Path)': " +
                    $coverageEvidence.Validation.Error
            }
        }

        if ($problems.Count -gt 0) {
            "$($suiteEvidence.SuiteName): $($problems -join '; ')."
        }
    }
}

function Copy-CiEvidenceFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SourcePath,

        [Parameter(Mandatory)]
        [string]$SourceContainmentRoot,

        [Parameter(Mandatory)]
        [string]$DestinationPath,

        [Parameter(Mandatory)]
        [string]$DestinationContainmentRoot,

        [Parameter(Mandatory)]
        [string]$ExpectedSourceSha256
    )

    Assert-CiPathContained `
        -ContainmentRoot $SourceContainmentRoot `
        -Path $SourcePath `
        -Description "source evidence file"
    Assert-CiPathContained `
        -ContainmentRoot $DestinationContainmentRoot `
        -Path $DestinationPath `
        -Description "curated evidence file"
    $sourceHashBefore = Get-Sha256 `
        -Path $SourcePath `
        -ContainmentRoot $SourceContainmentRoot `
        -Description "source evidence file"
    if (-not [string]::Equals(
            [string]$sourceHashBefore,
            $ExpectedSourceSha256,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "The source evidence hash changed before copy: $SourcePath"
    }

    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath
    Assert-CiPathContained `
        -ContainmentRoot $DestinationContainmentRoot `
        -Path $DestinationPath `
        -Description "curated evidence file"
    $sourceHashAfter = Get-Sha256 `
        -Path $SourcePath `
        -ContainmentRoot $SourceContainmentRoot `
        -Description "source evidence file"
    $copiedHash = Get-Sha256 `
        -Path $DestinationPath `
        -ContainmentRoot $DestinationContainmentRoot `
        -Description "curated evidence file"
    $copyMatchesSource = [string]::Equals(
        [string]$sourceHashBefore,
        [string]$sourceHashAfter,
        [StringComparison]::OrdinalIgnoreCase) -and
        [string]::Equals(
            [string]$sourceHashAfter,
            [string]$copiedHash,
            [StringComparison]::OrdinalIgnoreCase)
    if (-not $copyMatchesSource) {
        throw "The curated evidence copy did not retain the accepted source bytes: $SourcePath"
    }

    [pscustomobject]@{
        SourcePath = [IO.Path]::GetFullPath($SourcePath)
        DestinationPath = [IO.Path]::GetFullPath($DestinationPath)
        SourceSha256 = $sourceHashAfter
        CopiedSha256 = $copiedHash
        CopyMatchesSource = $copyMatchesSource
    }
}

Export-ModuleMember -Function @(
    "Assert-CiPathContained",
    "Get-CiRepositoryState",
    "Get-CiTestAssemblySnapshot",
    "Get-CiTestAssemblyIdentityErrors",
    "Get-CiTestSuiteInventory",
    "Get-CiTestEvidenceBaseline",
    "Get-CiTestEvidenceDelta",
    "Get-CiTestEvidenceCompletenessErrors",
    "Test-CiTrxFile",
    "Test-CiCoberturaFile",
    "Copy-CiEvidenceFile"
)
