param([Parameter(Mandatory=$true)][string]$ExpectedSha,[Parameter(Mandatory=$true)][string]$OutputRoot)
$ErrorActionPreference='Stop'
$repo=(Get-Location).Path
. ./tools/test-gate-result-contract.ps1
. ./tools/tests/fixtures/gate-result-model.ps1
$actualSha=(& git rev-parse HEAD).Trim()
if($actualSha -cne $ExpectedSha){throw 'Unexpected implementation SHA'}
if(Test-Path -LiteralPath $OutputRoot){throw 'Evidence root must be fresh'}
$root=[IO.Path]::GetFullPath($OutputRoot)
$null=New-Item -ItemType Directory -Path $root
$inputs=@('tools/run_test_gate.ps1','tools/test-gate-result-contract.ps1','tools/test-gates.json','tools/tests/test-gate-result-contract.tests.ps1','tools/tests/fixtures/gate-result-model.ps1','tools/tests/fixtures/gate-result-fake-dotnet.ps1','tools/tests/fixtures/gate-result-native-probe.ps1',$PSCommandPath)|ForEach-Object {[IO.Path]::GetFullPath($_)}
$before=Get-GateSource $repo $inputs @('local/ ignored isolated main controls','main-owned task evidence outside implementation closure')
$records=@()
$ps=(Get-Command powershell -CommandType Application|Select-Object -First 1).Source
$runner=Join-Path $repo 'tools/run_test_gate.ps1'
$executor=Join-Path $repo 'tools/tests/fixtures/gate-result-fake-dotnet.ps1'
function Record-MainControl([string]$Name,$Expected,$Actual,[string]$Raw,$Native=$null){
    $ok=(ConvertTo-GateJson $Expected) -ceq (ConvertTo-GateJson $Actual)
    $script:records+= [pscustomobject]@{name=$Name;expected=$Expected;actual=$Actual;status=$(if($ok){'Passed'}else{'Failed'});raw=$Raw;native=$Native}
}
function Run-MainFixture([string]$Name,[string]$Mode,[string]$Shape='mixed'){
    $f=New-GateControlFixture $executor $Mode $Shape
    $importDirectory=Join-Path $f.root ('independent import '+[char]0x6761+[char]0x4ef6)
    $null=New-Item -ItemType Directory -Path $importDirectory
    $importFile=Join-Path $importDirectory 'conditional file.nonstandard'
    [IO.File]::WriteAllText($importFile,'<Project />')
    foreach($p in $f.context.projectData){$p|Add-Member -NotePropertyName preprocessedOnlyImports -NotePropertyValue @($importFile)}
    Save-GateControlFixture $f
    $dir=Join-Path $root $Name
    $null=New-Item -ItemType Directory -Path $dir
    $args=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$runner,'-Gate','control','-ConfigPath',$f.configPath,'-ControlContextPath',$f.contextPath,'-ResultsDirectory',$dir,'-CI')
    $n=Invoke-GateNative $ps $args $dir 'runner' $repo
    [IO.File]::WriteAllText((Join-Path $dir 'native.json'),(ConvertTo-GateJson $n))
    $p=@(Get-ChildItem -LiteralPath $dir -Recurse -Filter gate-summary.json)
    if($p.Count -ne 1){throw 'Exactly one result required'}
    $s=Get-Content -LiteralPath $p[0].FullName -Raw -Encoding UTF8|ConvertFrom-Json
    return [pscustomobject]@{fixture=$f;summary=$s;summaryPath=$p[0].FullName;runRoot=$p[0].DirectoryName;native=$n;import=$importFile}
}
$positive=Run-MainFixture 'mixed-comments-positive' 'valid'
$retained=$true
foreach($leaf in $positive.summary.children){
    $entries=@($leaf.source.before.inputs|Where-Object path -ieq $positive.import)
    $buildEntries=@($leaf.provenance.build.inputs|Where-Object path -ieq $positive.import)
    if($entries.Count -ne 1 -or $buildEntries.Count -ne 1 -or $entries[0].sha256 -cne (Get-GateHash $positive.import)){$retained=$false}
}
Record-MainControl 'new-SHA-mixed-real-shaped-comments-import-retained' @{exit=0;status='Passed';retained=$true;source=$ExpectedSha} @{exit=$positive.native.processExitCode;status=$positive.summary.status;retained=$retained;source=$positive.summary.source.before.sha} $positive.summaryPath $positive.native
$graph=$positive.summary.children[1].provenance.restore.graph
$configs=@($graph.configPaths|Sort-Object -Unique)
$hashOnly=$true
foreach($leaf in $positive.summary.children){
    foreach($stage in @($leaf.provenance.restore,$leaf.provenance.build)){
        foreach($config in @($leaf.provenance.closure.configPaths|Sort-Object -Unique)){
            $entry=@($stage.inputs|Where-Object path -eq $config)
            if($entry.Count -ne 1 -or $entry[0].proofKind -cne 'EffectiveNuGetConfigHashOnly' -or $entry[0].artifact -ne $null -or $entry[0].sha256 -cne (Get-GateHash $config)){$hashOnly=$false}
        }
    }
}
Record-MainControl 'mixed-reference-TFM-and-hash-only-config-positive' $true ($hashOnly -and @($graph|Where-Object tfm -eq 'netstandard2.0').Count -eq 1 -and $configs.Count -gt 0 -and @((@($positive.summary.children[1].provenance.invocations|Where-Object {$_.toolArguments[0] -ceq 'restore'})[0].toolArguments)|Where-Object {$_ -match '^(-p:|/p:|--framework|--property:)(TargetFramework|net)'}).Count -eq 0) $positive.summaryPath
$changed=Run-MainFixture 'changed-import-negative' 'import-change'
Record-MainControl 'new-SHA-changing-import-refused' @{exit=1;status='Failed';reason=$true} @{exit=$changed.native.processExitCode;status=$changed.summary.status;reason=($changed.summary.children[0].reason -like 'Source/assets/binary changed:*conditional file.nonstandard')} $changed.summaryPath $changed.native
function Set-MainTrustedProducerPins($Context,$TrustedSummary,[string]$Name){
    $pins=@{}
    foreach($leaf in $TrustedSummary.children){
        $receipt=@($leaf.artifacts|Where-Object {$_.path -ceq 'stage-receipt.json' -and $_.role -ceq 'stage-receipt'})
        if($receipt.Count -ne 1){throw 'Trusted fresh producer receipt missing or ambiguous'}
        $pins[$leaf.resultId]=[pscustomobject]@{artifact=(ConvertTo-GateJson $receipt[0]|ConvertFrom-Json);sourceSha=$leaf.provenance.source.sha;sourceFingerprint=$leaf.provenance.source.inputFingerprint}
    }
    $Context|Add-Member -NotePropertyName producerReceipts -NotePropertyValue $pins -Force
    [IO.File]::WriteAllText((Join-Path $root ($Name+'-trusted-producer-pins.json')),(ConvertTo-GateJson $pins))
}
$plan=ConvertTo-GateJson $positive.summary|ConvertFrom-Json
$context=[pscustomobject]@{runRoot=$positive.runRoot;allowExample=$true;abortStatus=$null;control=$positive.fixture.context;configuration='Debug';repoRoot=$repo;dotnetPath=$null}
Set-MainTrustedProducerPins $context $positive.summary 'Executed'
foreach($name in @('producer-coherent-secondary-omission','producer-coherent-secondary-substitution','valid-roundtrip','empty-bindings','lowercase-status','missing-child-with-forged-completion','native-seven-claims-passed','wrong-source','forged-filter','omitted-build-config-coherent-fingerprint','config-proof-extra-content','non-config-hash-only-proof','retargeted-reference-declaration','binding-build-omit-primary','binding-loaded-omit-primary','binding-test-input-omit-primary','binding-coherent-omit-primary','binding-test-input-substitute-source','binding-coherent-substitute-source','binding-loaded-omit-secondary','binding-test-input-omit-secondary','binding-test-input-substitute-secondary-source','binding-loaded-duplicate-secondary','binding-test-input-duplicate-secondary','binding-loaded-order-positive','binding-test-input-order-positive','binding-loaded-test-order-positive')){
    $candidate=ConvertTo-GateJson $positive.summary|ConvertFrom-Json
    switch($name){
        'producer-coherent-secondary-omission' {
            $leaf=$candidate.children[1];$secondary=@($leaf.provenance.build.outputs|Where-Object {$_.path -ine $leaf.provenance.assembly -and $_.path -like '*dependency.dll'})[0]
            if(-not $secondary){throw 'Required independent secondary witness absent'}
            foreach($slot in @('build','loadedBefore','loadedAfter','test')){
                if($slot -eq 'build'){$leaf.provenance.build.outputs=@($leaf.provenance.build.outputs|Where-Object path -ine $secondary.path)}
                elseif($slot -eq 'test'){$leaf.provenance.test.inputs=@($leaf.provenance.test.inputs|Where-Object path -ine $secondary.path)}
                else{$leaf.provenance.$slot=@($leaf.provenance.$slot|Where-Object path -ine $secondary.path)}
            }
            $leaf.provenance.build.outputFingerprint=Get-GateFingerprint $leaf.provenance.build.outputs
            $leaf.provenance.test.inputFingerprint=Get-GateFingerprint $leaf.provenance.test.inputs
        }
        'producer-coherent-secondary-substitution' {
            $leaf=$candidate.children[1];$secondary=@($leaf.provenance.build.outputs|Where-Object {$_.path -ine $leaf.provenance.assembly -and $_.path -like '*dependency.dll'})[0]
            $replacement=@($leaf.provenance.build.inputs|Where-Object path -like '*.cs')[0]
            if(-not $secondary -or -not $replacement){throw 'Required independently valid source and secondary absent'}
            $leaf.provenance.build.outputs=@($leaf.provenance.build.outputs|Where-Object path -ine $secondary.path)+@($replacement)
            $leaf.provenance.loadedBefore=@($leaf.provenance.loadedBefore|Where-Object path -ine $secondary.path)+@($replacement)
            $leaf.provenance.loadedAfter=@($leaf.provenance.loadedAfter|Where-Object path -ine $secondary.path)+@($replacement)
            $leaf.provenance.test.inputs=@($leaf.provenance.test.inputs|Where-Object path -ine $secondary.path)+@($replacement)
            $leaf.provenance.build.outputFingerprint=Get-GateFingerprint $leaf.provenance.build.outputs
            $leaf.provenance.test.inputFingerprint=Get-GateFingerprint $leaf.provenance.test.inputs
        }
        'empty-bindings' {$candidate.coverage.bindings.'test-project'=@()}
        'lowercase-status' {$candidate.children[0].status='passed'}
        'missing-child-with-forged-completion' {$candidate.children=@($candidate.children[1])}
        'native-seven-claims-passed' {$candidate.children[0].processExitCode=7}
        'wrong-source' {$candidate.children[1].source.before.sha='0'*40;$candidate.children[1].source.after.sha='0'*40;$candidate.children[1].provenance.source.sha='0'*40;$candidate.children[1].provenance.trxBinding.sourceSha='0'*40}
        'forged-filter' {$candidate.children[1].provenance.trxBinding.filter='Gate=Unrelated'}
        'omitted-build-config-coherent-fingerprint' {
            $leaf=$candidate.children[1];$configs=@($leaf.provenance.closure.configPaths)
            $leaf.provenance.build.inputs=@($leaf.provenance.build.inputs|Where-Object {$_.path -notin $configs})
            $leaf.provenance.build.inputFingerprint=Get-GateFingerprint $leaf.provenance.build.inputs
        }
        'config-proof-extra-content' {
            $entry=@($candidate.children[1].provenance.restore.inputs|Where-Object proofKind -eq 'EffectiveNuGetConfigHashOnly')[0]
            $entry|Add-Member -NotePropertyName content -NotePropertyValue 'forged payload outside typed proof'
        }
        'non-config-hash-only-proof' {
            $entry=@($candidate.children[1].provenance.build.inputs|Where-Object {$_.path -like '*.cs'})[0]
            $entry.artifact=$null;$entry|Add-Member -NotePropertyName proofKind -NotePropertyValue 'EffectiveNuGetConfigHashOnly'
            $candidate.children[1].provenance.build.inputFingerprint=Get-GateFingerprint $candidate.children[1].provenance.build.inputs
        }
        'binding-build-omit-primary' {
            $leaf=$candidate.children[1];$primary=$leaf.provenance.assembly
            $leaf.provenance.build.outputs=@($leaf.provenance.build.outputs|Where-Object path -ne $primary)
            $leaf.provenance.build.outputFingerprint=Get-GateFingerprint $leaf.provenance.build.outputs
        }
        'binding-loaded-omit-primary' {
            $leaf=$candidate.children[1];$primary=$leaf.provenance.assembly
            $leaf.provenance.loadedBefore=@($leaf.provenance.loadedBefore|Where-Object path -ne $primary)
            $leaf.provenance.loadedAfter=$leaf.provenance.loadedBefore
        }
        'binding-test-input-omit-primary' {
            $leaf=$candidate.children[1];$primary=$leaf.provenance.assembly
            $leaf.provenance.test.inputs=@($leaf.provenance.test.inputs|Where-Object path -ne $primary)
            $leaf.provenance.test.inputFingerprint=Get-GateFingerprint $leaf.provenance.test.inputs
        }
        'binding-coherent-omit-primary' {
            $leaf=$candidate.children[1];$primary=$leaf.provenance.assembly
            $leaf.provenance.build.outputs=@($leaf.provenance.build.outputs|Where-Object path -ne $primary)
            $leaf.provenance.build.outputFingerprint=Get-GateFingerprint $leaf.provenance.build.outputs
            $leaf.provenance.loadedBefore=@($leaf.provenance.loadedBefore|Where-Object path -ne $primary)
            $leaf.provenance.loadedAfter=$leaf.provenance.loadedBefore
            $leaf.provenance.test.inputs=$leaf.provenance.loadedBefore
            $leaf.provenance.test.inputFingerprint=Get-GateFingerprint $leaf.provenance.test.inputs
        }
        'binding-test-input-substitute-source' {
            $leaf=$candidate.children[1];$primary=$leaf.provenance.assembly
            $replacement=@($leaf.provenance.build.inputs|Where-Object path -like '*.cs')[0]
            if (-not $replacement) { throw 'Meaningful valid source artifact unavailable.' }
            $leaf.provenance.test.inputs=@($leaf.provenance.test.inputs|Where-Object path -ne $primary)+@($replacement)
            $leaf.provenance.test.inputFingerprint=Get-GateFingerprint $leaf.provenance.test.inputs
        }
        'binding-coherent-substitute-source' {
            $leaf=$candidate.children[1];$primary=$leaf.provenance.assembly
            $replacement=@($leaf.provenance.build.inputs|Where-Object path -like '*.cs')[0]
            if (-not $replacement) { throw 'Meaningful valid source artifact unavailable.' }
            $leaf.provenance.build.outputs=@($leaf.provenance.build.outputs|Where-Object path -ne $primary)+@($replacement)
            $leaf.provenance.build.outputFingerprint=Get-GateFingerprint $leaf.provenance.build.outputs
            $leaf.provenance.loadedBefore=@($leaf.provenance.loadedBefore|Where-Object path -ne $primary)+@($replacement)
            $leaf.provenance.loadedAfter=$leaf.provenance.loadedBefore
            $leaf.provenance.test.inputs=$leaf.provenance.loadedBefore
            $leaf.provenance.test.inputFingerprint=Get-GateFingerprint $leaf.provenance.test.inputs
        }
        'binding-loaded-omit-secondary' {
            $leaf=$candidate.children[1];$secondary=@($leaf.provenance.build.outputs|Where-Object {$_.path -ine $leaf.provenance.assembly -and $_.path -like '*dependency.dll'})[0]
            if(-not $secondary){throw 'Required nonprimary dependency witness absent'}
            $leaf.provenance.loadedBefore=@($leaf.provenance.loadedBefore|Where-Object path -ine $secondary.path)
            $leaf.provenance.loadedAfter=$leaf.provenance.loadedBefore
        }
        'binding-test-input-omit-secondary' {
            $leaf=$candidate.children[1];$secondary=@($leaf.provenance.build.outputs|Where-Object {$_.path -ine $leaf.provenance.assembly -and $_.path -like '*dependency.dll'})[0]
            if(-not $secondary){throw 'Required nonprimary dependency witness absent'}
            $leaf.provenance.test.inputs=@($leaf.provenance.test.inputs|Where-Object path -ine $secondary.path)
            $leaf.provenance.test.inputFingerprint=Get-GateFingerprint $leaf.provenance.test.inputs
        }
        'binding-test-input-substitute-secondary-source' {
            $leaf=$candidate.children[1];$secondary=@($leaf.provenance.build.outputs|Where-Object {$_.path -ine $leaf.provenance.assembly -and $_.path -like '*dependency.dll'})[0]
            $replacement=@($leaf.provenance.build.inputs|Where-Object path -like '*.cs')[0]
            if(-not $secondary -or -not $replacement){throw 'Valid nonprimary/source witness absent'}
            $leaf.provenance.test.inputs=@($leaf.provenance.test.inputs|Where-Object path -ine $secondary.path)+@($replacement)
            $leaf.provenance.test.inputFingerprint=Get-GateFingerprint $leaf.provenance.test.inputs
        }
        'binding-loaded-duplicate-secondary' {
            $leaf=$candidate.children[1];$secondary=@($leaf.provenance.loadedBefore|Where-Object {$_.path -ine $leaf.provenance.assembly -and $_.path -like '*dependency.dll'})[0]
            if(-not $secondary){throw 'Required nonprimary loaded witness absent'}
            $leaf.provenance.loadedBefore=@($leaf.provenance.loadedBefore)+@($secondary)
            $leaf.provenance.loadedAfter=$leaf.provenance.loadedBefore
        }
        'binding-test-input-duplicate-secondary' {
            $leaf=$candidate.children[1];$secondary=@($leaf.provenance.test.inputs|Where-Object {$_.path -ine $leaf.provenance.assembly -and $_.path -like '*dependency.dll'})[0]
            if(-not $secondary){throw 'Required nonprimary test witness absent'}
            $leaf.provenance.test.inputs=@($leaf.provenance.test.inputs)+@($secondary)
            $leaf.provenance.test.inputFingerprint=Get-GateFingerprint $leaf.provenance.test.inputs
        }
        'binding-loaded-order-positive' {
            $leaf=$candidate.children[1];$reordered=@($leaf.provenance.loadedBefore);[Array]::Reverse($reordered)
            $leaf.provenance.loadedBefore=$reordered;$leaf.provenance.loadedAfter=$reordered
        }
        'binding-test-input-order-positive' {
            $leaf=$candidate.children[1];$reordered=@($leaf.provenance.test.inputs);[Array]::Reverse($reordered)
            $leaf.provenance.test.inputs=$reordered;$leaf.provenance.test.inputFingerprint=Get-GateFingerprint $reordered
        }
        'binding-loaded-test-order-positive' {
            $leaf=$candidate.children[1];$reordered=@($leaf.provenance.build.outputs);[Array]::Reverse($reordered)
            $leaf.provenance.build.outputs=$reordered;$leaf.provenance.build.outputFingerprint=Get-GateFingerprint $reordered
            $reordered=@($leaf.provenance.loadedBefore);[Array]::Reverse($reordered)
            $leaf.provenance.loadedBefore=$reordered;$leaf.provenance.loadedAfter=$reordered
        }
        'retargeted-reference-declaration' {
            $reference=@($candidate.children[1].provenance.restore.graph|Where-Object tfm -eq 'netstandard2.0')[0]
            $reference.tfm='net10.0';$reference.frameworks=@('net10.0')
        }

    }
    $path=Join-Path $root ($name+'.json');[IO.File]::WriteAllText($path,(ConvertTo-GateJson $candidate))
    $accepted=$true;$reason=$null
    $candidate=Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json
    try{$null=Assert-GateResult $candidate $plan $context}catch{$accepted=$false;$reason=$_.Exception.Message}
    Record-MainControl $name ($name -in @('valid-roundtrip','binding-loaded-order-positive','binding-test-input-order-positive','binding-loaded-test-order-positive')) $accepted $path
    [IO.File]::WriteAllText((Join-Path $root ($name+'-decision.json')),(ConvertTo-GateJson @{accepted=$accepted;reason=$reason}))
}
# Fresh test-only seed and explicitly assigned reuse manifest. This is isolated.
$reuseSeed=Run-MainFixture 'producer-reuse-test-seed' 'valid' 'test'
Record-MainControl 'producer-reuse-seed-valid' $true ($reuseSeed.native.processExitCode -eq 0 -and $reuseSeed.summary.status -ceq 'Passed') $reuseSeed.summaryPath $reuseSeed.native
$reusePath=Join-Path $reuseSeed.runRoot 'reuse-manifest.json'
foreach($reuseFlag in @('-NoBuild','-NoRestore')){
    $reuseDir=Join-Path $root ('producer-reuse'+$reuseFlag);$null=New-Item -ItemType Directory -Path $reuseDir
    $reuseArgs=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$runner,'-Gate','control','-ConfigPath',$reuseSeed.fixture.configPath,'-ControlContextPath',$reuseSeed.fixture.contextPath,'-ResultsDirectory',$reuseDir,'-CI',$reuseFlag,'-ReuseManifestPath',$reusePath)
    $reuseNative=Invoke-GateNative $ps $reuseArgs $reuseDir 'runner' $repo
    [IO.File]::WriteAllText((Join-Path $reuseDir 'native.json'),(ConvertTo-GateJson $reuseNative))
    $reuseSummaryPath=@(Get-ChildItem -LiteralPath $reuseDir -Recurse -Filter gate-summary.json)
    if($reuseSummaryPath.Count -ne 1){throw 'Exactly one independent reuse summary required'}
    $reuseSummary=Get-Content -LiteralPath $reuseSummaryPath[0].FullName -Raw -Encoding UTF8|ConvertFrom-Json
    Record-MainControl ('producer'+$reuseFlag+'-valid-explicit-reuse') $true ($reuseNative.processExitCode -eq 0 -and $reuseSummary.status -ceq 'Passed') $reuseSummaryPath[0].FullName $reuseNative
    if($reuseFlag -eq '-NoRestore'){continue}
    $reusePlan=ConvertTo-GateJson $reuseSummary|ConvertFrom-Json
    $reuseContext=[pscustomobject]@{runRoot=$reuseSummaryPath[0].DirectoryName;allowExample=$true;abortStatus=$null;control=$reuseSeed.fixture.context;configuration='Debug';repoRoot=$repo;dotnetPath=$null;reuseManifestPath=$reusePath}
    Set-MainTrustedProducerPins $reuseContext $reuseSummary 'Reused'
    foreach($case in @('unchanged-positive','coherent-secondary-omission','coherent-secondary-substitution','forged-stage-runId','forged-stage-resultId')){
        $candidate=ConvertTo-GateJson $reuseSummary|ConvertFrom-Json
        if($case -in @('coherent-secondary-omission','coherent-secondary-substitution')){
            $leaf=$candidate.children[0];$secondary=@($leaf.provenance.build.outputs|Where-Object {$_.path -ine $leaf.provenance.assembly -and $_.path -like '*dependency.dll'})[0]
            if(-not $secondary -or $leaf.provenance.build.mode -cne 'Reused'){throw 'Meaningful Reused secondary witness absent'}
            $replacement=@();if($case -eq 'coherent-secondary-substitution'){$replacement=@($leaf.provenance.build.inputs|Where-Object path -like '*.cs'|Select-Object -First 1);if($replacement.Count -ne 1){throw 'Valid owned replacement absent'}}
            $leaf.provenance.build.outputs=@($leaf.provenance.build.outputs|Where-Object path -ine $secondary.path)+$replacement
            $leaf.provenance.loadedBefore=@($leaf.provenance.loadedBefore|Where-Object path -ine $secondary.path)+$replacement
            $leaf.provenance.loadedAfter=@($leaf.provenance.loadedAfter|Where-Object path -ine $secondary.path)+$replacement
            $leaf.provenance.test.inputs=@($leaf.provenance.test.inputs|Where-Object path -ine $secondary.path)+$replacement
            $leaf.provenance.build.outputFingerprint=Get-GateFingerprint $leaf.provenance.build.outputs
            $leaf.provenance.test.inputFingerprint=Get-GateFingerprint $leaf.provenance.test.inputs
        }
        if($case -eq 'forged-stage-runId'){$candidate.children[0].provenance.build.runId=[guid]::NewGuid().ToString()}
        if($case -eq 'forged-stage-resultId'){$candidate.children[0].provenance.build.resultId=[guid]::NewGuid().ToString()}
        $path=Join-Path $root ('producer-Reused-'+$case+'.json');[IO.File]::WriteAllText($path,(ConvertTo-GateJson $candidate));$candidate=Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json
        $accepted=$true;$reason=$null;try{$null=Assert-GateResult $candidate $reusePlan $reuseContext}catch{$accepted=$false;$reason=$_.Exception.Message}
        Record-MainControl ('producer-Reused-'+$case) ($case -eq 'unchanged-positive') $accepted $path
        [IO.File]::WriteAllText((Join-Path $root ('producer-Reused-'+$case+'-decision.json')),(ConvertTo-GateJson @{accepted=$accepted;reason=$reason}))
    }
}

# Prepare a contract-blocked context for the independent same-second launch pair.
# It must never start a fake or real dotnet producer.
$pair=New-GateControlFixture $executor
$pair.config.gates[0].requiredCoverage=@()
Save-GateControlFixture $pair
[IO.File]::WriteAllText((Join-Path $root 'same-second-fixture.json'),(ConvertTo-GateJson @{configPath=$pair.configPath;contextPath=$pair.contextPath;launchesPath=$pair.context.launchesPath;runner=$runner;powershell=$ps;repo=$repo}))
$after=Get-GateSource $repo $inputs $before.evidenceExclusions
Record-MainControl 'input-and-HEAD-stability' $true ($before.sha -ceq $after.sha -and $before.inputFingerprint -ceq $after.inputFingerprint) $PSCommandPath
$failed=@($records|Where-Object status -ceq 'Failed').Count
[IO.File]::WriteAllText((Join-Path $root 'controls.json'),(ConvertTo-GateJson @{example=$true;acceptance='IndependentIsolatedContractControlsOnly';sourceBefore=$before;sourceAfter=$after;records=$records;executed=$records.Count;passed=$records.Count-$failed;failed=$failed;status=$(if($failed){'Failed'}else{'Passed'});realCooking='NotRun'}))
if($failed){exit 1};exit 0
