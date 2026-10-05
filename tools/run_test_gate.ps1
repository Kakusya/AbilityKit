param(
    [Alias('GateId')][string]$Gate,
    [string]$StepName,
    [string]$ConfigPath='tools\test-gates.json',
    [string]$Configuration='Debug',
    [string]$ResultsDirectory='local\Logs\test-gates',
    [switch]$List,
    [switch]$NoRestore,
    [switch]$NoBuild,
    [switch]$CI,
    [string]$ReuseManifestPath,
    [int]$TimeoutSeconds=0,
    [string]$CancelSignalPath,
    # Isolated controls only. This cannot confer real .NET/Unity acceptance.
    [string]$ControlContextPath
)
$ErrorActionPreference='Stop'
if ($CI) { $ProgressPreference='SilentlyContinue' }
. (Join-Path $PSScriptRoot 'test-gate-result-contract.ps1')
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
function Resolve-RepoPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}
$runId=[guid]::NewGuid().ToString()
$resultsRoot=Resolve-RepoPath $ResultsDirectory
Assert-GateNoReparse $resultsRoot
$runRoot=Join-Path $resultsRoot $runId
if (Test-Path -LiteralPath $runRoot) { throw 'Run directory already exists.' }
$null=New-Item -ItemType Directory -Path $runRoot
$context=[pscustomobject]@{runRoot=$runRoot; repoRoot=$repoRoot; configuration=$Configuration; allowExample=$false; control=$null; controlPath=$null; dotnetPath=$null; sdkInputPaths=@(); timeoutSeconds=$TimeoutSeconds; cancelSignalPath=$CancelSignalPath; abortStatus=$null; abortReason=$null; implementationInputs=@(); exclusions=@('local/ (ignored evidence and fixture outputs)','artifacts/ (ignored outputs)', '.trellis/tasks/ (coordinator-owned evidence; not compilation inputs)')}
$root=$null; $plan=$null; $configHash=$null; $gatesByName=@{}; $preflight=@(); $config=$null
try {
    $resolvedConfig=Resolve-RepoPath $ConfigPath
    $configHash=Get-GateHash $resolvedConfig
    $context.implementationInputs=@($resolvedConfig,$PSCommandPath,(Join-Path $PSScriptRoot 'test-gate-result-contract.ps1'))
    $config=Get-Content -LiteralPath $resolvedConfig -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $config.gates -or $config.gates.Count -eq 0) { throw 'Empty gate configuration.' }
    foreach ($gateDef in $config.gates) {
        if (-not $gateDef.name -or $gatesByName.ContainsKey([string]$gateDef.name)) { throw 'Missing/duplicate gate name.' }
        $gatesByName[[string]$gateDef.name]=$gateDef
    }
    if ($List) {
        foreach ($gateDef in $config.gates) {
            $diagnostic=if (-not $gateDef.requiredCoverage) { 'Blocked: MissingCoverageContract' } elseif (@($gateDef.steps | Where-Object { $_.kind -cnotin @('dotnet-build','dotnet-test','gate') }).Count) { 'Blocked: UnsupportedProducerContract' } else { 'Declared; selected-tree validation required' }
            Write-Output ('{0} [{1}] {2}' -f $gateDef.name,$gateDef.level,$diagnostic)
        }
        exit 0
    }
    if ($config.example -and -not $ControlContextPath) { throw 'SyntheticProductionConfiguration' }
    if (-not $Gate) { $Gate=[string]$config.defaultGate }
    if (-not $gatesByName.ContainsKey($Gate)) { throw 'Unknown gate selector.' }
    if ($Configuration -notmatch '^[A-Za-z0-9_.-]+$' -or $TimeoutSeconds -lt 0) { throw 'Invalid configuration/timeout.' }
    if ($ControlContextPath) {
        $control=Get-Content -LiteralPath $ControlContextPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $tempPrefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
        $fixtureRoot=[IO.Path]::GetFullPath($control.fixtureRoot)
        if (-not $fixtureRoot.StartsWith($tempPrefix,[StringComparison]::OrdinalIgnoreCase) -or -not ([IO.Path]::GetFullPath($ControlContextPath)).StartsWith($fixtureRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or -not $resolvedConfig.StartsWith($fixtureRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Control context/config must belong to an isolated TEMP fixture.' }
        $allowedExecutor=Join-Path $PSScriptRoot 'tests/fixtures/gate-result-fake-dotnet.ps1'
        if ($control.schemaVersion -ne 1 -or -not $control.example -or [IO.Path]::GetFullPath($control.executor) -ine [IO.Path]::GetFullPath($allowedExecutor) -or $control.executorSha256 -cne (Get-GateHash $allowedExecutor)) { throw 'Invalid isolated control executor.' }
        Assert-GateNoReparse $fixtureRoot
        $context.control=$control; $context.controlPath=[IO.Path]::GetFullPath($ControlContextPath); $context.allowExample=$true
        $context.implementationInputs+=@($allowedExecutor,$context.controlPath)
    } else {
        $dotnet=Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($dotnet) { $context.dotnetPath=$dotnet.Source; $context.implementationInputs+=$dotnet.Source }
    }
    function New-ConfiguredTree([string]$GateName,[string]$ParentId,[string]$Path,[string[]]$Visiting) {
        if ($GateName -cin $Visiting) { throw 'Cyclic gate configuration.' }
        if (-not $gatesByName.ContainsKey($GateName)) { throw 'Unknown nested gate.' }
        $def=$gatesByName[$GateName]
        if (-not $def.steps -or $def.steps.Count -eq 0) { throw 'Empty configured gate.' }
        $required=@($def.requiredCoverage | Where-Object { $null -ne $_ }); $optional=@($def.optionalCoverage | Where-Object { $null -ne $_ })
        if (-not $required.Count) { $script:preflight+= 'MissingCoverageContract' }
        $declaration=[pscustomobject]@{required=$required; optional=$optional; skipPolicy=$null}
        $settings=[pscustomobject]@{configSha256=$configHash; project=$null; tfm=$null; configuration=$Configuration; filter=''}
        $node=New-GateResult $runId $GateName 'gate' $ParentId $Path $declaration $settings $context.allowExample
        $node.source=[pscustomobject]@{before=(Get-GateSource $repoRoot $context.implementationInputs $context.exclusions); after=$null}
        $null=New-Item -ItemType Directory -Path (Get-GateNodeRoot $runRoot $node.resultId)
        $index=0
        foreach ($step in $def.steps) {
            $index++
            if (-not $step.name -or -not $step.kind) { throw 'Step identity missing.' }
            $childPath=$Path+'/'+$index
            if ($step.kind -ceq 'gate') {
                $child=New-ConfiguredTree ([string]$step.gate) $node.resultId $childPath @($Visiting+$GateName)
                $child.name=[string]$step.name
            } else {
                if ($step.kind -cnotin @('dotnet-build','dotnet-test','powershell-script','unity-editmode-test','unity-playmode-test','unity-execute-method')) { throw 'Unknown producer kind.' }
                if ($step.kind -cnotin @('dotnet-build','dotnet-test')) { $script:preflight+= 'UnsupportedProducerContract' }
                $tokens=@($step.coverage | Where-Object { $null -ne $_ })
                if (-not $tokens.Count -or -not $step.tfm -or -not $step.project) { $script:preflight+= 'MissingCoverageContract' }
                $isRequired=$step.required -ne $false
                $leafRequired=@(); $leafOptional=@()
                if ($isRequired) { $leafRequired=$tokens } else { $leafOptional=$tokens }
                $decl=[pscustomobject]@{required=$leafRequired; optional=$leafOptional; skipPolicy=$step.skipPolicy}
                $project=if ($step.project) { Resolve-RepoPath $step.project } else { $null }
                if ($context.control -and $project -and -not $project.StartsWith($context.control.fixtureRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Control project outside owned fixture.' }
                $settings=[pscustomobject]@{configSha256=$configHash; project=$project; tfm=$step.tfm; configuration=$Configuration; filter=$(if ($step.filter) { [string]$step.filter } else { '' })}
                $child=New-GateResult $runId ([string]$step.name) ([string]$step.kind) $node.resultId $childPath $decl $settings $context.allowExample
                $null=New-Item -ItemType Directory -Path (Get-GateNodeRoot $runRoot $child.resultId)
            }
            $node.children+= $child
        }
        # Explicit declarations must resolve to configured concrete leaves.
        $childTokens=@(foreach ($child in $node.children) { $child.declaration.required; $child.declaration.optional })
        $declared=@($required+$optional)
        if ($declared.Count) {
            if (@($declared | Where-Object { -not $_ -or $_ -match '\{|\}' }).Count -or @($declared | Sort-Object -Unique).Count -ne $declared.Count) { throw 'Malformed/duplicate coverage declaration.' }
            Assert-GateEqual @($declared | Sort-Object) @($childTokens | Sort-Object -Unique) 'configured coverage closure'
            foreach ($child in $node.children) {
                if (@($child.declaration.required | Where-Object { $_ -cnotin $required }).Count -or @($child.declaration.optional | Where-Object { $_ -cnotin $optional }).Count) { throw 'Required/optional declaration contradicts child.' }
            }
        }
        return $node
    }
    $root=New-ConfiguredTree $Gate $null $Gate @()
    $plan=ConvertTo-GateJson $root | ConvertFrom-Json
    $selectedPath=$null
    function Find-GateSelection($Node) {
        if ($Node.name -ceq $StepName -or $Node.invocationPath -ceq $StepName) { $script:selectionMatches+= $Node }
        foreach ($child in $Node.children) { Find-GateSelection $child }
    }
    if ($StepName) {
        $selectionMatches=@(); Find-GateSelection $root
        if ($selectionMatches.Count -eq 0) { throw 'StepNameNotFound' }
        if ($selectionMatches.Count -ne 1) { throw 'AmbiguousStepName' }
        $selectedPath=$selectionMatches[0].invocationPath
    }
    if ($preflight.Count) {
        $context.abortStatus='Blocked'; $context.abortReason=(@($preflight | Sort-Object -Unique) -join ';')
    } else {
        $stop=$false
        function Invoke-ConfiguredTree($Node,$Def) {
            $Node.times.invokedAt=[DateTimeOffset]::UtcNow.ToString('o')
            for ($i=0; $i -lt $Node.children.Count; $i++) {
                $child=$Node.children[$i]; $step=$Def.steps[$i]
                if ($script:stop) { continue }
                $matches= -not $selectedPath -or $child.invocationPath -ceq $selectedPath -or $child.invocationPath.StartsWith($selectedPath+'/') -or $selectedPath.StartsWith($child.invocationPath+'/')
                if (-not $matches) { continue }
                if ($child.kind -ceq 'gate') { Invoke-ConfiguredTree $child $gatesByName[[string]$step.gate] }
                else {
                    Invoke-GateDotNet $context $child $step -NoBuild:$NoBuild -NoRestore:$NoRestore -ReuseManifestPath $ReuseManifestPath
                    if ($child.status -cin @('Failed','Blocked')) { $script:stop=$true }
                }
            }
        }
        Invoke-ConfiguredTree $root $gatesByName[$Gate]
    }
} catch {
    $context.abortStatus='Failed'; $context.abortReason=$_.Exception.Message
    if (-not $root) {
        $declaration=[pscustomobject]@{required=@(); optional=@(); skipPolicy=$null}
        $settings=[pscustomobject]@{configSha256=$configHash; project=$null; tfm=$null; configuration=$Configuration; filter=''}
        $root=New-GateResult $runId $(if ($Gate) { $Gate } else { 'InvalidConfiguration' }) 'gate' $null $(if ($Gate) { $Gate } else { 'InvalidConfiguration' }) $declaration $settings $context.allowExample
        $null=New-Item -ItemType Directory -Path (Get-GateNodeRoot $runRoot $root.resultId)
    }
    if (-not $plan) { $plan=ConvertTo-GateJson $root | ConvertFrom-Json }
}
Complete-GateCoverage $root
function Bind-AggregateSource($Node) {
    foreach ($child in $Node.children) { Bind-AggregateSource $child }
    if ($Node.kind -ceq 'gate') {
        $before=if ($Node.source -and $Node.source.before) { $Node.source.before } else { Get-GateSource $repoRoot $context.implementationInputs $context.exclusions }
        $entries=@{}; foreach ($entry in $before.inputs) { $entries[$entry.path]=$entry }
        foreach ($child in $Node.children) {
            if ($child.source -and $child.source.before) {
                foreach ($entry in $child.source.before.inputs) {
                    if ($entries.ContainsKey($entry.path) -and $entries[$entry.path].sha256 -cne $entry.sha256) { $context.abortStatus='Failed'; $context.abortReason='SourceChangedBetweenChildren' }
                    $entries[$entry.path]=$entry
                }
            }
        }
        $before.inputs=@($entries.Values | Sort-Object path); $before.inputFingerprint=Get-GateFingerprint $before.inputs
        $Node.source=[pscustomobject]@{before=$before; after=(Get-GateSource $repoRoot @($before.inputs.path) $context.exclusions)}
        if ($Node.source.before.inputFingerprint -cne $Node.source.after.inputFingerprint) { $context.abortStatus='Failed'; $context.abortReason='AggregateSourceChanged' }
    }
}
Bind-AggregateSource $root
if ($context.abortStatus) { Set-GateTerminal $root $context.abortStatus $context.abortReason; $root.fullGateAccepted=$false }
try {
    $normalized=Assert-GateResult $root $plan $context
    $json=ConvertTo-GateJson $normalized
    $summaryPath=Join-Path $runRoot 'gate-summary.json'
    [IO.File]::WriteAllText($summaryPath,$json,[Text.UTF8Encoding]::new($false))
    $roundtrip=Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $null=Assert-GateResult $roundtrip $plan $context
} catch {
    $context.abortStatus='Failed'; $context.abortReason='ResultValidationFailed: '+$_.Exception.Message
    Set-GateTerminal $root 'Failed' $context.abortReason; $root.fullGateAccepted=$false
    $summaryPath=Join-Path $runRoot 'gate-summary.json'
    [IO.File]::WriteAllText($summaryPath,(ConvertTo-GateJson $root),[Text.UTF8Encoding]::new($false))
}
# Explicit index; consumers choose this exact file. Never search latest directories.
$receiptEntries=@{}
function Collect-GateReceipts($Node) {
    if ($Node.provenance -and $Node.provenance.restore) {
        $receipt=Join-Path (Get-GateNodeRoot $runRoot $Node.resultId) 'stage-receipt.json'
        $key=$Node.configuration.project+'|'+$Node.configuration.tfm+'|'+$Configuration
        $receiptEntries[$key]=[pscustomobject]@{project=$Node.configuration.project; tfm=$Node.configuration.tfm; configuration=$Configuration; path=$Node.resultId.Replace('-','').Substring(0,12)+'/stage-receipt.json'; runId=$Node.runId; resultId=$Node.resultId; bytes=(Get-Item -LiteralPath $receipt).Length; sha256=Get-GateHash $receipt}
    }
    foreach ($child in $Node.children) { Collect-GateReceipts $child }
}
Collect-GateReceipts $root
$receiptIndex=[pscustomobject]@{schemaVersion=1; example=$context.allowExample; receipts=@($receiptEntries.Values | Sort-Object project,tfm,configuration)}
[IO.File]::WriteAllText((Join-Path $runRoot 'reuse-manifest.json'),(ConvertTo-GateJson $receiptIndex),[Text.UTF8Encoding]::new($false))
Write-Output ('Gate {0}: {1}; CLI {2}; fullGateAccepted={3}; example={4}' -f $root.name,$root.status,$root.cliExitCode,$root.fullGateAccepted,$root.example)
Write-Output ('Summary: '+$summaryPath)
exit $root.cliExitCode
