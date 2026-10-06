param([string]$ArtifactRoot=('local/Artifacts/issue6-cooking-slice1-'+[guid]::NewGuid().ToString('N').Substring(0,8)),[switch]$RestoreGraphOnly)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repoRoot 'tools/test-gate-result-contract.ps1')
. (Join-Path $PSScriptRoot 'fixtures/gate-result-model.ps1')
$executor=Join-Path $PSScriptRoot 'fixtures/gate-result-fake-dotnet.ps1'
$runner=Join-Path $repoRoot 'tools/run_test_gate.ps1'
$powershell=(Get-Command powershell -CommandType Application).Source
$artifactRootFull=if ([IO.Path]::IsPathRooted($ArtifactRoot)) { [IO.Path]::GetFullPath($ArtifactRoot) } else { [IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactRoot)) }
$ownedPrefix=[IO.Path]::GetFullPath((Join-Path $repoRoot 'local/Artifacts/issue6-cooking-slice1-'))
if (-not $artifactRootFull.StartsWith($ownedPrefix,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $artifactRootFull)) { throw 'Controls require a fresh owned issue6-cooking-slice1 output tree.' }
Assert-GateNoReparse $artifactRootFull
$null=New-Item -ItemType Directory -Path $artifactRootFull
$cases=New-Object 'System.Collections.Generic.List[object]'
$caseCounter=0
$widthDimension='RawUI.WindowSize.Width'
$requiredWidths=@(40,80,120)
$sourceInputs=@($PSCommandPath,$runner,(Join-Path $repoRoot 'tools/test-gate-result-contract.ps1'),$executor,(Join-Path $PSScriptRoot 'fixtures/gate-result-model.ps1'),(Join-Path $PSScriptRoot 'fixtures/gate-result-native-probe.ps1'),(Join-Path $repoRoot 'tools/test-gates.json'))
$sourceBefore=Get-GateSource $repoRoot $sourceInputs @('local/ ignored control evidence','TEMP unique synthetic fixtures','coordinator task records are outside these control inputs')

function New-ControlDirectory {
    $script:caseCounter++
    $directory=Join-Path $artifactRootFull ('c'+$script:caseCounter.ToString('000'))
    $null=New-Item -ItemType Directory -Path $directory
    return $directory
}

function Test-ControlWindowWidth {
    param($Evidence,[int]$Requested)
    return [bool]($Evidence -and $Evidence.dimension -ceq $widthDimension -and $Evidence.mode -ceq 'Window' -and $Evidence.requestedWidth -eq $Requested -and $Evidence.widthCoverageStatus -ceq 'Passed' -and $Evidence.setterSucceeded -eq $true -and $Evidence.applied -eq $true -and $Evidence.appliedWidth -eq $Requested -and $Evidence.observedWidth -eq $Requested -and $Evidence.observedWindowWidth -eq $Requested -and $Evidence.finalState.windowWidth -eq $Requested -and $Evidence.observedBufferWidth -eq $Evidence.finalState.bufferWidth -and $Evidence.initialState.windowHeight -gt 0 -and $Evidence.finalState.windowHeight -eq $Evidence.initialState.windowHeight -and $Evidence.outputRedirected -eq $true -and $Evidence.errorRedirected -eq $true)
}

function Get-ControlWidthStatus {
    param([object[]]$Records)
    if ($Records.Count -ne $requiredWidths.Count) { return 'NotRun' }
    foreach ($requested in $requiredWidths) {
        $matching=@($Records | Where-Object { $_.evidence.width.requestedWidth -eq $requested })
        if ($matching.Count -ne 1) { return 'Blocked' }
        $record=$matching[0]
        if ($record.status -cne 'Passed' -or $record.nativeExit -ne 7 -or $record.evidence.exitConfirmed -ne $true -or $record.evidence.streamsExact -ne $true -or -not (Test-ControlWindowWidth $record.evidence.width $requested)) { return 'Blocked' }
    }
    return 'Passed'
}

function Save-ControlReceipt {
    param([bool]$Final=$false)
    $failed=@($cases | Where-Object { $_.status -ceq 'Failed' }).Count
    $widthRecords=@($cases | Where-Object { $_.group -ceq 'native-streams' })
    $widthEvidence=@($widthRecords | ForEach-Object { $_.evidence.width })
    $widthStatus=Get-ControlWidthStatus $widthRecords
    $receipt=[pscustomobject]@{schemaVersion=1; example=$true; acceptance=$(if ($RestoreGraphOnly) { 'IsolatedAffectedRestoreGraphControlsOnly' } else { 'IsolatedContractControlsOnly' }); sourceBefore=$sourceBefore; sourceAfter=$(if ($Final) { Get-GateSource $repoRoot $sourceInputs $sourceBefore.evidenceExclusions } else { $null }); tool=[pscustomobject]@{powershell=$PSVersionTable.PSVersion.ToString(); host=[Environment]::Version.ToString(); os=[Environment]::OSVersion.VersionString}; command=@('powershell','-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath,'-ArtifactRoot',$ArtifactRoot); status=$(if ($Final -and $failed -eq 0) { 'Passed' } else { 'Failed' }); total=$cases.Count; executed=$cases.Count; passed=$cases.Count-$failed; failed=$failed; fullControlSuiteAccepted=($Final -and $failed -eq 0 -and -not $RestoreGraphOnly); cases=@($cases.ToArray()); realDotNet='NotRun'; unity='NotRun'; issueAcceptance='NotRun'}
    $receipt | Add-Member -NotePropertyName consoleWidthCoverage -NotePropertyValue ([pscustomobject]@{status=$widthStatus; dimension=$widthDimension; requested=$requiredWidths; observations=$widthEvidence})
    $receipt | Add-Member -NotePropertyName contractControlsAccepted -NotePropertyValue ($Final -and $failed -eq 0)
    if ($Final -and $failed -eq 0 -and -not $RestoreGraphOnly -and $widthStatus -cne 'Passed') { $receipt.status=$widthStatus; $receipt.fullControlSuiteAccepted=$false }
    [IO.File]::WriteAllText((Join-Path $artifactRootFull 'controls.json'),(ConvertTo-GateJson $receipt),[Text.UTF8Encoding]::new($false))
}

function Record-Control {
    param([string]$Group,[string]$Name,$Expected,$Actual,[string]$Directory,$Command,[Nullable[int]]$NativeExit,$Evidence)
    $passed=(ConvertTo-GateJson $Expected) -ceq (ConvertTo-GateJson $Actual)
    $record=[pscustomobject]@{id=[guid]::NewGuid().ToString(); group=$Group; name=$Name; expected=$Expected; actual=$Actual; status=$(if ($passed) { 'Passed' } else { 'Failed' }); command=$Command; nativeExit=$NativeExit; rawDirectory=$Directory; evidence=$Evidence}
    $cases.Add($record)
    [IO.File]::WriteAllText((Join-Path $Directory 'control.json'),(ConvertTo-GateJson $record))
    Save-ControlReceipt
    Write-Host ('{0}: {1} (expected {2}, actual {3})' -f $record.status,$Name,(ConvertTo-GateJson $Expected),(ConvertTo-GateJson $Actual))
}

function Invoke-ControlRunner {
    param($Fixture,[string]$Directory,[string[]]$Extra=@(),[bool]$Production=$false)
    $args=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$runner,'-Gate','control','-ConfigPath',$Fixture.configPath,'-ResultsDirectory',$Directory)
    if (-not $Production) { $args+=@('-ControlContextPath',$Fixture.contextPath) }
    $args+= $Extra
    $existing=@(Get-ChildItem -LiteralPath $Directory -Filter gate-summary.json -Recurse | ForEach-Object { $_.FullName })
    $stem='parent'+$existing.Count
    $native=Invoke-GateNative $powershell $args $Directory $stem $repoRoot
    $summaryFiles=@(Get-ChildItem -LiteralPath $Directory -Filter gate-summary.json -Recurse | Where-Object { $_.FullName -cnotin $existing })
    if ($summaryFiles.Count -ne 1) { throw 'Runner did not produce exactly one result.' }
    $summaryPath=$summaryFiles[0].FullName
    $summary=Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $launches=@(); if ([IO.File]::Exists($Fixture.context.launchesPath)) { $launches=@(Get-Content -LiteralPath $Fixture.context.launchesPath | ForEach-Object { $_ | ConvertFrom-Json }) }
    return [pscustomobject]@{summary=$summary; path=$summaryPath; runRoot=(Split-Path $summaryPath); native=$native; launches=$launches; fixtureRoot=$Fixture.root}
}

function Runner-Case {
    param([string]$Group,[string]$Name,[string]$Mode,[int]$Exit,[string]$Status,[string]$Shape='pair',[string[]]$Extra=@(),[scriptblock]$Configure,[scriptblock]$Verify)
    $directory=New-ControlDirectory
    try {
        $fixture=New-GateControlFixture $executor $Mode $Shape
        if ($Configure) { & $Configure $fixture; Save-GateControlFixture $fixture }
        $run=Invoke-ControlRunner $fixture $directory $Extra
        $actual=[pscustomobject]@{exit=$run.native.processExitCode; status=$run.summary.status; extra=$(if ($Verify) { [bool](& $Verify $run) } else { $true })}
        $expected=[pscustomobject]@{exit=$Exit; status=$Status; extra=$true}
        Record-Control $Group $Name $expected $actual $directory $run.native.command $run.native.processExitCode ([pscustomobject]@{summary=$run.path; launches=$run.launches.Count; fixture=$fixture.root; children=@($run.summary.children | Select-Object name,status,processExitCode); counts=@($run.summary.children | Where-Object tests | ForEach-Object { $_.tests.counts })})
        return $run
    } catch {
        Record-Control $Group $Name 'CompletedControl' $_.Exception.Message $directory @('Runner-Case',$Name) $null $null
        return $null
    }
}

function Set-ControlPreprocessedImports {
    param($Fixture)
    $directory=Join-Path $Fixture.root ('import space '+[char]0x6761+[char]0x4ef6)
    $null=New-Item -ItemType Directory -Path $directory
    $imports=@((Join-Path $directory 'conditional file.nonstandard'),(Join-Path $directory 'sdk file.props'))
    foreach ($path in $imports) { [IO.File]::WriteAllText($path,'<Project />') }
    foreach ($project in $Fixture.context.projectData) { $project | Add-Member -NotePropertyName preprocessedOnlyImports -NotePropertyValue $imports }
}

function Test-ControlPreprocessedImportRetention {
    param($Run)
    $fixture=Get-Content (Join-Path $Run.fixtureRoot 'context.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($leaf in $Run.summary.children) {
        foreach ($path in $fixture.projectData[0].preprocessedOnlyImports) {
            $source=@($leaf.source.before.inputs | Where-Object path -ieq $path)
            $archive=@($leaf.provenance.build.inputs | Where-Object path -ieq $path)
            if ($source.Count -ne 1 -or $archive.Count -ne 1 -or $source[0].sha256 -cne (Get-GateHash $path) -or $archive[0].sha256 -cne $source[0].sha256) { return $false }
        }
    }
    return $true
}


# Cooking mixed graph and effective settings regressions use actual runner receipts.
$mixed=Runner-Case 'restore-graph' 'mixed-own-TFMs-and-hash-only-config-positive' 'valid' 0 'Passed' 'mixed' -Verify {
    param($r)
    foreach ($leaf in $r.summary.children) {
        if (@($leaf.provenance.restore.command.arguments[-1] | ConvertFrom-Json | Where-Object { $_ -like '-p:TargetFramework=*' }).Count) { return $false }
        foreach ($stage in @($leaf.provenance.restore,$leaf.provenance.build)) {
            $proof=@($stage.inputs | Where-Object proofKind -ceq 'EffectiveNuGetConfigHashOnly')
            if ($proof.Count -ne 1 -or $proof[0].artifact -ne $null -or $proof[0].sha256 -cne (Get-GateHash $proof[0].path)) { return $false }
        }
        foreach ($project in $leaf.provenance.restore.graph) {
            $entry=@($leaf.provenance.restore.outputs | Where-Object path -ieq $project.assetsPath)[0]
            $assets=Get-Content -LiteralPath (Assert-GateArtifact $entry.artifact $leaf $r.runRoot) -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($project.tfm -cnotin @($assets.project.frameworks.PSObject.Properties.Name)) { return $false }
        }
    }
    return $r.summary.children[0].configuration.tfm -ceq 'netstandard2.0'
}
$null=Runner-Case 'restore-graph' 'relative-sdk-task-registration-positive' 'valid' 0 'Passed' 'build' -Verify {
    param($r)
    $leaf=$r.summary.children[0];$project=$leaf.provenance.closure[0]
    $raw=Assert-GateArtifact $project.settingsArtifact $leaf $r.runRoot
    $properties=(Get-Content -LiteralPath $raw -Raw -Encoding UTF8 | ConvertFrom-Json).Properties
    $task=[IO.Path]::GetFullPath((Join-Path (Split-Path $properties.NuGetRestoreTargets) $properties.RestoreTaskAssemblyFile))
    return -not [IO.Path]::IsPathRooted($properties.RestoreTaskAssemblyFile) -and $task -iin $project.restoreToolInputs -and $properties.NuGetRestoreTargets -iin $project.restoreToolInputs -and @($leaf.source.before.inputs | Where-Object { $_.path -ieq $task -and $_.sha256 -ceq (Get-GateHash $task) }).Count -eq 1
}
foreach ($mode in @('settings-task-missing','settings-target-missing')) {
    $null=Runner-Case 'restore-graph' $mode $mode 1 'Failed' 'build' -Verify { param($r) @($r.launches | Where-Object { $_.stage -cin @('restore','build','test') }).Count -eq 0 -and $r.summary.children[0].provenance.invocations.Count -gt 0 }
}
foreach ($mode in @('restore-wrong-reference-tfm','restore-missing-reference','config-change-restore','config-change-build','source-change-restore','sdk-change-restore','sdk-nuget-change-restore')) {
    $null=Runner-Case 'restore-graph' $mode $mode 1 'Failed' 'test' -Verify { param($r) @($r.launches | Where-Object stage -ceq 'test').Count -eq 0 }
}
$null=Runner-Case 'restore-graph' 'recorded-generated-transition-is-not-authored-source-change' 'valid' 0 'Passed' 'build' -Configure {
    param($f)
    $p=$f.data[0];$null=New-Item -ItemType Directory -Path $p.extensionsPath -Force
    foreach ($ext in @('.nuget.g.props','.nuget.g.targets')) { [IO.File]::WriteAllText((Join-Path $p.extensionsPath ((Split-Path $p.project -Leaf)+$ext)),'<Project><!-- prior SDK restore output --></Project>') }
} -Verify { param($r) $stage=$r.summary.children[0].provenance.restore; $stage.priorOutputs.Count -eq 2 -and $stage.outputs.Count -eq 3 }
# JSON roundtrip mutations retain the trusted assigned plan and actual raw archives.
foreach ($mutation in @('omitted-config','forged-config-hash','forged-config-size','extra-proof-field','wrong-proof-kind','hash-only-authored-source','config-archive','missing-graph','forged-reference-TFM','missing-source-config')) {
    $directory=New-ControlDirectory
    $candidate=ConvertTo-GateJson $mixed.summary | ConvertFrom-Json
    $leaf=$candidate.children[1];$stage=$leaf.provenance.restore
    $proof=@($stage.inputs | Where-Object proofKind -ceq 'EffectiveNuGetConfigHashOnly')[0]
    switch ($mutation) {
        'omitted-config' { $stage.inputs=@($stage.inputs | Where-Object { $_.path -ine $proof.path }); $stage.inputFingerprint=Get-GateFingerprint $stage.inputs }
        'forged-config-hash' { $proof.sha256='0'*64; $stage.inputFingerprint=Get-GateFingerprint $stage.inputs }
        'forged-config-size' { $proof.bytes++; $stage.inputFingerprint=Get-GateFingerprint $stage.inputs }
        'extra-proof-field' { $proof | Add-Member -NotePropertyName bypass -NotePropertyValue $true }
        'wrong-proof-kind' { $proof.proofKind='SourceHashOnly' }
        'hash-only-authored-source' { $proof.path=$leaf.provenance.closure[0].project; $proof.bytes=(Get-Item $proof.path).Length; $proof.sha256=Get-GateHash $proof.path; $stage.inputFingerprint=Get-GateFingerprint $stage.inputs }
        'config-archive' { $proof.artifact=$stage.outputs[0].artifact }
        'missing-graph' { $stage.graph=@() }
        'forged-reference-TFM' { $stage.graph[1].frameworks=@('net9.0') }
        'missing-source-config' { $leaf.source.before.inputs=@($leaf.source.before.inputs | Where-Object { $_.path -ine $proof.path }); $leaf.source.before.inputFingerprint=Get-GateFingerprint $leaf.source.before.inputs; $leaf.provenance.source=$leaf.source.before }
    }
    [IO.File]::WriteAllText((Join-Path $directory 'candidate.json'),(ConvertTo-GateJson $candidate))
    $candidate=Get-Content -LiteralPath (Join-Path $directory 'candidate.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $ctx=[pscustomobject]@{runRoot=$mixed.runRoot;allowExample=$true;control=$null;abortStatus=$null}
    # Control identity remains the exact fixture that generated this accepted baseline.
    $ctx.control=Get-Content -LiteralPath (Join-Path $mixed.fixtureRoot 'context.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $accepted=$false;$reason=$null
    try { $null=Assert-GateResult $candidate $mixed.summary $ctx; $accepted=$true } catch { $reason=$_.Exception.Message }
    Record-Control 'restore-graph-json' ('reject-'+$mutation) $false $accepted $directory @('Assert-GateResult','candidate.json','trusted-assigned-plan') $null $reason
}
# Reused effective configuration is never copied and cannot change after the seed.
$configReuseFixture=New-GateControlFixture $executor 'valid' 'test'
$configReuseDirectory=New-ControlDirectory;$configReuse=Invoke-ControlRunner $configReuseFixture $configReuseDirectory
Record-Control 'restore-graph-reuse' 'config-hash-only-reuse-seed' 0 $configReuse.native.processExitCode $configReuseDirectory $configReuse.native.command $configReuse.native.processExitCode $configReuse.path
foreach ($flag in @('-NoBuild','-NoRestore')) {
    $directory=New-ControlDirectory;$r=Invoke-ControlRunner $configReuseFixture $directory @($flag,'-ReuseManifestPath',(Join-Path $configReuse.runRoot 'reuse-manifest.json'))
    $proof=@($r.summary.children[0].provenance.restore.inputs | Where-Object proofKind -ceq 'EffectiveNuGetConfigHashOnly')
    Record-Control 'restore-graph-reuse' ($flag+'-valid-effective-config-hash-only') $true ($r.native.processExitCode -eq 0 -and $proof.Count -eq 1 -and $proof[0].artifact -eq $null) $directory $r.native.command $r.native.processExitCode $r.path
}

$directory=New-ControlDirectory;$copyRoot=Join-Path $directory 'forged-prior'
Copy-Item -LiteralPath $configReuse.runRoot -Destination $copyRoot -Recurse
$copyIndexPath=Join-Path $copyRoot 'reuse-manifest.json'
$index=Get-Content -LiteralPath $copyIndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
$entry=$index.receipts[0];$receiptPath=Join-Path $copyRoot $entry.path
$prior=Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($invocation in $prior.invocations) {
    $invocation.stdoutPath=$invocation.stdoutPath.Replace($configReuse.runRoot,$copyRoot)
    $invocation.stderrPath=$invocation.stderrPath.Replace($configReuse.runRoot,$copyRoot)
}
$prior.restore.inputs=@($prior.restore.inputs | Where-Object { $_.proofKind -cne 'EffectiveNuGetConfigHashOnly' })
$prior.restore.inputFingerprint=Get-GateFingerprint $prior.restore.inputs
[IO.File]::WriteAllText($receiptPath,(ConvertTo-GateJson $prior))
$entry.bytes=(Get-Item $receiptPath).Length;$entry.sha256=Get-GateHash $receiptPath
[IO.File]::WriteAllText($copyIndexPath,(ConvertTo-GateJson $index))
$r=Invoke-ControlRunner $configReuseFixture $directory @('-NoRestore','-ReuseManifestPath',$copyIndexPath)
Record-Control 'restore-graph-reuse' 'omitted-effective-config-in-rehashed-reuse-receipt-refused' $true ($r.native.processExitCode -eq 1 -and $r.summary.children[0].reason -ceq 'Effective config omitted from restore inputs.') $directory $r.native.command $r.native.processExitCode $r.path
$null=Runner-Case 'restore-graph' 'authored-generated-name-lookalike-is-not-exempt' 'source-change-restore' 1 'Failed' 'build' -Configure {
    param($f)
    $path=Join-Path (Split-Path $f.data[0].project) 'Authored.nuget.g.targets'
    [IO.File]::WriteAllText($path,'authored linked input with a generated-looking filename')
    $f.data[0].compile[0]=$path
} -Verify { param($r) $r.summary.children[0].reason -like 'Source/assets/binary changed:*Authored.nuget.g.targets' }
[IO.File]::AppendAllText($configReuseFixture.data[0].configPaths[0],'changed config after seed')
foreach ($flag in @('-NoBuild','-NoRestore')) {
    $directory=New-ControlDirectory;$r=Invoke-ControlRunner $configReuseFixture $directory @($flag,'-ReuseManifestPath',(Join-Path $configReuse.runRoot 'reuse-manifest.json'))
    Record-Control 'restore-graph-reuse' ($flag+'-changed-effective-config-refused') 1 $r.native.processExitCode $directory $r.native.command $r.native.processExitCode $r.path
}
if ($RestoreGraphOnly) {
    $after=Get-GateSource $repoRoot $sourceInputs $sourceBefore.evidenceExclusions
    $directory=New-ControlDirectory
    Record-Control 'freeze' 'affected-inputs-before-after-identical' $true ($sourceBefore.sha -ceq $after.sha -and $sourceBefore.inputFingerprint -ceq $after.inputFingerprint) $directory @('Get-GateSource','sourceInputs') $null ([pscustomobject]@{before=$sourceBefore;after=$after})
    Save-ControlReceipt $true
    $failed=@($cases | Where-Object status -ceq 'Failed').Count
    Write-Output ('Affected restore graph controls: {0} executed, {1} failed. Receipt: {2}' -f $cases.Count,$failed,(Join-Path $artifactRootFull 'controls.json'))
    exit $(if ($failed) { 1 } else { 0 })
}

$null=Runner-Case 'imports' 'mixed-sdk-comments-retain-spaces-unicode-nonstandard-imports' 'valid' 0 'Passed' -Configure { param($f) Set-ControlPreprocessedImports $f } -Verify { param($r) Test-ControlPreprocessedImportRetention $r }
$null=Runner-Case 'imports' 'changed-preprocessed-only-import-refused' 'import-change' 1 'Failed' 'build' -Configure { param($f) Set-ControlPreprocessedImports $f } -Verify { param($r) $r.summary.children[0].reason -like 'Source/assets/binary changed:*conditional file.nonstandard' }

$positive=Runner-Case 'valid' 'current-build-test-JSON-roundtrip' 'valid' 0 'Passed' -Verify { param($r) $r.summary.fullGateAccepted -and $r.summary.children[0].tests -eq $null -and $r.summary.children[1].tests.counts.executed -eq 2 -and @($r.summary.children[1].provenance.compilerInputs | Where-Object { $_.path -like '*Generated.opaque' }).Count -eq 1 }
# Pipeline logging is separate from returned runs.
if ($positive -is [array]) { $positive=$positive[-1] }
$null=Runner-Case 'valid' 'current-build-only-tests-null' 'valid' 0 'Passed' 'build'
$null=Runner-Case 'original-red' 'warning-native-zero-refused-for-no-build-evidence' 'build-warning-zero' 1 'Failed' 'build'
foreach ($mode in @('test-missing','test-empty','test-malformed','test-zero','test-no-entries','test-failed-zero','test-counter','test-completed-counter','test-lowercase','test-wrong-assembly','test-stale','test-duplicate','test-no-definition','test-skipped','test-replaced-dll','test-native-fail')) {
    $null=Runner-Case 'trx' $mode $mode 1 'Failed' 'test'
}
$null=Runner-Case 'environment' 'missing-tool-no-launch' 'missing-tool' 2 'Blocked' -Verify { param($r) $r.launches.Count -eq 0 -and $r.summary.children[0].processExitCode -eq $null -and $r.summary.children[1].status -ceq 'NotRun' }
$null=Runner-Case 'failure' 'required-native7-failure-later-NotRun' 'build-fail' 1 'Failed' -Verify { param($r) $r.summary.children[0].processExitCode -eq 7 -and $r.summary.children[1].status -ceq 'NotRun' }
$null=Runner-Case 'source' 'source-change-fails' 'source-change' 1 'Failed' 'build'
$null=Runner-Case 'source' 'evaluated-TFM-mismatch' 'evaluated-tfm' 1 'Failed' 'build'
$null=Runner-Case 'timeout-cancel' 'timeout-owned-child-exit-confirmed' 'timeout' 1 'Failed' -Extra @('-TimeoutSeconds','1') -Verify { param($r) $leaf=$r.summary.children[0]; $leaf.reason -ceq 'Timeout' -and $leaf.provenance.invocations[-1].exitConfirmed -and $r.summary.children[1].status -ceq 'NotRun' }
$cancelFixture=New-GateControlFixture $executor 'cancel'
$cancelDirectory=New-ControlDirectory
$cancelRun=Invoke-ControlRunner $cancelFixture $cancelDirectory @('-CancelSignalPath',$cancelFixture.context.signalPath)
Record-Control 'timeout-cancel' 'started-cancellation-owned-exit-confirmed' $true ($cancelRun.native.processExitCode -eq 1 -and $cancelRun.summary.children[0].reason -ceq 'StartedCancellation' -and $cancelRun.summary.children[0].provenance.invocations[-1].exitConfirmed -and $cancelRun.summary.children[1].status -ceq 'NotRun') $cancelDirectory $cancelRun.native.command $cancelRun.native.processExitCode $cancelRun.path
$null=Runner-Case 'coverage' 'nested-complete' 'valid' 0 'Passed' 'nested'
$null=Runner-Case 'coverage' 'repeated-nested-unique-node-directory-bindings' 'valid' 0 'Passed' 'repeated' -Verify { param($r) $ids=@($r.summary.children | ForEach-Object { $_.children[0].resultId }); @($ids | Sort-Object -Unique).Count -eq 2 -and @($r.summary.coverage.bindings.'build-library').Count -eq 2 }
$null=Runner-Case 'coverage' 'StepName-positive-parent-partial-NotRun4' 'valid' 4 'NotRun' -Extra @('-StepName','Tests') -Verify { param($r) -not $r.summary.fullGateAccepted -and $r.summary.children[0].command -eq $null -and $r.summary.children[0].times.executionStartedAt -eq $null -and $r.summary.children[1].status -ceq 'Passed' }
$null=Runner-Case 'coverage' 'nested-focus-partial' 'valid' 4 'NotRun' 'nested' -Extra @('-StepName','Tests')
$null=Runner-Case 'selector' 'missing-selector-zero-launches' 'valid' 1 'Failed' -Extra @('-StepName','absent') -Verify { param($r) $r.launches.Count -eq 0 -and $r.summary.children.Count -eq 2 }
$null=Runner-Case 'selector' 'ambiguous-selector-zero-launches' 'valid' 1 'Failed' 'repeated' -Extra @('-StepName','Build') -Verify { param($r) $r.launches.Count -eq 0 }
$null=Runner-Case 'coverage' 'optional-declared-missing-tool-skip' 'optional-missing-tool' 0 'Passed' 'optional' -Verify { param($r) $r.summary.children[1].status -ceq 'Skipped' -and 'test-project' -cin $r.summary.coverage.missing }
$null=Runner-Case 'coverage' 'optional-real-failure-not-laundered' 'test-failed-zero' 1 'Failed' 'optional'
foreach ($kind in @('powershell-script','unity-editmode-test','unity-playmode-test','unity-execute-method')) {
    $null=Runner-Case 'unsupported' ('producer-'+$kind+'-blocked-before-launch') 'valid' 2 'Blocked' -Configure { param($f) $f.config.gates[0].steps[1].kind=$kind } -Verify { param($r) $r.launches.Count -eq 0 -and $r.summary.children.Count -eq 2 }
}
$null=Runner-Case 'unsupported' 'missing-explicit-coverage-before-launch' 'valid' 2 'Blocked' -Configure { param($f) $f.config.gates[0].requiredCoverage=@() } -Verify { param($r) $r.launches.Count -eq 0 }
$null=Runner-Case 'config' 'duplicate-gate-fails-before-launch' 'valid' 1 'Failed' -Configure { param($f) $f.config.gates+= $f.config.gates[0] } -Verify { param($r) $r.launches.Count -eq 0 }
$null=Runner-Case 'config' 'unknown-kind-fails-before-launch' 'valid' 1 'Failed' -Configure { param($f) $f.config.gates[0].steps[1].kind='unknown' } -Verify { param($r) $r.launches.Count -eq 0 }
$null=Runner-Case 'config' 'empty-tree-fails-before-launch' 'valid' 1 'Failed' -Configure { param($f) $f.config.gates[0].steps=@() } -Verify { param($r) $r.launches.Count -eq 0 }
$null=Runner-Case 'config' 'throw-before-execution-complete-NotRun-tree' 'valid' 1 'Failed' -Extra @('-StepName','missing') -Verify { param($r) @($r.summary.children | Where-Object { $_.status -ceq 'NotRun' }).Count -eq 2 }
$null=Runner-Case 'config' 'unrelated-unadapted-entry-does-not-stop-selected-tree' 'valid' 0 'Passed' 'build' -Configure { param($f) $f.config.gates+= [pscustomobject]@{name='legacy-unselected'; steps=@([pscustomobject]@{name='Legacy'; kind='powershell-script'; script='never-launch.ps1'})} }

# Same-second isolation uses a preflight-blocked producer so no workloads can run.
$sameFixture=New-GateControlFixture $executor
$sameFixture.config.gates[0].steps[1].kind='powershell-script'; Save-GateControlFixture $sameFixture
$sameDirectory=New-ControlDirectory
# Align at the beginning of a second, then invoke twice in the SAME directory.
$second=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
while ([DateTimeOffset]::UtcNow.ToUnixTimeSeconds() -eq $second) { Start-Sleep -Milliseconds 5 }
$one=Invoke-ControlRunner $sameFixture $sameDirectory; $two=Invoke-ControlRunner $sameFixture $sameDirectory
Record-Control 'identity' 'same-second-same-directory-UUID-isolation' $true ($one.summary.runId -cne $two.summary.runId -and $one.summary.resultId -cne $two.summary.resultId -and $one.native.startedAt.Substring(0,19) -ceq $two.native.startedAt.Substring(0,19)) $sameDirectory @($one.native.command,$two.native.command) 2 @($one.path,$two.path)

# Reuse controls use the exact manifest from an explicit seed run. Nothing guesses
# a latest directory, and all corrupt copies/failed attempts remain archived.
$reuseFixture=New-GateControlFixture $executor 'valid' 'test'
$seedDirectory=New-ControlDirectory; $seed=Invoke-ControlRunner $reuseFixture $seedDirectory
Record-Control 'reuse' 'seed-current-original-receipts' 0 $seed.native.processExitCode $seedDirectory $seed.native.command $seed.native.processExitCode $seed.path
$manifestPath=Join-Path $seed.runRoot 'reuse-manifest.json'
foreach ($flag in @('-NoBuild','-NoRestore')) {
    $directory=New-ControlDirectory; $r=Invoke-ControlRunner $reuseFixture $directory @($flag,'-ReuseManifestPath',$manifestPath)
    $leaf=$r.summary.children[0]
    $ok=$r.native.processExitCode -eq 0 -and $leaf.provenance.restore.mode -ceq 'Reused' -and $leaf.provenance.restore.command -eq $null
    if ($flag -ceq '-NoBuild') { $ok=$ok -and $leaf.provenance.build.mode -ceq 'Reused' -and $leaf.provenance.build.command -eq $null }
    else { $ok=$ok -and $leaf.provenance.build.mode -ceq 'Executed' }
    Record-Control 'reuse' ($flag+'-valid-proof-no-new-stage-credit') $true $ok $directory $r.native.command $r.native.processExitCode $r.path
}
foreach ($flag in @('-NoBuild','-NoRestore')) { $null=Runner-Case 'reuse' ($flag+'-missing-proof-blocked') 'valid' 2 'Blocked' 'test' -Extra @($flag) }
$explicitFixture=New-GateControlFixture $executor 'valid' 'pair'
$explicitSeedDir=New-ControlDirectory; $explicitSeed=Invoke-ControlRunner $explicitFixture $explicitSeedDir
Record-Control 'reuse' 'explicit-build-seed' 0 $explicitSeed.native.processExitCode $explicitSeedDir $explicitSeed.native.command $explicitSeed.native.processExitCode $explicitSeed.path
$explicitDirectory=New-ControlDirectory
$explicit=Invoke-ControlRunner $explicitFixture $explicitDirectory @('-NoBuild','-ReuseManifestPath',(Join-Path $explicitSeed.runRoot 'reuse-manifest.json'))
Record-Control 'reuse' 'NoBuild-preserves-explicit-build-step' $true ($explicit.native.processExitCode -eq 0 -and $explicit.summary.children[0].provenance.build.mode -ceq 'Executed' -and $explicit.summary.children[1].provenance.build.mode -ceq 'Reused') $explicitDirectory $explicit.native.command $explicit.native.processExitCode $explicit.path
$restoreFixture=New-GateControlFixture $executor 'build-fail-once' 'build'
$restoreSeedDir=New-ControlDirectory; $restoreSeed=Invoke-ControlRunner $restoreFixture $restoreSeedDir
Record-Control 'reuse' 'failed-build-retains-independent-restore' 1 $restoreSeed.native.processExitCode $restoreSeedDir $restoreSeed.native.command $restoreSeed.native.processExitCode $restoreSeed.path
$restoreDir=New-ControlDirectory
$restored=Invoke-ControlRunner $restoreFixture $restoreDir @('-NoRestore','-ReuseManifestPath',(Join-Path $restoreSeed.runRoot 'reuse-manifest.json'))
Record-Control 'reuse' 'NoRestore-accepts-restore-alone-after-failed-build' 0 $restored.native.processExitCode $restoreDir $restored.native.command $restored.native.processExitCode $restored.path

foreach ($field in @('configuration','tfm','sdk','configSha256','sourceSha','restoreNative','buildNative','emptyOutputs')) {
    $directory=New-ControlDirectory; $copyRoot=Join-Path $directory 'prior'; $null=New-Item -ItemType Directory -Path $copyRoot
    # Enumerate only this owned source directory; no glob or latest-run search.
    Get-ChildItem -LiteralPath $seed.runRoot | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $copyRoot -Recurse }
    $indexPath=Join-Path $copyRoot 'reuse-manifest.json'; $index=Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json
    $entry=$index.receipts[0]; $receiptPath=Resolve-GateArtifact $copyRoot $entry.path
    $prior=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    switch ($field) {
        'configuration' { $prior.configuration='Release' }
        'tfm' { $prior.tfm='net9.0' }
        'sdk' { $prior.sdk='9.0.999' }
        'configSha256' { $prior.configSha256='0'*64 }
        'sourceSha' { $prior.source.sha='0'*40 }
        'restoreNative' { $prior.restore.processExitCode=7 }
        'buildNative' { $prior.build.processExitCode=7 }
        'emptyOutputs' { $prior.build.outputs=@() }
    }
    [IO.File]::WriteAllText($receiptPath,(ConvertTo-GateJson $prior)); $entry.sha256=Get-GateHash $receiptPath; $entry.bytes=(Get-Item -LiteralPath $receiptPath).Length
    [IO.File]::WriteAllText($indexPath,(ConvertTo-GateJson $index))
    $output=Join-Path $directory 'run'; $null=New-Item -ItemType Directory -Path $output
    $r=Invoke-ControlRunner $reuseFixture $output @('-NoBuild','-ReuseManifestPath',$indexPath)
    Record-Control 'reuse' ('corrupt-proof-'+$field) 1 $r.native.processExitCode $directory $r.native.command $r.native.processExitCode $r.path
    $restoreOutput=Join-Path $directory 'nr'; $null=New-Item -ItemType Directory -Path $restoreOutput
    $r=Invoke-ControlRunner $reuseFixture $restoreOutput @('-NoRestore','-ReuseManifestPath',$indexPath)
    $expectedRestore=if ($field -cin @('buildNative','emptyOutputs')) { 0 } else { 1 }
    Record-Control 'reuse' ('NoRestore-proof-'+$field) $expectedRestore $r.native.processExitCode $restoreOutput $r.native.command $r.native.processExitCode $r.path
}
foreach ($target in @('assets','source','DLL','package-tool')) {
    $directory=New-ControlDirectory
    $file=switch ($target) { 'assets' { $reuseFixture.data[1].assetsPath }; 'source' { $reuseFixture.data[1].compile[0] }; 'DLL' { $reuseFixture.data[1].targetPath }; 'package-tool' { Join-Path $reuseFixture.context.projectData[0].packageRoot 'fixture/1.0.0/tool.opaque' } }
    $original=[IO.File]::ReadAllBytes($file)
    try {
        [IO.File]::AppendAllText($file,'changed-identity')
        Copy-Item -LiteralPath $file -Destination (Join-Path $directory 'changed-input.bin')
        $r=Invoke-ControlRunner $reuseFixture $directory @('-NoBuild','-ReuseManifestPath',$manifestPath)
        Record-Control 'reuse' ('changed-'+$target+'-proof-refused') 1 $r.native.processExitCode $directory $r.native.command $r.native.processExitCode $r.path
    } finally { [IO.File]::WriteAllBytes($file,$original) }
}

# Actual validator mutation controls, all based on a previously accepted runner
# result and its immutable archives. The expected plan stays untouched.
$baseline=$seed.summary
$validationContext=[pscustomobject]@{runRoot=$seed.runRoot; allowExample=$true; control=$reuseFixture.context; abortStatus=$null}
function Validator-Case {
    param([string]$Name,[scriptblock]$Mutation,[bool]$AllowExample=$true,[bool]$ExpectedAccepted=$false,$BaselineRun=$null)
    $directory=New-ControlDirectory
    $trusted=if ($BaselineRun) { $BaselineRun.summary } else { $baseline }
    $runRoot=if ($BaselineRun) { $BaselineRun.runRoot } else { $seed.runRoot }
    $control=if ($BaselineRun) { Get-Content -LiteralPath (Join-Path $BaselineRun.fixtureRoot 'context.json') -Raw -Encoding UTF8 | ConvertFrom-Json } else { $reuseFixture.context }
    $candidate=ConvertTo-GateJson $trusted | ConvertFrom-Json
    & $Mutation $candidate
    [IO.File]::WriteAllText((Join-Path $directory 'candidate.json'),(ConvertTo-GateJson $candidate))
    $candidate=Get-Content -LiteralPath (Join-Path $directory 'candidate.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $ctx=[pscustomobject]@{runRoot=$runRoot; allowExample=$AllowExample; control=$control; abortStatus=$null}
    $accepted=$false; $reason=$null
    try { $null=Assert-GateResult $candidate $trusted $ctx; $accepted=$true } catch { $reason=$_.Exception.Message }
    Record-Control 'validator' $Name $ExpectedAccepted $accepted $directory @('Assert-GateResult','candidate.json','unchanged-assigned-plan') $null $reason
}
# Each mutation keeps its unchanged assigned plan and real fixture archives.
# Recomputed fingerprints make these association controls independent of scalar
# fingerprint rejection. Source replacements are separately valid archived files.
foreach ($binding in @('build-omit-primary','loaded-omit-primary','test-input-omit-primary','coherent-omit-primary','test-input-substitute-source','coherent-substitute-source','loaded-after-omit-primary','loaded-omit-dependency','test-input-omit-dependency','test-input-substitute-dependency','loaded-duplicate-normalized-path','test-input-duplicate-normalized-path','loaded-hash-contradiction','loaded-size-contradiction')) {
    Validator-Case ('binding-'+$binding) {
        param($r)
        $p=$r.children[0].provenance;$primary=$p.assembly
        $dependencyPath=Join-Path (Split-Path $primary) 'dependency.dll'
        $dependency=@($p.build.outputs | Where-Object path -ieq $dependencyPath)
        $replacement=@($p.build.inputs | Where-Object path -like '*.cs')[0]
        if ($dependency.Count -ne 1 -or -not $replacement) { throw 'Nonempty valid dependency/source binding fixture required.' }
        switch ($binding) {
            'build-omit-primary' { $p.build.outputs=@($p.build.outputs | Where-Object path -ine $primary) }
            'loaded-omit-primary' { $p.loadedBefore=@($p.loadedBefore | Where-Object path -ine $primary);$p.loadedAfter=$p.loadedBefore }
            'test-input-omit-primary' { $p.test.inputs=@($p.test.inputs | Where-Object path -ine $primary) }
            'coherent-omit-primary' { $p.build.outputs=@($p.build.outputs | Where-Object path -ine $primary);$p.loadedBefore=@($p.loadedBefore | Where-Object path -ine $primary);$p.loadedAfter=$p.loadedBefore;$p.test.inputs=$p.loadedBefore }
            'test-input-substitute-source' { $p.test.inputs=@($p.test.inputs | Where-Object path -ine $primary)+@($replacement) }
            'coherent-substitute-source' { $p.build.outputs=@($p.build.outputs | Where-Object path -ine $primary)+@($replacement);$p.loadedBefore=@($p.loadedBefore | Where-Object path -ine $primary)+@($replacement);$p.loadedAfter=$p.loadedBefore;$p.test.inputs=$p.loadedBefore }
            'loaded-after-omit-primary' { $p.loadedAfter=@($p.loadedAfter | Where-Object path -ine $primary) }
            'loaded-omit-dependency' { $p.loadedBefore=@($p.loadedBefore | Where-Object path -ine $dependency[0].path);$p.loadedAfter=$p.loadedBefore }
            'test-input-omit-dependency' { $p.test.inputs=@($p.test.inputs | Where-Object path -ine $dependency[0].path) }
            'test-input-substitute-dependency' { $p.test.inputs=@($p.test.inputs | Where-Object path -ine $dependency[0].path)+@($replacement) }
            'loaded-duplicate-normalized-path' { $alias=ConvertTo-GateJson $p.loadedBefore[0] | ConvertFrom-Json;$alias.path=$alias.path.ToUpperInvariant().Replace('\','/');$p.loadedBefore+=@($alias);$p.loadedAfter=$p.loadedBefore }
            'test-input-duplicate-normalized-path' { $alias=ConvertTo-GateJson $p.test.inputs[0] | ConvertFrom-Json;$alias.path=$alias.path.ToUpperInvariant().Replace('\','/');$p.test.inputs+=@($alias) }
            'loaded-hash-contradiction' { $p.loadedBefore[0].sha256='0'*64;$p.loadedAfter=$p.loadedBefore }
            'loaded-size-contradiction' { $p.loadedBefore[0].bytes++;$p.loadedAfter=$p.loadedBefore }
        }
        $p.build.outputFingerprint=Get-GateFingerprint $p.build.outputs
        $p.test.inputFingerprint=Get-GateFingerprint $p.test.inputs
    }
}
foreach ($bindingPositive in @('distinct-archive-locations','reordered-identities','alternate-valid-archives-and-roles','normalized-windows-paths')) {
    Validator-Case ('binding-positive-'+$bindingPositive) {
        param($r)
        $p=$r.children[0].provenance
        if ($p.build.outputs.Count -lt 2 -or $p.build.outputs[0].artifact.path -ceq $p.loadedBefore[0].artifact.path) { throw 'Distinct build/loaded archive locations required.' }
        switch ($bindingPositive) {
            'reordered-identities' { $p.loadedBefore=@($p.loadedBefore | Sort-Object path -Descending);$p.loadedAfter=@($p.loadedAfter | Sort-Object path);$p.test.inputs=@($p.test.inputs | Sort-Object path -Descending) }
            'alternate-valid-archives-and-roles' { $p.loadedAfter=@($p.build.outputs);$p.test.inputs=@($p.build.outputs) }
            'normalized-windows-paths' { foreach ($entry in $p.loadedBefore+$p.loadedAfter+$p.test.inputs) { $entry.path=$entry.path.ToUpperInvariant().Replace('\','/') } }
        }
        $p.test.inputFingerprint=Get-GateFingerprint $p.test.inputs
    } -ExpectedAccepted $true
}
# Existing actual NoBuild execution re-archives verified build outputs. The
# same set check applies without claiming that this run executed its reused build.
Validator-Case 'binding-reused-build-positive' { param($r) if ($r.children[1].provenance.build.mode -cne 'Reused' -or $r.children[1].provenance.build.command -ne $null) { throw 'Actual reused build required.' } } -ExpectedAccepted $true -BaselineRun $explicit
Validator-Case 'binding-reused-build-test-input-omission' { param($r) $p=$r.children[1].provenance;$p.test.inputs=@($p.test.inputs | Where-Object path -ine $p.assembly);$p.test.inputFingerprint=Get-GateFingerprint $p.test.inputs } -BaselineRun $explicit
Validator-Case 'binding-reused-build-loaded-omission' { param($r) $p=$r.children[1].provenance;$p.loadedBefore=@($p.loadedBefore | Where-Object path -ine $p.assembly);$p.loadedAfter=$p.loadedBefore } -BaselineRun $explicit
Validator-Case 'strict-lowercase-status' { param($r) $r.children[0].status='passed' }
Validator-Case 'strict-CLI-map' { param($r) $r.children[0].cliExitCode=2 }
Validator-Case 'native2-cannot-claim-Passed' { param($r) $r.children[0].processExitCode=2 }
Validator-Case 'old-run-result' { param($r) $r.children[0].runId=[guid]::NewGuid().ToString() }
Validator-Case 'duplicate-result-ID' { param($r) $r.children[0].resultId=$r.resultId }
Validator-Case 'wrong-parent-ID' { param($r) $r.children[0].parentResultId=[guid]::NewGuid().ToString() }
Validator-Case 'missing-child' { param($r) $r.children=@() }
Validator-Case 'empty-serialized-bindings' { param($r) $r.coverage.bindings.'test-project'=@() }
Validator-Case 'forged-descendant' { param($r) $r.coverage.bindings.'test-project'=@([guid]::NewGuid().ToString()) }
Validator-Case 'duplicate-binding' { param($r) $r.coverage.bindings.'test-project'=@($r.children[0].resultId,$r.children[0].resultId) }
Validator-Case 'forged-completion-summary' { param($r) $r.coverage.completed=@() }
Validator-Case 'forged-missing-summary' { param($r) $r.coverage.missing=@('test-project') }
Validator-Case 'forged-full-acceptance' { param($r) $r.fullGateAccepted=$false }
Validator-Case 'leaf-claims-full-acceptance' { param($r) $r.children[0].fullGateAccepted=$true }
Validator-Case 'leaf-claims-children' { param($r) $r.children[0].children=@($r) }
Validator-Case 'old-SHA-TRX-binding' { param($r) $r.children[0].provenance.trxBinding.sourceSha='0'*40 }
Validator-Case 'wrong-run-TRX-binding' { param($r) $r.children[0].provenance.trxBinding.runId=[guid]::NewGuid().ToString() }
Validator-Case 'wrong-filter-binding' { param($r) $r.children[0].provenance.trxBinding.filter='Gate=Wrong' }
Validator-Case 'wrong-filter-command' { param($r) $a=[string[]]($r.children[0].provenance.test.command.arguments[-1] | ConvertFrom-Json); $a[[Array]::IndexOf($a,'--filter')+1]='Gate=Wrong'; $r.children[0].provenance.test.command.arguments[-1]=ConvertTo-GateJson $a }
Validator-Case 'duplicate-filter-stage-and-native-command' {
    param($r)
    $stage=$r.children[0].provenance.test
    $a=[string[]]($stage.command.arguments[-1] | ConvertFrom-Json)
    $a+=@('--filter','Gate=Wrong'); $stage.command.arguments[-1]=ConvertTo-GateJson $a
    $native=$r.children[0].provenance.invocations | Where-Object startedAt -ceq $stage.startedAt | Select-Object -First 1
    $native.command.arguments[-1]=$stage.command.arguments[-1]; $native.toolArguments=$a
    $r.children[0].command.arguments[-1]=$stage.command.arguments[-1]
}
Validator-Case 'forged-leaf-summary-command' { param($r) $r.children[0].command.arguments=@('forged-summary-command') }
Validator-Case 'forged-leaf-summary-time' { param($r) $r.children[0].times.executionEndedAt='2000-01-01T00:00:00Z' }
Validator-Case 'native-tool-argv-contradiction' { param($r) ($r.children[0].provenance.invocations | Where-Object { $_.toolArguments[0] -ceq 'build' } | Select-Object -First 1).toolArguments=@('build','wrong-project') }
Validator-Case 'native-executable-contradiction' {
    param($r)
    $stage=$r.children[0].provenance.build
    $stage.command.executable='C:/not-the-verified-tool.exe'
    ($r.children[0].provenance.invocations | Where-Object startedAt -ceq $stage.startedAt | Select-Object -First 1).command.executable=$stage.command.executable
}
Validator-Case 'native-stream-other-node-path' { param($r) $r.children[0].provenance.invocations[0].stdoutPath=Join-Path $seed.runRoot 'foreign.stdout.txt' }
Validator-Case 'contradictory-test-counts' { param($r) $r.children[0].tests.counts.executed=0 }
Validator-Case 'wrong-test-definition' { param($r) $r.children[0].tests.identities[0].testId=[guid]::NewGuid().ToString() }
Validator-Case 'source-after-mismatch' { param($r) $r.children[0].source.after.inputFingerprint='0'*64 }
Validator-Case 'SDK-provenance-mismatch' { param($r) $r.children[0].provenance.sdk='9.0.999' }
Validator-Case 'native-stage-time-forgery' { param($r) $r.children[0].provenance.build.startedAt='2000-01-01T00:00:00Z' }
foreach ($identityCase in @('build-tfm','test-tfm','restore-tfm','restore-configuration','build-configuration','test-configuration')) {
    Validator-Case ('stage-and-native-'+$identityCase+'-mismatch') {
        param($r)
        $stageName=$identityCase.Split('-')[0]; $stage=$r.children[0].provenance.$stageName
        $arguments=[string[]]($stage.command.arguments[-1] | ConvertFrom-Json)
        if ($stageName -ceq 'restore') {
            $prefix=if ($identityCase -ceq 'restore-tfm') { '-p:TargetFramework=' } else { '-p:Configuration=' }
            if ($identityCase -ceq 'restore-tfm') { $arguments+=('-p:TargetFramework=net9.0') } else {
                for ($i=0; $i -lt $arguments.Count; $i++) { if ($arguments[$i].StartsWith($prefix)) { $arguments[$i]=$prefix+'Release' } }
            }
        } else {
            $flag=if ($identityCase.EndsWith('tfm')) { '-f' } else { '-c' }
            $arguments[[Array]::IndexOf($arguments,$flag)+1]=$(if ($flag -ceq '-f') { 'net9.0' } else { 'Release' })
        }
        $stage.command.arguments[-1]=ConvertTo-GateJson $arguments
        $matching=$r.children[0].provenance.invocations | Where-Object { $_.startedAt -ceq $stage.startedAt } | Select-Object -First 1
        $matching.command.arguments[-1]=$stage.command.arguments[-1]; $matching.toolArguments=$arguments
    }
}
Validator-Case 'forged-entire-source-SHA-rejected-by-parent' { param($r) $r.children[0].source.before.sha='0'*40; $r.children[0].source.after.sha='0'*40; $r.children[0].provenance.source.sha='0'*40; $r.children[0].provenance.trxBinding.sourceSha='0'*40 }
Validator-Case 'compiler-input-closure-empty' { param($r) $r.children[0].provenance.compilerInputs=@() }
Validator-Case 'compiler-input-hash-forgery' { param($r) $r.children[0].provenance.compilerInputs[0].sha256='0'*64 }
Validator-Case 'artifact-path-escape' { param($r) $r.children[0].artifacts[0].path='../escape.log' }
Validator-Case 'artifact-absolute-path' { param($r) $r.children[0].artifacts[0].path='C:/escape.log' }
Validator-Case 'artifact-backslash-path' { param($r) $r.children[0].artifacts[0].path='dir\escape.log' }
Validator-Case 'artifact-other-run-owner' { param($r) $r.children[0].artifacts[0].runId=[guid]::NewGuid().ToString() }
Validator-Case 'artifact-other-result-owner' { param($r) $r.children[0].artifacts[0].resultId=[guid]::NewGuid().ToString() }
Validator-Case 'artifact-length-forgery' { param($r) $r.children[0].artifacts[0].bytes++ }
Validator-Case 'artifact-hash-forgery' { param($r) $r.children[0].artifacts[0].sha256='0'*64 }
Validator-Case 'duplicate-artifact' { param($r) $r.children[0].artifacts+= $r.children[0].artifacts[0] }
Validator-Case 'undeclared-optional-skip' { param($r) $r.children[0].status='Skipped'; $r.children[0].cliExitCode=3 }
Validator-Case 'synthetic-production-refusal' { param($r) } $false
$productionDirectory=New-ControlDirectory
$productionFixture=New-GateControlFixture $executor 'valid' 'test'
$production=Invoke-ControlRunner $productionFixture $productionDirectory @() $true
Record-Control 'production' 'synthetic-config-refused-before-any-dotnet-launch' $true ($production.native.processExitCode -eq 1 -and $production.summary.reason -like '*SyntheticProductionConfiguration*' -and $production.launches.Count -eq 0) $productionDirectory $production.native.command $production.native.processExitCode $production.path

# Actual repository declarations are inventoried through preflight only. No
# dotnet workload can launch: legacy entries lack contracts; Cooking uses a
# deliberately missing selector before execution.
$repositoryConfig=Get-Content -LiteralPath (Join-Path $repoRoot 'tools/test-gates.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($gate in $repositoryConfig.gates) {
    $directory=New-ControlDirectory
    $argv=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$runner,'-Gate',$gate.name,'-ResultsDirectory',$directory)
    $isCooking=$gate.name -cin @('cooking-et-level-runtime','cooking-kitchen-loop')
    if ($isCooking) { $argv+=@('-StepName','never-match-control-selector') }
    $native=Invoke-GateNative $powershell $argv $directory 'repository' $repoRoot
    $summaryFile=Get-ChildItem -LiteralPath $directory -Filter gate-summary.json -Recurse | Select-Object -First 1
    $summary=Get-Content -LiteralPath $summaryFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $expected=if ($isCooking) { 1 } else { 2 }
    $uninvoked=@($summary.children | Where-Object { $_.status -cne 'NotRun' -or $_.command -ne $null -or $_.processExitCode -ne $null -or $_.times.executionStartedAt -ne $null }).Count -eq 0
    Record-Control 'compatibility' ('repository-preflight-'+$gate.name) ([pscustomobject]@{exit=$expected; uninvoked=$true}) ([pscustomobject]@{exit=$native.processExitCode; uninvoked=$uninvoked}) $directory $native.command $native.processExitCode $summaryFile.FullName
}

# Direct raw stream checks run real fake processes at several console widths.
foreach ($width in $requiredWidths) {
    $directory=New-ControlDirectory
    $widthPath=Join-Path $directory 'width-evidence.json'
    $argv=@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'fixtures/gate-result-native-probe.ps1'),'-WidthMode','Window','-Width',[string]$width,'-NativeExit','7','-WidthEvidencePath',$widthPath)
    $native=Invoke-GateNative $powershell $argv $directory 'probe' $repoRoot
    $out=[IO.File]::ReadAllText($native.stdoutPath); $err=[IO.File]::ReadAllText($native.stderrPath)
    $ok=$native.processExitCode -eq 7 -and $out -ceq ('stdout:'+('O'*8192)+"`r`n") -and $err -ceq ('stderr:'+('E'*8192)+"`r`nWARNING: required evidence unavailable; skipping`r`n")
    $widthEvidence=Get-Content -LiteralPath $widthPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $widthAccounted=(Test-ControlWindowWidth $widthEvidence $width) -or ($widthEvidence.dimension -ceq $widthDimension -and $widthEvidence.requestedWidth -eq $width -and $widthEvidence.widthCoverageStatus -ceq 'Blocked' -and -not $widthEvidence.applied -and $widthEvidence.limitation)
    Record-Control 'native-streams' ('window-width-'+$width+'-long-lines-direct-capture') $true ([bool]($ok -and $widthAccounted)) $directory $native.command $native.processExitCode ([pscustomobject]@{stdout=$native.stdoutPath; stderr=$native.stderrPath; stdoutBytes=(Get-Item $native.stdoutPath).Length; stderrBytes=(Get-Item $native.stderrPath).Length; streamsExact=[bool]$ok; exitConfirmed=$native.exitConfirmed; widthPath=$widthPath; width=$widthEvidence})
}
# Mutate actual subprocess receipts to prove aggregation cannot accept nominal
# widths, repeated observations, failed setters or truncated streams.
$actualWidthRecords=@($cases | Where-Object { $_.group -ceq 'native-streams' })
foreach ($mutation in @('dimension','duplicate','setter','streams')) {
    $directory=New-ControlDirectory
    $mutated=(ConvertTo-GateJson $actualWidthRecords) | ConvertFrom-Json
    switch ($mutation) {
        'dimension' { $mutated[0].evidence.width.dimension='RawUI.BufferSize.Width' }
        'duplicate' { $mutated[1].evidence.width.requestedWidth=$mutated[0].evidence.width.requestedWidth }
        'setter' { $mutated[0].evidence.width.setterSucceeded=$false }
        'streams' { $mutated[0].evidence.streamsExact=$false }
    }
    Record-Control 'native-width-bindings' ('reject-'+$mutation) 'Blocked' (Get-ControlWidthStatus $mutated) $directory @('Get-ControlWidthStatus',$mutation) $null ([pscustomobject]@{original=@($actualWidthRecords | ForEach-Object { $_.rawDirectory }); mutated=$mutated})
}
$reparseDirectory=New-ControlDirectory
$target=Join-Path $reparseDirectory 'target'; $link=Join-Path $reparseDirectory 'link'
$null=New-Item -ItemType Directory -Path $target
[IO.File]::WriteAllText((Join-Path $target 'raw.txt'),'reparse target')
$null=New-Item -ItemType Junction -Path $link -Target $target
$reparseAccepted=$false; $reparseReason=$null
try { $null=Resolve-GateArtifact $reparseDirectory 'link/raw.txt'; $reparseAccepted=$true } catch { $reparseReason=$_.Exception.Message }
Record-Control 'paths' 'reparse-artifact-refused' $false $reparseAccepted $reparseDirectory @('Resolve-GateArtifact','link/raw.txt') $null $reparseReason
$sourceAfter=Get-GateSource $repoRoot $sourceInputs $sourceBefore.evidenceExclusions
$stabilityDirectory=New-ControlDirectory
Record-Control 'freeze' 'implementation-inputs-before-after-identical' $true ($sourceBefore.sha -ceq $sourceAfter.sha -and $sourceBefore.inputFingerprint -ceq $sourceAfter.inputFingerprint) $stabilityDirectory @('Get-GateSource','sourceInputs') $null ([pscustomobject]@{before=$sourceBefore; after=$sourceAfter})
Save-ControlReceipt $true
$failed=@($cases | Where-Object { $_.status -ceq 'Failed' }).Count
Write-Output ('Controls: {0} executed, {1} failed. Receipt: {2}' -f $cases.Count,$failed,(Join-Path $artifactRootFull 'controls.json'))
$final=Get-Content -LiteralPath (Join-Path $artifactRootFull 'controls.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Write-Output ('Actual console-width coverage: '+$final.consoleWidthCoverage.status)
exit $(if ($failed -gt 0) { 1 } elseif ($final.consoleWidthCoverage.status -ceq 'Blocked') { 2 } elseif ($final.consoleWidthCoverage.status -ceq 'NotRun') { 4 } else { 0 })
