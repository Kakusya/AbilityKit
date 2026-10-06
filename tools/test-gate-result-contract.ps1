# Cooking gate result contract. Dot-source only; loading never launches a producer.
# Selected reuse from 15e79fefd5ba1d9294491122c91df286ef94e2f8 is documented in slice1-report.md.
$script:GateStates = @('Passed', 'Failed', 'Blocked', 'Skipped', 'NotRun')

function Get-GateHash {
    param([string]$Path)
    if (-not [IO.File]::Exists($Path)) { throw "Missing file: $Path" }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-GateTextHash {
    param([string]$Text)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)))).Replace('-', '').ToLowerInvariant() }
    finally { $hash.Dispose() }
}

function ConvertTo-GateJson {
    param($Value)
    return ConvertTo-Json -InputObject $Value -Depth 100 -Compress
}

function Assert-GateEqual {
    param($Actual, $Expected, [string]$Label)
    if ((ConvertTo-GateJson $Actual) -cne (ConvertTo-GateJson $Expected)) { throw "Contract mismatch: $Label" }
}

function Get-GateFingerprint {
    param([object[]]$Entries)
    return Get-GateTextHash ((@($Entries | Sort-Object path | ForEach-Object { $_.path + [char]0 + $_.sha256 + [char]0 + $_.bytes + "`n" })) -join '')
}

function Assert-GateNoReparse {
    param([string]$Path)
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if ((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Reparse path: $current" }
        $parent = [IO.Directory]::GetParent($current)
        $current = if ($parent) { $parent.FullName } else { $null }
    }
}

function Resolve-GateArtifact {
    param([string]$Root, [string]$RelativePath)
    if ($RelativePath -cnotmatch '^[A-Za-z0-9_-][A-Za-z0-9._-]*(/[A-Za-z0-9_-][A-Za-z0-9._-]*)*$') { throw 'Artifact must use a contained relative path.' }
    foreach ($part in $RelativePath.Split('/')) {
        if ($part -in @('.', '..') -or $part -match '[ .]$|^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)') { throw 'Unsafe artifact path.' }
    }
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $full = [IO.Path]::GetFullPath((Join-Path $base $RelativePath))
    if (-not $full.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Artifact escapes root.' }
    Assert-GateNoReparse $full
    return $full
}

function Get-GateNodeRoot {
    param([string]$RunRoot, [string]$ResultId)
    # Short, assigned directory key keeps Windows PowerShell 5.1 evidence paths
    # usable in deep Orca checkouts. Allocation rejects any prefix collision.
    return Join-Path $RunRoot ($ResultId.Replace('-','').Substring(0,12))
}

function Add-GateArtifact {
    param($Result, [string]$RunRoot, [string]$Path, [string]$Role)
    $nodeRoot = Get-GateNodeRoot $RunRoot $Result.resultId
    $prefix = [IO.Path]::GetFullPath($nodeRoot).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Artifact has a different owner.' }
    $relative = $full.Substring($prefix.Length).Replace('\', '/')
    $null = Resolve-GateArtifact $nodeRoot $relative
    $entry = [pscustomobject][ordered]@{runId=$Result.runId; resultId=$Result.resultId; path=$relative; role=$Role; bytes=(Get-Item -LiteralPath $full).Length; sha256=Get-GateHash $full}
    if (@($Result.artifacts | Where-Object { $_.path -ceq $relative }).Count) { throw 'Duplicate artifact path.' }
    $Result.artifacts += $entry
    return $entry
}

function Assert-GateArtifact {
    param($Artifact, $Result, [string]$RunRoot)
    if ($Artifact.runId -cne $Result.runId -or $Artifact.resultId -cne $Result.resultId) { throw 'Stale artifact owner.' }
    $full = Resolve-GateArtifact (Get-GateNodeRoot $RunRoot $Result.resultId) $Artifact.path
    if (-not [IO.File]::Exists($full) -or (Get-Item -LiteralPath $full).Length -ne $Artifact.bytes -or (Get-GateHash $full) -cne $Artifact.sha256) { throw 'Missing/replaced artifact.' }
    return $full
}

function Get-GateSource {
    param([string]$RepoRoot, [string[]]$InputPaths, [string[]]$Exclusions=@())
    $sha = (& git -C $RepoRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Source SHA unavailable.' }
    $dirtyDetails = @(& git -C $RepoRoot -c core.quotepath=false status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { throw 'Source dirty details unavailable.' }
    $entries = @(foreach ($path in @($InputPaths | Sort-Object -Unique)) {
        $full = [IO.Path]::GetFullPath($path)
        Assert-GateNoReparse $full
        if (-not [IO.File]::Exists($full)) { throw "Evaluated input missing: $full" }
        [pscustomobject]@{path=$full; bytes=(Get-Item -LiteralPath $full).Length; sha256=Get-GateHash $full}
    })
    return [pscustomobject][ordered]@{sha=$sha; dirty=($dirtyDetails.Count -gt 0); dirtyDetails=$dirtyDetails; evidenceExclusions=@($Exclusions); inputs=$entries; inputFingerprint=Get-GateFingerprint $entries}
}

function Assert-GateInputs {
    param([object[]]$Entries)
    foreach ($entry in $Entries) {
        Assert-GateNoReparse $entry.path
        if (-not [IO.File]::Exists($entry.path) -or (Get-Item -LiteralPath $entry.path).Length -ne $entry.bytes -or (Get-GateHash $entry.path) -cne $entry.sha256) { throw "Source/assets/binary changed: $($entry.path)" }
    }
}

function Save-GateFiles {
    param($Result, [string]$RunRoot, [string[]]$Paths, [string]$Role, [string[]]$HashOnlyConfigPaths=@())
    $entries = @(); $index = 0
    foreach ($path in @($Paths | Sort-Object -Unique)) {
        $full = [IO.Path]::GetFullPath($path)
        Assert-GateNoReparse $full
        if (-not [IO.File]::Exists($full)) { throw "Missing $Role input: $full" }
        if ($full -iin $HashOnlyConfigPaths) {
            $entries += [pscustomobject]@{path=$full; bytes=(Get-Item -LiteralPath $full).Length; sha256=Get-GateHash $full; artifact=$null; proofKind='EffectiveNuGetConfigHashOnly'}
            continue
        }
        if ([IO.Path]::GetFileName($full) -ieq 'NuGet.Config') { throw 'NuGet config was not bound by SDK discovery; content archive forbidden.' }
        $destination = Join-Path (Get-GateNodeRoot $RunRoot $Result.resultId) ('f'+$Result.artifacts.Count+'-'+$index+[IO.Path]::GetExtension($full))
        $index++
        Copy-Item -LiteralPath $full -Destination $destination
        $artifact = Add-GateArtifact $Result $RunRoot $destination $Role
        $entries += [pscustomobject]@{path=$full; bytes=(Get-Item -LiteralPath $full).Length; sha256=Get-GateHash $full; artifact=$artifact}
        if ($entries[-1].sha256 -cne $artifact.sha256) { throw 'Input changed while archiving.' }
    }
    return ,$entries
}

function ConvertTo-GateProcessArgument {
    param([string]$Value)
    # Windows CommandLineToArgvW quoting, including trailing backslashes.
    return '"' + [regex]::Replace([regex]::Replace($Value, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
}

function Invoke-GateNative {
    param([string]$Executable, [string[]]$Arguments, [string]$Directory, [string]$Stem, [string]$WorkingDirectory, [int]$TimeoutSeconds=0, [string]$CancelSignalPath)
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName=$Executable; $info.Arguments=(@($Arguments | ForEach-Object { ConvertTo-GateProcessArgument $_ }) -join ' ')
    $info.WorkingDirectory=$WorkingDirectory; $info.UseShellExecute=$false; $info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    $process = New-Object Diagnostics.Process
    $process.StartInfo=$info
    $startedAt=[DateTimeOffset]::UtcNow.ToString('o'); $stopReason=$null; $code=$null; $pidOwned=$null; $exitConfirmed=$false
    $stdoutPath=Join-Path $Directory ($Stem+'.stdout.txt'); $stderrPath=Join-Path $Directory ($Stem+'.stderr.txt')
    try {
        if (-not $process.Start()) { throw 'Process launch failed.' }
        $pidOwned=$process.Id
        # Drain both streams concurrently. No PowerShell ErrorRecord formatting or merged chronology.
        $outStream=[IO.File]::Create($stdoutPath); $errStream=[IO.File]::Create($stderrPath)
        try {
            $outTask=$process.StandardOutput.BaseStream.CopyToAsync($outStream)
            $errTask=$process.StandardError.BaseStream.CopyToAsync($errStream)
            $watch=[Diagnostics.Stopwatch]::StartNew()
            while (-not $process.WaitForExit(50)) {
                if ($TimeoutSeconds -gt 0 -and $watch.Elapsed.TotalSeconds -ge $TimeoutSeconds) { $stopReason='Timeout' }
                if ($CancelSignalPath -and [IO.File]::Exists($CancelSignalPath)) { $stopReason='StartedCancellation' }
                if ($stopReason) {
                    # Only this Process instance is owned. Production does not launch arbitrary shell producers.
                    $process.Kill()
                    if (-not $process.WaitForExit(10000)) { throw "UnknownWriter: owned pid $pidOwned did not exit." }
                    break
                }
            }
            $process.WaitForExit(); $exitConfirmed=$process.HasExited; $code=$process.ExitCode
            $null=$outTask.GetAwaiter().GetResult(); $null=$errTask.GetAwaiter().GetResult()
        } finally { $outStream.Dispose(); $errStream.Dispose() }
    } finally { $process.Dispose() }
    return [pscustomobject][ordered]@{command=[pscustomobject]@{executable=$Executable; arguments=@($Arguments); workingDirectory=$WorkingDirectory}; startedAt=$startedAt; endedAt=[DateTimeOffset]::UtcNow.ToString('o'); processExitCode=$code; reason=$stopReason; ownedProcessId=$pidOwned; exitConfirmed=$exitConfirmed; stdoutPath=$stdoutPath; stderrPath=$stderrPath}
}

function New-GateResult {
    param([string]$RunId, [string]$Name, [string]$Kind, [string]$ParentResultId, [string]$InvocationPath, $Declaration, $Configuration, [bool]$Example)
    return [pscustomobject][ordered]@{
        schemaVersion=1; example=$Example; runId=$RunId; resultId=[guid]::NewGuid().ToString(); parentResultId=$ParentResultId; invocationPath=$InvocationPath
        name=$Name; kind=$Kind; status='NotRun'; cliExitCode=4; processExitCode=$null; reason='UnselectedOrNotReached'; declaration=$Declaration
        source=$null; configuration=$Configuration; command=$null; tools=@(); times=[pscustomobject]@{invokedAt=$null; executionStartedAt=$null; executionEndedAt=$null}
        coverage=[pscustomobject]@{required=@($Declaration.required); optional=@($Declaration.optional); completed=@(); missing=@($Declaration.required+$Declaration.optional); bindings=[pscustomobject]@{}}
        tests=$null; artifacts=@(); provenance=$null; children=@(); fullGateAccepted=$false
    }
}

function Set-GateTerminal {
    param($Result, [string]$Status, [string]$Reason)
    $index=[Array]::IndexOf($script:GateStates, $Status)
    if ($index -lt 0) { throw 'Noncanonical terminal status.' }
    $Result.status=$Status; $Result.cliExitCode=$index; $Result.reason=$Reason
}

function Get-GateDerivedCoverage {
    param($Result)
    $declared=@($Result.coverage.required+$Result.coverage.optional); $bindings=[ordered]@{}
    if ($Result.kind -ceq 'gate') {
        foreach ($child in $Result.children) {
            foreach ($token in $child.coverage.completed) {
                if ($token -cin $declared) {
                    if (-not $bindings.Contains($token)) { $bindings[$token]=@() }
                    $bindings[$token]+= @($child.coverage.bindings.PSObject.Properties[$token].Value)
                }
            }
        }
    } elseif ($Result.status -ceq 'Passed') {
        foreach ($token in $declared) { $bindings[$token]=@($Result.resultId) }
    }
    $completed=@($declared | Where-Object { $bindings.Contains($_) })
    $missing=@($declared | Where-Object { -not $bindings.Contains($_) })
    return [pscustomobject]@{required=@($Result.coverage.required); optional=@($Result.coverage.optional); completed=$completed; missing=$missing; bindings=[pscustomobject]$bindings}
}

function Get-GateAggregateStatus {
    param($Result)
    if (@($Result.children | Where-Object { $_.status -ceq 'Failed' }).Count) { return 'Failed' }
    if (@($Result.children | Where-Object { $_.status -ceq 'Blocked' }).Count) { return 'Blocked' }
    if (@($Result.coverage.required | Where-Object { $_ -cin $Result.coverage.missing }).Count) { return 'NotRun' }
    if (@($Result.coverage.required+$Result.coverage.optional).Count -eq 0) { return 'NotRun' }
    if (-not $Result.children.Count) { return 'NotRun' }
    return 'Passed'
}

function Complete-GateCoverage {
    param($Result)
    foreach ($child in $Result.children) { Complete-GateCoverage $child }
    $Result.coverage=Get-GateDerivedCoverage $Result
    if ($Result.kind -ceq 'gate') {
        $status=Get-GateAggregateStatus $Result
        Set-GateTerminal $Result $status $(if ($status -ceq 'Passed') { $null } else { 'ChildFailureOrIncompleteCoverage' })
        $Result.fullGateAccepted=($status -ceq 'Passed')
    }
}

function Get-GateTrxTests {
    param([string]$Path, [string]$AssemblyPath, [string]$Filter, [string]$StartedAt, [string]$EndedAt)
    $settings=New-Object Xml.XmlReaderSettings
    $settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit; $settings.XmlResolver=$null
    $reader=[Xml.XmlReader]::Create($Path,$settings)
    try { $xml=New-Object Xml.XmlDocument; $xml.XmlResolver=$null; $xml.Load($reader) } finally { $reader.Dispose() }
    if ($xml.DocumentElement.LocalName -cne 'TestRun') { throw 'Malformed TRX root.' }
    $select="/*[local-name()='TestRun']"
    $results=@($xml.SelectNodes($select+"/*[local-name()='Results']/*[local-name()='UnitTestResult']"))
    $definitions=@($xml.SelectNodes($select+"/*[local-name()='TestDefinitions']/*[local-name()='UnitTest']"))
    $summary=$xml.SelectSingleNode($select+"/*[local-name()='ResultSummary']")
    $counters=$xml.SelectSingleNode($select+"/*[local-name()='ResultSummary']/*[local-name()='Counters']")
    $times=$xml.SelectSingleNode($select+"/*[local-name()='Times']")
    $runGuid=[guid]::Empty
    if (-not [guid]::TryParse([string]$xml.DocumentElement.id, [ref]$runGuid) -or $runGuid -eq [guid]::Empty -or -not $times) { throw 'TRX identity/times missing.' }
    $start=[DateTimeOffset]::Parse([string]$times.start); $finish=[DateTimeOffset]::Parse([string]$times.finish)
    if ($start -lt [DateTimeOffset]::Parse($StartedAt).AddSeconds(-2) -or $finish -gt [DateTimeOffset]::Parse($EndedAt).AddSeconds(2) -or $finish -lt $start) { throw 'Stale TRX time.' }
    if (-not $counters -or -not $summary -or $results.Count -eq 0) { throw 'Zero/absent TRX execution.' }
    $byTest=@{}; $seen=@{}; $raw=@{}; $identities=@()
    foreach ($definition in $definitions) {
        if (-not $definition.id -or $byTest.ContainsKey([string]$definition.id)) { throw 'Duplicate/missing test definition.' }
        $byTest[[string]$definition.id]=$definition
    }
    $counts=[ordered]@{total=$results.Count; resultEntries=$results.Count; passed=0; failed=0; skipped=0; notExecuted=0; executed=0}
    foreach ($entry in $results) {
        $execution=[string]$entry.executionId; $test=[string]$entry.testId; $outcome=[string]$entry.outcome
        $id=[guid]::Empty
        if (-not [guid]::TryParse($execution,[ref]$id) -or $seen.ContainsKey($execution)) { throw 'Duplicate/missing TRX execution identity.' }
        $seen[$execution]=$true
        $definition=$byTest[$test]
        if (-not $definition) { throw 'TRX result has no definition.' }
        $method=$definition.SelectSingleNode("*[local-name()='TestMethod']")
        $definitionExecution=$definition.SelectSingleNode("*[local-name()='Execution']")
        if (-not $method -or -not $definitionExecution -or [string]$definitionExecution.id -cne $execution -or [string]$definition.name -cne [string]$entry.testName) { throw 'TRX definition/result contradiction.' }
        $assembly=[string]$method.codeBase
        if (-not $assembly) { $assembly=[string]$definition.storage }
        if (-not [IO.Path]::IsPathRooted($assembly)) { $assembly=Join-Path (Split-Path $AssemblyPath) $assembly }
        if ([IO.Path]::GetFullPath($assembly) -ine [IO.Path]::GetFullPath($AssemblyPath)) { throw 'Wrong TRX assembly.' }
        if ($outcome -cnotin @('Passed','Failed','Error','Timeout','Aborted','Inconclusive','Warning','NotExecuted','Pending','NotRunnable','Disconnected')) { throw 'Noncanonical TRX outcome.' }
        $key=$outcome.Substring(0,1).ToLowerInvariant()+$outcome.Substring(1)
        if (-not $raw.ContainsKey($key)) { $raw[$key]=0 }; $raw[$key]++
        if ($outcome -ceq 'Passed') { $counts.passed++ }
        elseif ($outcome -cin @('NotExecuted','Pending','NotRunnable','Disconnected')) { $counts.notExecuted++ }
        else { $counts.failed++ }
        $identities += [pscustomobject]@{testId=$test; executionId=$execution; name=[string]$entry.testName; assembly=[IO.Path]::GetFullPath($assembly); outcome=$outcome}
    }
    $counts.executed=$counts.passed+$counts.failed
    foreach ($key in @('passed','failed','error','timeout','aborted','inconclusive','warning','notExecuted','pending','notRunnable','disconnected','completed','inProgress','passedButRunAborted')) {
        $actual=if ($raw.ContainsKey($key)) { $raw[$key] } else { 0 }
        if ($counters.HasAttribute($key)) { if ([int]$counters.GetAttribute($key) -ne $actual) { throw "TRX counter contradiction: $key" } }
        elseif ($actual -gt 0) { throw "Missing TRX counter: $key" }
    }
    foreach ($key in @('total','executed')) { if (-not $counters.HasAttribute($key) -or [int]$counters.GetAttribute($key) -ne $counts[$key]) { throw "TRX counter contradiction: $key" } }
    # VSTest's TrxLogger emits completed=0 for ordinary passed tests. It is
    # an outcome counter, not an alias for executed or a second completion sum.
    return [pscustomobject]@{counts=[pscustomobject]$counts; identities=$identities; trxRunId=[string]$xml.DocumentElement.id; assembly=[IO.Path]::GetFullPath($AssemblyPath); filter=$Filter; summaryOutcome=[string]$summary.outcome; rawCounters=@(foreach ($attribute in $counters.Attributes) { [pscustomobject]@{name=$attribute.Name; value=$attribute.Value} })}
}

function Assert-GateTests {
    param($Tests)
    if (-not $Tests) { throw 'Test evidence absent.' }
    $c=$Tests.counts
    foreach ($key in @('total','resultEntries','passed','failed','skipped','notExecuted','executed')) {
        if (($c.$key -isnot [int] -and $c.$key -isnot [long]) -or $c.$key -lt 0) { throw 'Invalid test count.' }
    }
    if ($c.total -ne $c.resultEntries -or $c.total -ne $c.passed+$c.failed+$c.skipped+$c.notExecuted -or $c.executed -ne $c.passed+$c.failed -or $c.total -ne @($Tests.identities).Count) { throw 'Contradictory normalized counters.' }
    if ($c.executed -le 0 -or $c.failed -gt 0 -or $c.skipped -gt 0 -or $c.notExecuted -gt 0 -or $Tests.summaryOutcome -cne 'Completed') { throw 'Failed or incomplete required tests.' }
}

function Invoke-GateTool {
    param($Context, $Result, [string[]]$Arguments, [string]$Stem)
    $directory=Get-GateNodeRoot $Context.runRoot $Result.resultId
    $argv=@($Arguments); $executable=$Context.dotnetPath
    if ($Context.control) {
        $executable=(Get-Command powershell -CommandType Application).Source
        $argv=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$Context.control.executor,'-ContextPath',$Context.controlPath,'-ArgumentJson',(ConvertTo-GateJson @($Arguments)))
    }
    $native=Invoke-GateNative $executable $argv $directory $Stem $Context.repoRoot $Context.timeoutSeconds $Context.cancelSignalPath
    $null=Add-GateArtifact $Result $Context.runRoot $native.stdoutPath ($Stem+'-stdout')
    $null=Add-GateArtifact $Result $Context.runRoot $native.stderrPath ($Stem+'-stderr')
    # Every launch, including version/evaluation, retains its own native receipt.
    $native | Add-Member -NotePropertyName toolArguments -NotePropertyValue @($Arguments)
    $Result.provenance.invocations+= $native
    if ($native.reason -or -not $native.exitConfirmed) { throw $(if ($native.reason) { $native.reason } else { 'UnknownWriter' }) }
    if ($native.processExitCode -ne 0) {
        $Result.command=$native.command; $Result.processExitCode=$native.processExitCode
        $Result.times.executionStartedAt=$native.startedAt; $Result.times.executionEndedAt=$native.endedAt
        throw "NativeFailure:${Stem}:$($native.processExitCode)"
    }
    return $native
}

function Get-GateProjectClosure {
    param($Context, $Result, [string]$Project, [string]$Phase)
    $queue=New-Object 'System.Collections.Generic.Queue[string]'; $queue.Enqueue($Project)
    $seen=@{}; $projects=@(); $inputs=@()
    while ($queue.Count) {
        $path=[IO.Path]::GetFullPath($queue.Dequeue())
        if ($seen.ContainsKey($path)) { continue }; $seen[$path]=$true
        if (-not [IO.File]::Exists($path)) { throw 'Evaluated project missing.' }
        $stem='ev-'+$Phase.Substring(0,1)+'-'+$projects.Count
        $args=@('msbuild',$path,'-nologo',('-p:Configuration='+$Context.configuration),'-getProperty:MSBuildProjectFullPath,MSBuildAllProjects,TargetFramework,TargetPath,ProjectAssetsFile,AssemblyName,NuGetPackageRoot','-getItem:Compile,ProjectReference,PackageReference,Analyzer,AdditionalFiles,EmbeddedResource,Content,None,Reference,EditorConfigFiles')
        # The selected TFM is applied only to the root; referenced projects have their own TFM.
        if ($path -ieq $Project) { $args+=('-p:TargetFramework='+$Result.configuration.tfm) }
        $native=Invoke-GateTool $Context $Result $args $stem
        $evaluated=Get-Content -LiteralPath $native.stdoutPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not $evaluated.Properties.TargetFramework -or -not $evaluated.Properties.TargetPath -or -not $evaluated.Properties.ProjectAssetsFile -or $evaluated.Properties.MSBuildProjectFullPath -ine $path) { throw 'Incomplete evaluated project identity.' }
        $properties=$evaluated.Properties
        $settingsArgs=@('msbuild',$path,'-nologo',('-p:Configuration='+$Context.configuration),'-target:_GetRestoreProjectStyle,_GetRestoreSettings','-getProperty:MSBuildProjectFullPath,TargetFramework,TargetFrameworks,_OutputConfigFilePaths,ProjectAssetsFile,MSBuildProjectExtensionsPath,RestoreTaskAssemblyFile,NuGetRestoreTargets')
        $settingsNative=Invoke-GateTool $Context $Result $settingsArgs ($stem+'-settings')
        $settings=(Get-Content -LiteralPath $settingsNative.stdoutPath -Raw -Encoding UTF8 | ConvertFrom-Json).Properties
        if ($settings.MSBuildProjectFullPath -ine $path -or -not $settings.MSBuildProjectExtensionsPath -or $settings.ProjectAssetsFile -ine $properties.ProjectAssetsFile) { throw 'Incomplete restore settings identity.' }
        $frameworks=@($(if ($settings.TargetFrameworks) { $settings.TargetFrameworks } else { $settings.TargetFramework }).Split(';') | Where-Object { $_ } | Sort-Object -Unique)
        if (-not $frameworks.Count -or $properties.TargetFramework -cnotin $frameworks) { throw 'Configured/evaluated restore framework mismatch.' }
        $configPaths=@(([string]$settings._OutputConfigFilePaths).Split(';') | Where-Object { $_ } | ForEach-Object { [IO.Path]::GetFullPath($_) } | Sort-Object -Unique)
        if (-not $configPaths.Count) { throw 'Effective NuGet configuration discovery absent.' }
        $restoreToolInputs=Get-GateRestoreToolInputs ([string]$settings.RestoreTaskAssemblyFile) ([string]$settings.NuGetRestoreTargets)
        $inputs+= $configPaths+$restoreToolInputs
        $extensionRoot=[IO.Path]::GetFullPath($settings.MSBuildProjectExtensionsPath)
        $products=@([IO.Path]::GetFullPath($properties.ProjectAssetsFile),(Join-Path $extensionRoot ((Split-Path $path -Leaf)+'.nuget.g.props')),(Join-Path $extensionRoot ((Split-Path $path -Leaf)+'.nuget.g.targets')))
        $settingsArtifact=@($Result.artifacts | Where-Object { $_.path -ceq ([IO.Path]::GetFileName($settingsNative.stdoutPath)) })
        if ($settingsArtifact.Count -ne 1) { throw 'Restore discovery archive absent.' }
        $projects+=[pscustomobject]@{project=$path; tfm=[string]$properties.TargetFramework; frameworks=$frameworks; configuration=$Context.configuration; targetPath=[IO.Path]::GetFullPath($properties.TargetPath); assetsPath=[IO.Path]::GetFullPath($properties.ProjectAssetsFile); assemblyName=[string]$properties.AssemblyName; packageRoot=[string]$properties.NuGetPackageRoot; configPaths=$configPaths; restoreToolInputs=$restoreToolInputs; restoreOutputs=$products; settingsArtifact=$settingsArtifact[0]; settingsStartedAt=$settingsNative.startedAt}
        $inputs+=$path
        foreach ($import in ([string]$properties.MSBuildAllProjects).Split(';')) { if ($import -and [IO.File]::Exists($import)) { $inputs+=[IO.Path]::GetFullPath($import) } }
        # /pp expands the actual SDK/conditional imports. Hash every existing file named
        # by import boundary comments, rather than inferring imports from extensions.
        $preprocessed=Join-Path (Get-GateNodeRoot $Context.runRoot $Result.resultId) ($stem+'.pp.xml')
        $null=Invoke-GateTool $Context $Result @('msbuild',$path,'-nologo',('-p:Configuration='+$Context.configuration),('-preprocess:'+$preprocessed)) ($stem+'-imports')
        $null=Add-GateArtifact $Result $Context.runRoot $preprocessed 'evaluated-imports'
        $invalidPathCharacters=[IO.Path]::GetInvalidPathChars()
        foreach ($comment in [regex]::Matches([IO.File]::ReadAllText($preprocessed),'(?s)<!--(.*?)-->')) {
            foreach ($line in $comment.Groups[1].Value.Split("`n")) {
                $candidate=$line.Trim()
                # SDK boundary comments also contain XML/prose. On .NET Framework,
                # IsPathRooted throws for those invalid characters; reject them
                # before asking the BCL to interpret an actual existing path.
                if ($candidate -and $candidate.IndexOfAny($invalidPathCharacters) -lt 0 -and [IO.Path]::IsPathRooted($candidate) -and [IO.File]::Exists($candidate)) { $inputs+=[IO.Path]::GetFullPath($candidate) }
            }
        }
        foreach ($itemName in @('Compile','Analyzer','AdditionalFiles','EmbeddedResource','Content','None','Reference','EditorConfigFiles')) {
            foreach ($item in $evaluated.Items.$itemName) {
                $file=[string]$item.FullPath
                if (-not $file -and $item.Identity) { $file=Join-Path (Split-Path $path) $item.Identity }
                if ($file -and [IO.File]::Exists($file)) { $inputs+=[IO.Path]::GetFullPath($file) }
            }
        }
        foreach ($reference in $evaluated.Items.ProjectReference) {
            $next=[string]$reference.FullPath
            if (-not $next) { $next=Join-Path (Split-Path $path) $reference.Identity }
            $queue.Enqueue([IO.Path]::GetFullPath($next))
        }
        $ancestor=Split-Path $path
        while ($ancestor) {
            foreach ($name in @('global.json','NuGet.Config','nuget.config','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','packages.lock.json')) {
                $candidate=Join-Path $ancestor $name
                if ([IO.File]::Exists($candidate)) { $inputs+=$candidate }
            }
            $parent=[IO.Directory]::GetParent($ancestor); $ancestor=if ($parent) { $parent.FullName } else { $null }
        }
    }
    if ($projects[0].tfm -cne $Result.configuration.tfm) { throw 'Configured/evaluated TFM mismatch.' }
    $inputs+=@($Context.implementationInputs)+@($Context.sdkInputPaths)
    return [pscustomobject]@{projects=$projects; inputPaths=@($inputs | Sort-Object -Unique); configPaths=@($projects.configPaths | Sort-Object -Unique); restoreOutputs=@($projects.restoreOutputs | Sort-Object -Unique)}
}

function Get-GateRestoreToolInputs {
    param([string]$TaskAssembly, [string]$RestoreTargets)
    # Use the current SDK's own runtime dependency manifest, not a filename or
    # extension whitelist and not an independent package/configuration resolver.
    if (-not [IO.Path]::IsPathRooted($RestoreTargets) -or -not [IO.File]::Exists($RestoreTargets)) { throw 'SDK restore registration target absent.' }
    Assert-GateNoReparse $RestoreTargets
    # MSBuild resolves UsingTask AssemblyFile relative to the importing target.
    # The SDK returns that actual target via NuGetRestoreTargets, not cwd/search.
    if (-not [IO.Path]::IsPathRooted($TaskAssembly)) { $TaskAssembly=[IO.Path]::GetFullPath((Join-Path (Split-Path $RestoreTargets) $TaskAssembly)) }
    if (-not [IO.File]::Exists($TaskAssembly)) { throw 'SDK restore task assembly absent.' }
    $sdkRoot=Split-Path ([IO.Path]::GetFullPath($TaskAssembly))
    $manifest=Join-Path $sdkRoot 'MSBuild.deps.json'
    $metadata=Get-Content -LiteralPath $manifest -Raw -Encoding UTF8 | ConvertFrom-Json
    $targets=@($metadata.targets.PSObject.Properties)
    if ($targets.Count -ne 1) { throw 'Ambiguous SDK runtime dependency target.' }
    $libraries=$targets[0].Value
    $roots=@($libraries.PSObject.Properties.Name | Where-Object { $_.StartsWith(([IO.Path]::GetFileNameWithoutExtension($TaskAssembly))+'/',[StringComparison]::Ordinal) })
    if ($roots.Count -ne 1) { throw 'SDK task dependency root absent/ambiguous.' }
    $queue=New-Object 'System.Collections.Generic.Queue[string]';$queue.Enqueue($roots[0]);$seen=@{};$inputs=@($TaskAssembly,$manifest,$RestoreTargets)
    while ($queue.Count) {
        $key=$queue.Dequeue();if ($seen.ContainsKey($key)) { continue };$seen[$key]=$true
        $library=$libraries.PSObject.Properties[$key].Value
        if (-not $library) { throw 'SDK dependency declaration absent.' }
        foreach ($runtime in @($library.runtime.PSObject.Properties)) {
            if ($null -eq $runtime) { continue }
            # This locked SDK flattens its declared managed runtime assets beside
            # MSBuild.dll. Reject missing/ambiguous layouts rather than guessing.
            $file=Join-Path $sdkRoot ([IO.Path]::GetFileName($runtime.Name))
            if (-not [IO.File]::Exists($file)) { throw 'Declared SDK restore runtime asset absent.' }
            $inputs+=$file
        }
        foreach ($dependency in @($library.dependencies.PSObject.Properties)) { if ($null -ne $dependency) { $queue.Enqueue($dependency.Name+'/'+$dependency.Value) } }
    }
    return ,@($inputs | Sort-Object -Unique)
}

function Assert-GateRestoreDiscovery {
    param([object[]]$Graph, $Result, $Context)
    if (-not $Graph.Count -or @($Graph.project | Sort-Object -Unique).Count -ne $Graph.Count -or $Graph[0].project -ine $Result.configuration.project -or $Result.configuration.tfm -cnotin $Graph[0].frameworks) { throw 'Restore graph identity/framework contradiction.' }
    foreach ($project in $Graph) {
        if ($project.configuration -cne $Result.configuration.configuration) { throw 'Restore graph Configuration mismatch.' }
        $file=Assert-GateArtifact $project.settingsArtifact $Result $Context.runRoot
        $suffix=[IO.Path]::DirectorySeparatorChar+$Result.resultId.Replace('-','').Substring(0,12)+[IO.Path]::DirectorySeparatorChar+$project.settingsArtifact.path.Replace('/',[IO.Path]::DirectorySeparatorChar)
        $native=@($Result.provenance.invocations | Where-Object { $_.startedAt -ceq $project.settingsStartedAt -and $_.stdoutPath.EndsWith($suffix,[StringComparison]::OrdinalIgnoreCase) })
        $expected=@('msbuild',$project.project,'-nologo',('-p:Configuration='+$Result.configuration.configuration),'-target:_GetRestoreProjectStyle,_GetRestoreSettings','-getProperty:MSBuildProjectFullPath,TargetFramework,TargetFrameworks,_OutputConfigFilePaths,ProjectAssetsFile,MSBuildProjectExtensionsPath,RestoreTaskAssemblyFile,NuGetRestoreTargets')
        if ($native.Count -ne 1 -or $native[0].processExitCode -ne 0 -or -not $native[0].exitConfirmed) { throw 'Restore discovery native receipt absent.' }
        Assert-GateEqual @($native[0].toolArguments) $expected 'restore discovery argv'
        $properties=(Get-Content -LiteralPath $file -Raw -Encoding UTF8 | ConvertFrom-Json).Properties
        $frameworks=@($(if ($properties.TargetFrameworks) { $properties.TargetFrameworks } else { $properties.TargetFramework }).Split(';') | Where-Object { $_ } | Sort-Object -Unique)
        $configs=@(([string]$properties._OutputConfigFilePaths).Split(';') | Where-Object { $_ } | ForEach-Object { [IO.Path]::GetFullPath($_) } | Sort-Object -Unique)
        $extensions=[IO.Path]::GetFullPath($properties.MSBuildProjectExtensionsPath)
        $outputs=@([IO.Path]::GetFullPath($properties.ProjectAssetsFile),(Join-Path $extensions ((Split-Path $project.project -Leaf)+'.nuget.g.props')),(Join-Path $extensions ((Split-Path $project.project -Leaf)+'.nuget.g.targets')))
        if ($properties.MSBuildProjectFullPath -ine $project.project -or $project.assetsPath -ine $properties.ProjectAssetsFile -or $project.tfm -cnotin $frameworks -or -not $configs.Count) { throw 'Restore discovery project/TFM/config contradiction.' }
        Assert-GateEqual @($project.frameworks) $frameworks 'declared restore frameworks'
        Assert-GateEqual @($project.configPaths) $configs 'effective config paths'
        Assert-GateEqual @($project.restoreToolInputs) (Get-GateRestoreToolInputs ([string]$properties.RestoreTaskAssemblyFile) ([string]$properties.NuGetRestoreTargets)) 'SDK restore task dependency inputs'
        Assert-GateEqual @($project.restoreOutputs) $outputs 'SDK restore output locations'
    }
}

function Assert-GateConfigProof {
    param($Entry, [string[]]$ConfigPaths)
    $keys=@($Entry.PSObject.Properties.Name | Sort-Object)
    Assert-GateEqual $keys @('artifact','bytes','path','proofKind','sha256') 'hash-only configuration proof shape'
    if ($Entry.proofKind -cne 'EffectiveNuGetConfigHashOnly' -or $Entry.artifact -ne $null -or $Entry.path -inotin $ConfigPaths -or -not [IO.Path]::IsPathRooted($Entry.path) -or [IO.Path]::GetFullPath($Entry.path) -cne $Entry.path -or $Entry.sha256 -cnotmatch '^[0-9a-f]{64}$' -or ($Entry.bytes -isnot [int] -and $Entry.bytes -isnot [long]) -or $Entry.bytes -le 0) { throw 'Invalid effective configuration hash-only proof.' }
    Assert-GateInputs @($Entry)
}

function Assert-GateRestoreGraph {
    param($Stage, $Result, $Context)
    Assert-GateRestoreDiscovery $Stage.graph $Result $Context
    $configs=@($Stage.graph.configPaths | Sort-Object -Unique)
    foreach ($config in $configs) {
        $entries=@($Stage.inputs | Where-Object { $_.path -ieq $config })
        if ($entries.Count -ne 1) { throw 'Effective config omitted from restore inputs.' }
        Assert-GateConfigProof $entries[0] $configs
    }
    $expectedOutputs=@($Stage.graph.restoreOutputs | Sort-Object -Unique)
    Assert-GateEqual @($Stage.outputs.path | Sort-Object -Unique) $expectedOutputs 'complete SDK restore outputs'
    foreach ($project in $Stage.graph) {
        $entry=@($Stage.outputs | Where-Object { $_.path -ieq $project.assetsPath })
        if ($entry.Count -ne 1) { throw 'Referenced project assets absent.' }
        $assets=Get-Content -LiteralPath (Assert-GateArtifact $entry[0].artifact $Result $Context.runRoot) -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($assets.project.restore.projectPath -ine $project.project -or $assets.project.restore.projectUniqueName -ine $project.project) { throw 'Restore assets project identity mismatch.' }
        Assert-GateEqual @($assets.project.restore.originalTargetFrameworks | Sort-Object -Unique) @($project.frameworks | Sort-Object -Unique) 'restored graph original TFM'
        Assert-GateEqual @($assets.project.frameworks.PSObject.Properties.Name | Sort-Object -Unique) @($project.frameworks | Sort-Object -Unique) 'restored graph frameworks'
        foreach ($tfm in $project.frameworks) { if ($tfm -cnotin @($assets.targets.PSObject.Properties.Name | ForEach-Object { $_.Split('/')[0] } | Sort-Object -Unique)) { throw 'Referenced restored target missing.' } }
        Assert-GateEqual @($assets.project.restore.configFilePaths | ForEach-Object { [IO.Path]::GetFullPath($_) } | Sort-Object -Unique) @($project.configPaths) 'assets effective config discovery'
    }
    foreach ($prior in @($Stage.priorOutputs)) {
        if ($prior.path -inotin $expectedOutputs) { throw 'Undeclared prior restore output.' }
        $null=Assert-GateArtifact $prior.artifact $Result $Context.runRoot
        if ($prior.sha256 -cne $prior.artifact.sha256 -or $prior.bytes -ne $prior.artifact.bytes) { throw 'Prior restore output archive mismatch.' }
    }
}

function Get-GatePackageInputs {
    param([object[]]$Projects)
    $paths=@()
    foreach ($project in $Projects) {
        $assets=Get-Content -LiteralPath $project.assetsPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not $assets.project.restore.projectUniqueName -or [IO.Path]::GetFullPath($assets.project.restore.projectUniqueName) -ine $project.project -or -not $assets.targets.PSObject.Properties.Name.Count) { throw 'Assets project/target identity mismatch.' }
        foreach ($library in $assets.libraries.PSObject.Properties) {
            if ($library.Value.type -ceq 'package') {
                $found=$false
                foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
                    $root=Join-Path $folder $library.Value.path
                    if ([IO.Directory]::Exists($root)) {
                        foreach ($file in $library.Value.files) {
                            $candidate=Join-Path $root $file
                            if (-not [IO.File]::Exists($candidate)) { throw 'Missing resolved package/tool input.' }
                            $paths+=[IO.Path]::GetFullPath($candidate)
                        }
                        $found=$true; break
                    }
                }
                if (-not $found) { throw 'Resolved package root absent.' }
            }
        }
    }
    return ,@($paths | Sort-Object -Unique)
}

function New-GateStage {
    param([string]$Name, $Native, [object[]]$Inputs, [object[]]$Outputs, $Result, [object[]]$Graph=@(), [object[]]$PriorOutputs=@())
    return [pscustomobject][ordered]@{name=$Name; mode='Executed'; runId=$Result.runId; resultId=$Result.resultId; command=$Native.command; startedAt=$Native.startedAt; endedAt=$Native.endedAt; processExitCode=$Native.processExitCode; inputs=@($Inputs); inputFingerprint=Get-GateFingerprint $Inputs; outputs=@($Outputs); outputFingerprint=Get-GateFingerprint $Outputs; graph=@($Graph); priorOutputs=@($PriorOutputs); reuse=$null}
}

function Assert-GateStage {
    param($Stage, $Result, $Context, [string]$Name, [switch]$RestoreOnly)
    if ($Stage.name -cne $Name -or $Stage.mode -cnotin @('Executed','Reused')) { throw 'Invalid stage declaration.' }
    if ($Stage.mode -ceq 'Reused') {
        if ($Stage.command -ne $null -or $Stage.startedAt -ne $null -or $Stage.endedAt -ne $null -or $Stage.processExitCode -ne $null -or -not $Stage.reuse) { throw 'Reused stage claims new execution.' }
        $receipt=$Stage.reuse.receipt
        if ($receipt.mode -cne 'Executed' -or -not $Stage.reuse.manifestArtifact -or -not $Stage.reuse.originalRunId -or -not $Stage.reuse.originalResultId) { throw 'Invalid reuse origin.' }
        $path=Assert-GateArtifact $Stage.reuse.manifestArtifact $Result $Context.runRoot
        $prior=Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
        Assert-GateEqual $receipt $prior.$Name 'archived reuse stage'
        if ($receipt.runId -cne $Stage.reuse.originalRunId -or $receipt.resultId -cne $Stage.reuse.originalResultId) { throw 'Reuse producer contradiction.' }
        # Inputs/outputs are re-archived under the current node; original receipt retains original IDs.
        if ($Name -ceq 'build') { Assert-GateProducerMembership $Result $Context }
        if ($receipt.inputFingerprint -cne $Stage.inputFingerprint -or ($Name -cne 'build' -and $receipt.outputFingerprint -cne $Stage.outputFingerprint)) { throw 'Reuse fingerprint mismatch.' }
    } else { $receipt=$Stage }
    if ($receipt.processExitCode -ne 0 -or -not $receipt.startedAt -or -not $receipt.endedAt -or [DateTimeOffset]$receipt.endedAt -lt [DateTimeOffset]$receipt.startedAt) { throw 'Failed/invalid stage receipt.' }
    if (-not $receipt.command -or -not $receipt.command.arguments.Count) { throw 'Stage argv absent.' }
    $argv=@($receipt.command.arguments)
    if ($Context.control) {
        $pos=[Array]::IndexOf($argv,'-ArgumentJson')
        if ($pos -lt 0) { throw 'Control invocation missing native args.' }
        $argv=[string[]]($argv[$pos+1] | ConvertFrom-Json)
    }
    if ($argv[0] -cne $Name -or $argv[1] -ine $Result.configuration.project) { throw 'Stage command project mismatch.' }
    if ($Name -ceq 'restore') {
        $values=@($argv | Where-Object { $_.StartsWith('-p:Configuration=',[StringComparison]::OrdinalIgnoreCase) })
        if ($values.Count -ne 1 -or $values[0] -cne '-p:Configuration='+$Result.configuration.configuration) { throw 'Restore Configuration argv mismatch.' }
        if (@($argv | Where-Object { $_ -match '(?i)TargetFramework|^/(p|property):|^--(configuration|framework)$|^-[cf]$' }).Count) { throw 'Restore must preserve per-project frameworks; global TFM forbidden.' }
    }
    if ($Name -cne 'restore') {
        $index=[Array]::IndexOf($argv,'-c')
        $tfmIndex=[Array]::IndexOf($argv,'-f')
        if ($index -lt 0 -or @($argv | Where-Object { $_ -ceq '-c' }).Count -ne 1 -or $argv[$index+1] -cne $Result.configuration.configuration -or '--no-restore' -cnotin $argv) { throw 'Stage configuration/restore mismatch.' }
        if ($tfmIndex -lt 0 -or @($argv | Where-Object { $_ -ceq '-f' }).Count -ne 1 -or $argv[$tfmIndex+1] -cne $Result.configuration.tfm) { throw 'Stage TFM argv mismatch.' }
        if (@($argv | Where-Object { $_ -match '^(-p:|/p:|/property:|-property:)(Configuration|TargetFramework)=|^--(configuration|framework)$' }).Count) { throw 'Ambiguous stage dimension argv.' }
    }
    if ($Name -ceq 'test') {
        if ('--no-build' -cnotin $argv) { throw 'Implicit test build forbidden.' }
        $index=[Array]::IndexOf($argv,'--filter')
        if (@($argv | Where-Object { $_ -ceq '--filter' }).Count -gt 1) { throw 'Ambiguous test filter argv.' }
        $filter=if ($index -ge 0) { [string]$argv[$index+1] } else { '' }
        if ($filter -cne $Result.configuration.filter) { throw 'Test filter command mismatch.' }
    }
    if ($Stage.mode -ceq 'Executed' -and ($Stage.runId -cne $Result.runId -or $Stage.resultId -cne $Result.resultId)) { throw 'Stage stale owner.' }
    if ($Stage.mode -ceq 'Executed' -and $Result.provenance.invocations) {
        $matches=@($Result.provenance.invocations | Where-Object { $_.startedAt -ceq $Stage.startedAt -and $_.endedAt -ceq $Stage.endedAt -and (ConvertTo-GateJson $_.command) -ceq (ConvertTo-GateJson $Stage.command) })
        if ($matches.Count -ne 1 -or $matches[0].processExitCode -ne 0 -or -not $matches[0].exitConfirmed -or $matches[0].reason) { throw 'Stage/native receipt mismatch.' }
        Assert-GateEqual @($matches[0].toolArguments) @($argv) 'stage/native tool argv'
    }
    foreach ($set in @('inputs','outputs')) {
        $entries=@($Stage.$set)
        if (-not $entries.Count) { throw 'Empty stage input/output closure.' }
        if ($set -ceq 'outputs' -and @($entries | Where-Object { $_.bytes -le 0 }).Count) { throw 'Empty stage output.' }
        $seen=@{}
        foreach ($entry in $entries) {
            if ($seen.ContainsKey($entry.path)) { throw 'Duplicate stage path.' }; $seen[$entry.path]=$true
            $configs=@($Result.provenance.closure.configPaths | Sort-Object -Unique)
            if ($entry.proofKind) {
                if ($set -cne 'inputs') { throw 'Hash-only output forbidden.' }
                Assert-GateConfigProof $entry $configs
            } else {
                if ($entry.path -iin $configs) { throw 'Effective config must use hash-only proof.' }
                $null=Assert-GateArtifact $entry.artifact $Result $Context.runRoot
                if ($entry.sha256 -cne $entry.artifact.sha256 -or $entry.bytes -ne $entry.artifact.bytes) { throw 'Stage archive contradiction.' }
            }
        }
        if ((Get-GateFingerprint $entries) -cne $Stage.($set.Substring(0,$set.Length-1)+'Fingerprint')) { throw 'Forged stage fingerprint.' }
    }
    if ($Name -ceq 'restore') { Assert-GateRestoreGraph $Stage $Result $Context }
    if ($Name -ceq 'build') {
        foreach ($config in @($Result.provenance.closure.configPaths | Sort-Object -Unique)) {
            $entry=@($Stage.inputs | Where-Object { $_.path -ieq $config })
            if ($entry.Count -ne 1) { throw 'Effective config omitted from build inputs.' }
            Assert-GateConfigProof $entry[0] @($Result.provenance.closure.configPaths)
        }
    }

}

function Assert-GateBinaryIdentitySet {
    param([object[]]$Expected, [object[]]$Actual, $Result, $Context, [string]$Label)
    # Archive paths/roles and enumeration order can differ across stages. The
    # complete consumed binary identities cannot: Windows path + length + hash.
    $sets=@()
    foreach ($entries in @(@{entries=$Expected},@{entries=$Actual})) {
        if (-not $entries.entries.Count) { throw "Empty binary identity set: $Label" }
        $identities=New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $entries.entries) {
            if (-not [IO.Path]::IsPathRooted($entry.path) -or $entry.sha256 -cnotmatch '^[0-9a-f]{64}$' -or ($entry.bytes -isnot [int] -and $entry.bytes -isnot [long]) -or $entry.bytes -le 0) { throw "Invalid binary identity: $Label" }
            $path=[IO.Path]::GetFullPath($entry.path)
            if ($identities.ContainsKey($path)) { throw "Duplicate binary identity: $Label" }
            $null=Assert-GateArtifact $entry.artifact $Result $Context.runRoot
            if ($entry.sha256 -cne $entry.artifact.sha256 -or $entry.bytes -ne $entry.artifact.bytes) { throw "Binary archive contradiction: $Label" }
            $identities.Add($path,$entry)
        }
        $sets+=,$identities
    }
    if ($sets[0].Count -ne $sets[1].Count) { throw "Binary identity membership mismatch: $Label" }
    foreach ($path in $sets[0].Keys) {
        if (-not $sets[1].ContainsKey($path) -or $sets[0][$path].bytes -ne $sets[1][$path].bytes -or $sets[0][$path].sha256 -cne $sets[1][$path].sha256) { throw "Binary identity mismatch: $Label" }
    }
}

function Save-GateProducerReceipt {
    param($Result, $Context)
    # Capture the producer's complete ledger before validating any candidate.
    # This trusted invocation map is deliberately outside serialized results.
    if (-not $Context.producerReceipts) { $Context | Add-Member -NotePropertyName producerReceipts -NotePropertyValue @{} -Force }
    if ($Context.producerReceipts.ContainsKey($Result.resultId)) { return }
    $path=Join-Path (Get-GateNodeRoot $Context.runRoot $Result.resultId) 'stage-receipt.json'
    [IO.File]::WriteAllText($path,(ConvertTo-GateJson $Result.provenance),[Text.UTF8Encoding]::new($false))
    $artifact=Add-GateArtifact $Result $Context.runRoot $path 'stage-receipt'
    $Context.producerReceipts[$Result.resultId]=[pscustomobject]@{
        artifact=(ConvertTo-GateJson $artifact | ConvertFrom-Json)
        sourceSha=$Result.provenance.source.sha
        sourceFingerprint=$Result.provenance.source.inputFingerprint
    }
}

function Assert-GateProducerMembership {
    param($Result, $Context)
    if (-not $Context.producerReceipts -or -not $Context.producerReceipts.ContainsKey($Result.resultId)) { throw 'Independent producer receipt missing.' }
    $proof=$Context.producerReceipts[$Result.resultId]
    if ($proof.artifact.path -cne 'stage-receipt.json' -or $proof.artifact.role -cne 'stage-receipt') { throw 'Independent producer receipt reference mismatch.' }
    $path=Assert-GateArtifact $proof.artifact $Result $Context.runRoot
    $references=@($Result.artifacts | Where-Object { $_.path -ceq 'stage-receipt.json' -or $_.role -ceq 'stage-receipt' })
    if ($references.Count -ne 1) { throw 'Independent producer receipt reference missing/ambiguous.' }
    Assert-GateEqual $references[0] $proof.artifact 'trusted producer receipt reference'
    $producer=Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    $p=$Result.provenance
    if ($producer.schemaVersion -ne 1 -or ($producer.example -and -not $Context.allowExample) -or $producer.project -ine $Result.configuration.project -or $producer.tfm -cne $Result.configuration.tfm -or $producer.configuration -cne $Result.configuration.configuration -or $producer.configSha256 -cne $Result.configuration.configSha256 -or $producer.sdk -cne $p.sdk) { throw 'Independent producer configuration mismatch.' }
    if (-not $proof.sourceSha -or -not $proof.sourceFingerprint -or $producer.source.sha -cne $proof.sourceSha -or $producer.source.inputFingerprint -cne $proof.sourceFingerprint -or $p.source.sha -cne $proof.sourceSha -or $p.source.inputFingerprint -cne $proof.sourceFingerprint) { throw 'Independent producer source mismatch.' }
    $build=$producer.build
    if (-not $build -or $build.runId -cne $Result.runId -or $build.resultId -cne $Result.resultId -or $p.build.runId -cne $build.runId -or $p.build.resultId -cne $build.resultId -or $build.mode -cne $p.build.mode) { throw 'Independent producer run/result/mode mismatch.' }
    # A reused build's independently recorded origin is also fixed; candidates
    # cannot choose another owned, valid receipt with a smaller output set.
    if ($build.mode -ceq 'Reused') { Assert-GateEqual $p.build.reuse $build.reuse 'trusted reused producer origin' }
    Assert-GateBinaryIdentitySet $build.outputs $p.build.outputs $Result $Context 'independent producer -> build.outputs'
    Assert-GateEqual $p.compilerEvidence $producer.compilerEvidence 'trusted compiler evidence source'
}

function Assert-GateProvenance {
    param($Result, $Context)
    $p=$Result.provenance
    if (-not $p -or $p.schemaVersion -ne 1 -or ($p.example -and -not $Context.allowExample)) { throw 'Missing/synthetic production provenance.' }
    foreach ($field in @('project','tfm','configuration','configSha256')) { if ($p.$field -cne $Result.configuration.$field) { throw "Provenance identity mismatch: $field" } }
    if (-not $p.sdk -or $p.sdk -cne $Result.tools[0].version -or $p.source.sha -cne $Result.source.before.sha -or $p.source.inputFingerprint -cne $Result.source.before.inputFingerprint) { throw 'Provenance source/SDK mismatch.' }
    if ($Result.source.before.sha -cne $Result.source.after.sha -or $Result.source.before.inputFingerprint -cne $Result.source.after.inputFingerprint) { throw 'Source changed during run.' }
    if (-not $Result.source.before.inputs.Count) { throw 'Empty evaluated source closure.' }
    foreach ($source in @($Result.source.before,$Result.source.after)) {
        if ((Get-GateFingerprint $source.inputs) -cne $source.inputFingerprint) { throw 'Forged source fingerprint.' }
    }
    if ($p.closure.Count -eq 0 -or $p.closure[0].project -ine $p.project -or $p.closure[0].tfm -cne $p.tfm -or $p.closure[0].targetPath -ine $p.assembly -or @($p.closure.project | Sort-Object -Unique).Count -ne $p.closure.Count) { throw 'Evaluated project closure contradiction.' }
    $toolPath=if ($Context.control) { $Context.control.executor } else { $Context.dotnetPath }
    if (-not $toolPath -or $Result.tools[0].sha256 -cne (Get-GateHash $toolPath)) { throw 'Tool executable changed.' }
    $nativePath=if ($Context.control) { (Get-Command powershell -CommandType Application).Source } else { $Context.dotnetPath }
    foreach ($invocation in $p.invocations) {
        if ($invocation.command.executable -ine $nativePath) { throw 'Native executable identity mismatch.' }
        foreach ($stream in @('stdoutPath','stderrPath')) {
            $path=[IO.Path]::GetFullPath($invocation.$stream)
            $prefix=[IO.Path]::GetFullPath((Get-GateNodeRoot $Context.runRoot $Result.resultId)).TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
            if (-not $path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Native stream escapes node ownership.' }
            $relative=$path.Substring($prefix.Length).Replace('\','/')
            $owned=@($Result.artifacts | Where-Object { $_.path -ceq $relative })
            if ($owned.Count -ne 1) { throw 'Native stream lacks owned archive binding.' }
            $null=Assert-GateArtifact $owned[0] $Result $Context.runRoot
        }
    }
    $sdkReceipt=@($p.invocations | Where-Object { $_.toolArguments.Count -eq 1 -and $_.toolArguments[0] -ceq '--version' })
    if ($sdkReceipt.Count -ne 1 -or $sdkReceipt[0].processExitCode -ne 0 -or ([IO.File]::ReadAllText($sdkReceipt[0].stdoutPath)).Trim() -cne $p.sdk) { throw 'SDK receipt mismatch.' }
    Assert-GateRestoreDiscovery $p.closure $Result $Context
    if (-not $p.sourceBeforeRestore -or @($p.sourceBeforeRestore.inputs.path | Sort-Object -Unique).Count -ne $p.sourceBeforeRestore.inputs.Count -or $p.sourceBeforeRestore.sha -cne $Result.source.before.sha -or (Get-GateFingerprint $p.sourceBeforeRestore.inputs) -cne $p.sourceBeforeRestore.inputFingerprint) { throw 'Pre-restore source proof absent/forged.' }
    Assert-GateInputs $p.sourceBeforeRestore.inputs
    foreach ($entry in $p.sourceBeforeRestore.inputs) {
        $current=@($Result.source.before.inputs | Where-Object { $_.path -ieq $entry.path })
        if ($current.Count -ne 1 -or $current[0].sha256 -cne $entry.sha256 -or $current[0].bytes -ne $entry.bytes) { throw 'Authored/SDK/config source changed across restore.' }
    }
    foreach ($config in @($p.closure.configPaths | Sort-Object -Unique)) {
        foreach ($source in @($p.sourceBeforeRestore,$Result.source.before,$Result.source.after)) { if (@($source.inputs | Where-Object { $_.path -ieq $config }).Count -ne 1) { throw 'Effective config omitted from source proof.' } }
    }
    Assert-GateInputs $Result.source.after.inputs
    Assert-GateStage $p.restore $Result $Context 'restore'
    Assert-GateStage $p.build $Result $Context 'build'
    Assert-GateProducerMembership $Result $Context
    $terminal=if ($Result.kind -ceq 'dotnet-test') { $p.test } else { $p.build }
    if (-not $terminal -or $terminal.mode -cne 'Executed') { throw 'Terminal stage was not executed.' }
    Assert-GateEqual $Result.command $terminal.command 'terminal summary command'
    if ($Result.processExitCode -ne $terminal.processExitCode -or $Result.times.executionStartedAt -cne $terminal.startedAt -or $Result.times.executionEndedAt -cne $terminal.endedAt) { throw 'Terminal summary native/time mismatch.' }
    if ($p.restore.mode -ceq 'Executed' -and $p.build.mode -ceq 'Executed' -and [DateTimeOffset]$p.build.startedAt -lt [DateTimeOffset]$p.restore.endedAt) { throw 'Restore/build order contradiction.' }
    Assert-GateInputs $p.restore.outputs
    Assert-GateInputs $p.build.outputs
    Assert-GateCompilerEvidence $Result $Context
    Assert-GateInputs $p.compilerInputs
    $assembly=@($p.build.outputs | Where-Object { [IO.Path]::GetFullPath($_.path) -ieq [IO.Path]::GetFullPath($p.assembly) })
    if ($assembly.Count -ne 1) { throw 'Build target binary absent.' }
    if ($Result.kind -ceq 'dotnet-test') {
        Assert-GateStage $p.test $Result $Context 'test'
        if ($p.build.mode -ceq 'Executed' -and [DateTimeOffset]$p.test.startedAt -lt [DateTimeOffset]$p.build.endedAt) { throw 'Build/test order contradiction.' }
        Assert-GateBinaryIdentitySet $p.build.outputs $p.loadedBefore $Result $Context 'build.outputs -> loadedBefore'
        Assert-GateBinaryIdentitySet $p.loadedBefore $p.loadedAfter $Result $Context 'loadedBefore -> loadedAfter'
        Assert-GateBinaryIdentitySet $p.loadedBefore $p.test.inputs $Result $Context 'loadedBefore -> test.inputs'
        Assert-GateInputs $p.loadedAfter
        $trx=@($Result.artifacts | Where-Object { $_.role -ceq 'trx' })
        if ($trx.Count -ne 1) { throw 'TRX artifact missing/duplicate.' }
        $path=Assert-GateArtifact $trx[0] $Result $Context.runRoot
        $tests=Get-GateTrxTests $path $p.assembly $Result.configuration.filter $p.test.startedAt $p.test.endedAt
        Assert-GateEqual $Result.tests $tests 'TRX normalized evidence'
        Assert-GateTests $tests
        if ($p.trxBinding.runId -cne $Result.runId -or $p.trxBinding.resultId -cne $Result.resultId -or $p.trxBinding.sourceSha -cne $Result.source.before.sha -or $p.trxBinding.sha256 -cne $trx[0].sha256 -or $p.trxBinding.filter -cne $Result.configuration.filter) { throw 'Old-run/SHA/filter TRX binding.' }
    } elseif ($Result.tests -ne $null -or $p.test -ne $null) { throw 'Build claims tests.' }
}

function Assert-GateResult {
    param($Result, $Plan, $Context)
    # Normalize both Hashtable and JSON/PSCustomObject through the same boundary.
    $root=ConvertTo-GateJson $Result | ConvertFrom-Json
    $expected=ConvertTo-GateJson $Plan | ConvertFrom-Json
    $seen=@{}; $leaves=@{}
    function Visit-GateResult($node,$planNode) {
        foreach ($field in @('schemaVersion','example','runId','resultId','parentResultId','invocationPath','name','kind','declaration','configuration')) { Assert-GateEqual $node.$field $planNode.$field "assigned $field" }
        if ($node.example -and -not $Context.allowExample) { throw 'Synthetic production result.' }
        $id=[guid]::Empty
        if (-not [guid]::TryParse($node.resultId,[ref]$id) -or $seen.ContainsKey($node.resultId)) { throw 'Duplicate/invalid result ID.' }; $seen[$node.resultId]=$true
        $index=[Array]::IndexOf($script:GateStates,[string]$node.status)
        if ($index -lt 0 -or $node.cliExitCode -ne $index) { throw 'Noncanonical status/CLI.' }
        if ($node.children.Count -ne $planNode.children.Count) { throw 'Incomplete result tree.' }
        $required=@($planNode.declaration.required); $optional=@($planNode.declaration.optional)
        if (@($required+$optional | Sort-Object -Unique).Count -ne ($required.Count+$optional.Count)) { throw 'Overlapping/duplicate coverage declaration.' }
        Assert-GateEqual @($node.coverage.required) $required 'required coverage'
        Assert-GateEqual @($node.coverage.optional) $optional 'optional coverage'
        for ($i=0; $i -lt $node.children.Count; $i++) { Visit-GateResult $node.children[$i] $planNode.children[$i] }
        if ($node.kind -cne 'gate') {
            if ($node.children.Count -ne 0 -or $node.fullGateAccepted) { throw 'Leaf claims children/full acceptance.' }
            if ($node.status -ceq 'Passed') {
                if ($node.source.before.sha -cne $expected.source.before.sha) { throw 'Leaf source SHA differs from parent-assigned source.' }
                if ($node.processExitCode -ne 0 -or -not $node.command -or -not $node.times.executionStartedAt -or -not $node.times.executionEndedAt -or -not $node.artifacts.Count -or -not $node.tools.Count) { throw 'Passed leaf lacks execution evidence.' }
                Assert-GateProvenance $node $Context
                $leaves[$node.resultId]=$node
            }
            if ($node.status -ceq 'NotRun' -and ($node.command -ne $null -or $node.processExitCode -ne $null -or $node.times.executionStartedAt -ne $null -or $node.times.executionEndedAt -ne $null)) { throw 'Uninvoked leaf has execution metadata.' }
            if ($node.status -ceq 'Skipped' -and ($planNode.declaration.skipPolicy -cne 'MissingTool' -or $node.reason -cne 'MissingTool' -or $required.Count -gt 0 -or $node.processExitCode -ne $null)) { throw 'Undeclared/invalid optional skip.' }
        }
        $derived=Get-GateDerivedCoverage $node
        Assert-GateEqual $node.coverage $derived 'derived coverage/bindings'
        foreach ($token in $node.coverage.completed) {
            $bindings=@($node.coverage.bindings.PSObject.Properties[$token].Value)
            if (-not $bindings.Count -or @($bindings | Sort-Object -Unique).Count -ne $bindings.Count) { throw 'Empty/duplicate bindings.' }
            foreach ($binding in $bindings) {
                if (-not $leaves.ContainsKey([string]$binding) -or $token -cnotin @($leaves[$binding].declaration.required+$leaves[$binding].declaration.optional)) { throw 'Forged descendant binding.' }
            }
        }
        if ($node.kind -ceq 'gate') {
            $status=Get-GateAggregateStatus $node
            if ($node.resultId -ceq $root.resultId -and $Context.abortStatus) { $status=$Context.abortStatus }
            if ($node.status -cne $status -or $node.fullGateAccepted -ne ($status -ceq 'Passed')) { throw 'Forged aggregate status/acceptance.' }
            if ($node.processExitCode -ne $null -or $node.command -ne $null) { throw 'Aggregate claims native execution.' }
        }
        $artifactPaths=@{}
        foreach ($artifact in $node.artifacts) {
            if ($artifactPaths.ContainsKey($artifact.path)) { throw 'Duplicate artifact.' }; $artifactPaths[$artifact.path]=$true
            $null=Assert-GateArtifact $artifact $node $Context.runRoot
        }
    }
    Visit-GateResult $root $expected
    return $root
}

function Copy-GateCompilerEvidence {
    param($Prior,$OldResult,[string]$PriorRoot,$Result,$Context)
    if (-not $Prior.compilerEvidence -or -not $Prior.compilerInputs.Count) { throw 'Reuse compiler evidence missing.' }
    Assert-GateCompilerEvidence $OldResult ([pscustomobject]@{runRoot=$PriorRoot})
    $copy=ConvertTo-GateJson $Prior.compilerEvidence | ConvertFrom-Json
    $copy.mode='Reused'; $copy.runId=$Result.runId; $copy.resultId=$Result.resultId
    $copy.origin=[pscustomobject]@{runId=$Prior.compilerEvidence.runId;resultId=$Prior.compilerEvidence.resultId;invocationId=$Prior.compilerEvidence.invocationId;aggregateSha256=$Prior.compilerEvidence.aggregateArtifact.sha256}
    function Copy-CompilerArtifact($artifact,[string]$label) {
        $source=Assert-GateArtifact $artifact $OldResult $PriorRoot
        $extension=[IO.Path]::GetExtension($source); if (-not $extension) { $extension='.bin' }
        $destination=Join-Path (Get-GateNodeRoot $Context.runRoot $Result.resultId) ('reuse-compiler-'+$Result.artifacts.Count+$extension)
        Copy-Item -LiteralPath $source -Destination $destination
        return Add-GateArtifact $Result $Context.runRoot $destination $label
    }
    $copy.targetArtifact=Copy-CompilerArtifact $Prior.compilerEvidence.targetArtifact 'reused-compiler-target'
    $copy.eventLogArtifact=Copy-CompilerArtifact $Prior.compilerEvidence.eventLogArtifact 'reused-compiler-event-log'
    $copy.eventManifestArtifact=Copy-CompilerArtifact $Prior.compilerEvidence.eventManifestArtifact 'reused-compiler-event-manifest'
    $reader=@(); foreach ($artifact in @($Prior.compilerEvidence.readerArtifacts)) { $reader+=Copy-CompilerArtifact $artifact 'reused-compiler-reader' }; $copy.readerArtifacts=$reader
    $captures=@(); foreach ($artifact in @($Prior.compilerEvidence.captureArtifacts)) { $captures+=Copy-CompilerArtifact $artifact 'reused-compiler-capture' }; $copy.captureArtifacts=$captures
    $copy.aggregateArtifact=$null
    $aggregatePath=Join-Path (Get-GateNodeRoot $Context.runRoot $Result.resultId) ('reused-compiler-evidence-'+$Result.artifacts.Count+'.json')
    [IO.File]::WriteAllText($aggregatePath,(ConvertTo-GateJson $copy),[Text.UTF8Encoding]::new($false))
    $copy.aggregateArtifact=Add-GateArtifact $Result $Context.runRoot $aggregatePath 'reused-compiler-evidence'
    return $copy
}

function Use-GateReuse {
    param($Context, $Result, [string]$ManifestPath, [string]$Sdk, [bool]$NeedBuild)
    if (-not $ManifestPath -or -not [IO.File]::Exists($ManifestPath)) { throw 'Blocked:MissingReuseManifest' }
    Assert-GateNoReparse $ManifestPath
    $index=Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($index.schemaVersion -ne 1 -or ($index.example -and -not $Context.allowExample)) { throw 'Invalid/synthetic reuse index.' }
    $matches=@($index.receipts | Where-Object { $_.project -ieq $Result.configuration.project -and $_.tfm -ceq $Result.configuration.tfm -and $_.configuration -ceq $Context.configuration })
    if ($matches.Count -eq 0) {
        if (@($index.receipts | Where-Object { $_.project -ieq $Result.configuration.project }).Count) { throw 'Reuse index TFM/configuration mismatch.' }
        throw 'Blocked:MissingProjectReuseReceipt'
    }
    if ($matches.Count -ne 1) { throw 'Ambiguous reuse receipt.' }
    $entry=$matches[0]; $priorRoot=Split-Path ([IO.Path]::GetFullPath($ManifestPath))
    $path=Resolve-GateArtifact $priorRoot $entry.path
    if (-not [IO.File]::Exists($path)) { throw 'Missing indexed reuse evidence.' }
    if ((Get-GateHash $path) -cne $entry.sha256 -or (Get-Item -LiteralPath $path).Length -ne $entry.bytes) { throw 'Corrupt reuse receipt.' }
    $prior=Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($prior.schemaVersion -ne 1 -or ($prior.example -and -not $Context.allowExample) -or $prior.sdk -cne $Sdk -or $prior.source.sha -cne $Result.source.before.sha -or $prior.configSha256 -cne $Result.configuration.configSha256 -or $prior.project -ine $Result.configuration.project -or $prior.tfm -cne $Result.configuration.tfm -or $prior.configuration -cne $Context.configuration) { throw 'Reuse source/config/TFM/SDK mismatch.' }
    Assert-GateRestoreDiscovery $Result.provenance.closure $Result $Context
    Assert-GateInputs $prior.source.inputs
    if ($prior.source.inputFingerprint -cne (Get-GateFingerprint $prior.source.inputs)) { throw 'Corrupt reuse input fingerprint.' }
    # Validate original receipts against their actual old-owned archive, then make
    # fresh copies. NoRestore requires only restore; a failed prior build is allowed.
    $oldResult=ConvertTo-GateJson $Result | ConvertFrom-Json
    $oldResult.runId=$entry.runId; $oldResult.resultId=$entry.resultId
    $oldResult.provenance=$prior
    $oldContext=[pscustomobject]@{runRoot=$priorRoot; control=$Context.control}
    $manifestCopy=Join-Path (Get-GateNodeRoot $Context.runRoot $Result.resultId) ('reuse-'+$Result.artifacts.Count+'.json')
    Copy-Item -LiteralPath $path -Destination $manifestCopy
    $manifestArtifact=Add-GateArtifact $Result $Context.runRoot $manifestCopy 'reuse-manifest'
    $stages=@('restore'); if ($NeedBuild) { $stages+= 'build' }
    $reused=[ordered]@{}
    foreach ($name in $stages) {
        $oldStage=$prior.$name
        if (-not $oldStage -or $oldStage.mode -cne 'Executed') { throw 'Reuse requires an original executed receipt.' }
        Assert-GateStage $oldStage $oldResult $oldContext $name
        Assert-GateInputs $oldStage.inputs; Assert-GateInputs $oldStage.outputs
        $inputs=Save-GateFiles $Result $Context.runRoot @($oldStage.inputs.path) ('reuse-'+$name+'-inputs') @($Result.provenance.closure.configPaths)
        $outputs=Save-GateFiles $Result $Context.runRoot @($oldStage.outputs.path) ('reuse-'+$name+'-outputs')
        $reused[$name]=[pscustomobject]@{name=$name; mode='Reused'; runId=$Result.runId; resultId=$Result.resultId; command=$null; startedAt=$null; endedAt=$null; processExitCode=$null; inputs=$inputs; inputFingerprint=Get-GateFingerprint $inputs; outputs=$outputs; outputFingerprint=Get-GateFingerprint $outputs; graph=@($Result.provenance.closure); priorOutputs=@(); reuse=[pscustomobject]@{manifestArtifact=$manifestArtifact; originalRunId=$oldStage.runId; originalResultId=$oldStage.resultId; receipt=$oldStage}}
    }
    if ($NeedBuild) {
        Assert-GateInputs $prior.compilerInputs
        $reused.compilerInputs=Save-GateFiles $Result $Context.runRoot @($prior.compilerInputs.path) 'compiler-input' @($Result.provenance.closure.configPaths)
        $reused.compilerEvidence=Copy-GateCompilerEvidence $prior $oldResult $priorRoot $Result $Context
    }
    return [pscustomobject]$reused
}

function Save-GateCompilerTarget {
    param([string]$Path)
    $text=@'
<Project>
  <UsingTask TaskName="AbilityKitSetCompilerInputLengths" TaskFactory="RoslynCodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll">
    <ParameterGroup><Files ParameterType="Microsoft.Build.Framework.ITaskItem[]" Required="true" /><Items ParameterType="Microsoft.Build.Framework.ITaskItem[]" Output="true" /></ParameterGroup>
    <Task><Using Namespace="System.IO" /><Code Type="Fragment" Language="cs"><![CDATA[
      foreach (var file in Files) { file.SetMetadata("AbilityKitByteLength", new FileInfo(file.ItemSpec).Length.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
      Items = Files;
    ]]></Code></Task>
  </UsingTask>
  <Target Name="AbilityKitCaptureCompilerInputs" BeforeTargets="CoreCompile" DependsOnTargets="AddGlobalAnalyzerConfigForPackage_MicrosoftCodeAnalysisNetAnalyzers;AddGlobalAnalyzerConfigForPackage_MicrosoftCodeAnalysisCSharpCodeStyle;GenerateMSBuildEditorConfigFile" Condition="'$(AbilityKitCompilerCaptureDisabled)' != 'true'">
    <PropertyGroup>
      <AbilityKitCompilerCaptureId>$([System.Guid]::NewGuid().ToString('D'))</AbilityKitCompilerCaptureId>
      <AbilityKitCompilerCaptureTemp>$(AbilityKitCompilerCaptureRoot)\pending\$(AbilityKitCompilerCaptureId).tmp</AbilityKitCompilerCaptureTemp>
      <AbilityKitCompilerCaptureComplete>$(AbilityKitCompilerCaptureRoot)\completed\$(AbilityKitCompilerCaptureId).complete</AbilityKitCompilerCaptureComplete>
    </PropertyGroup>
    <Error Condition="'$(AbilityKitCompilerCaptureRoot)' == '' Or '$(AbilityKitCompilerRunId)' == '' Or '$(AbilityKitCompilerResultId)' == '' Or '$(AbilityKitCompilerInvocationId)' == ''" Text="Compiler capture ownership is incomplete." />
    <Error Condition="Exists('$(AbilityKitCompilerCaptureTemp)') Or Exists('$(AbilityKitCompilerCaptureComplete)')" Text="Compiler capture collision." />
    <Exec Condition="'$(AbilityKitCompilerBarrierScript)' != ''" Command="powershell -NoProfile -ExecutionPolicy Bypass -File &quot;$(AbilityKitCompilerBarrierScript)&quot; -Root &quot;$(AbilityKitCompilerBarrierRoot)&quot; -CaptureId &quot;$(AbilityKitCompilerCaptureId)&quot; -Project &quot;$(MSBuildProjectName)&quot; -Mode &quot;$(AbilityKitCompilerBarrierMode)&quot;" />
    <ItemGroup>
      <AbilityKitCompilerInput Include="$(MSBuildProjectFullPath);$(MSBuildAllProjects);@(Compile->'%(FullPath)');@(ReferencePathWithRefAssemblies->'%(FullPath)');@(ReferencePath->'%(FullPath)');@(Analyzer->'%(FullPath)');@(AdditionalFiles->'%(FullPath)');@(EmbeddedResource->'%(FullPath)');@(EditorConfigFiles->'%(FullPath)');@(GlobalAnalyzerConfigFiles->'%(FullPath)');@(_GlobalAnalyzerConfigFiles->'%(FullPath)');$(GeneratedMSBuildEditorConfigFile);$(ApplicationIcon);$(AppConfigForTargetPath);$(Win32Resource);$(Win32Manifest);$(CodeAnalysisRuleSet);$(KeyOriginatorFile)" />
      <AbilityKitCompilerInput Remove="@(AbilityKitCompilerInput)" Condition="!Exists('%(Identity)')" />
    </ItemGroup>
    <RemoveDuplicates Inputs="@(AbilityKitCompilerInput)">
      <Output TaskParameter="Filtered" ItemName="AbilityKitUniqueCompilerInput" />
    </RemoveDuplicates>
    <ItemGroup>
      <AbilityKitUniqueCompilerInput>
        <AbilityKitEncodedPath>$([System.Uri]::EscapeDataString('%(FullPath)'))</AbilityKitEncodedPath>
      </AbilityKitUniqueCompilerInput>
    </ItemGroup>
    <AbilityKitSetCompilerInputLengths Files="@(AbilityKitUniqueCompilerInput)">
      <Output TaskParameter="Items" ItemName="AbilityKitSizedCompilerInput" />
    </AbilityKitSetCompilerInputLengths>
    <GetFileHash Files="@(AbilityKitSizedCompilerInput)" Algorithm="SHA256">
      <Output TaskParameter="Items" ItemName="AbilityKitHashedCompilerInput" />
    </GetFileHash>
    <ItemGroup>
      <AbilityKitCompilerRecord Include="AKCT|1" />
      <AbilityKitCompilerRecord Include="owner|$(AbilityKitCompilerRunId)|$(AbilityKitCompilerResultId)|$(AbilityKitCompilerInvocationId)|$(AbilityKitCompilerCaptureId)" />
      <AbilityKitCompilerRecord Include="project|$([System.Uri]::EscapeDataString('$(MSBuildProjectFullPath)'))" />
      <AbilityKitCompilerRecord Include="dimensions|$([System.Uri]::EscapeDataString('$(TargetFramework)'))|$([System.Uri]::EscapeDataString('$(Configuration)'))|$([System.Uri]::EscapeDataString('$(Platform)'))|$([System.Uri]::EscapeDataString('$(RuntimeIdentifier)'))" />
      <AbilityKitCompilerRecord Include="target|$([System.Uri]::EscapeDataString('$(TargetPath)'))" />
      <AbilityKitCompilerRecord Include="@(AbilityKitHashedCompilerInput->'input|%(AbilityKitEncodedPath)|%(AbilityKitByteLength)|%(FileHash)')" />
    </ItemGroup>
    <WriteLinesToFile File="$(AbilityKitCompilerCaptureTemp)" Lines="@(AbilityKitCompilerRecord)" Overwrite="true" Encoding="UTF-8" />
    <GetFileHash Files="$(AbilityKitCompilerCaptureTemp)" Algorithm="SHA256">
      <Output TaskParameter="Items" ItemName="AbilityKitCompilerRecordHash" />
    </GetFileHash>
    <Error Condition="!Exists('$(AbilityKitCompilerCaptureTemp)') Or '@(AbilityKitCompilerRecordHash)' == ''" Text="Compiler capture temporary record was not closed and verified." />
    <Move SourceFiles="$(AbilityKitCompilerCaptureTemp)" DestinationFiles="$(AbilityKitCompilerCaptureComplete)" />
    <Error Condition="Exists('$(AbilityKitCompilerCaptureTemp)') Or !Exists('$(AbilityKitCompilerCaptureComplete)')" Text="Compiler capture atomic publication failed." />
    <Message Importance="High" Text="ABILITYKIT_CAPTURE|$(AbilityKitCompilerRunId)|$(AbilityKitCompilerResultId)|$(AbilityKitCompilerInvocationId)|$(AbilityKitCompilerCaptureId)|$(MSBuildProjectFullPath)|$(TargetFramework)|$(Configuration)|$(Platform)|$(RuntimeIdentifier)" />
  </Target>
</Project>
'@
    [IO.File]::WriteAllText($Path,$text,[Text.UTF8Encoding]::new($false))
}

function ConvertFrom-GateBase64 {
    param([string]$Value)
    try { return [Uri]::UnescapeDataString($Value) }
    catch { throw 'Malformed compiler capture encoding.' }
}

function Get-GateCompilerCapture {
    param([string]$Path, [string]$RunId, [string]$ResultId, [string]$InvocationId, [DateTimeOffset]$StartedAt, [DateTimeOffset]$EndedAt)
    Assert-GateNoReparse $Path
    $stream=$null
    try {
        $stream=[IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
        $bytes=New-Object byte[] $stream.Length
        $offset=0
        while ($offset -lt $bytes.Length) { $read=$stream.Read($bytes,$offset,$bytes.Length-$offset); if ($read -le 0) { throw 'Partial compiler capture read.' }; $offset+=$read }
    } catch { throw 'Compiler completion record is missing, partial, or locked.' }
    finally { if ($stream) { $stream.Dispose() } }
    try { $text=[Text.UTF8Encoding]::new($false,$true).GetString($bytes) } catch { throw 'Malformed compiler capture UTF-8.' }
    $lines=@($text -split "`r?`n" | Where-Object { $_ -cne '' })
    if ($lines.Count -lt 6 -or $lines[0] -cne 'AKCT|1') { throw 'Malformed compiler completion record.' }
    $owner=$lines[1].Split('|'); $project=$lines[2].Split('|'); $dimensions=$lines[3].Split('|'); $target=$lines[4].Split('|')
    if ($owner.Count -ne 5 -or $owner[0] -cne 'owner' -or $owner[1] -cne $RunId -or $owner[2] -cne $ResultId -or $owner[3] -cne $InvocationId -or $owner[4] -notmatch '^[0-9a-fA-F-]{36}$') { throw 'Compiler completion owner mismatch.' }
    if ($project.Count -ne 2 -or $project[0] -cne 'project' -or $dimensions.Count -ne 5 -or $dimensions[0] -cne 'dimensions' -or $target.Count -ne 2 -or $target[0] -cne 'target') { throw 'Malformed compiler completion identity.' }
    $captureId=$owner[4]
    if ([IO.Path]::GetFileNameWithoutExtension($Path) -cne $captureId) { throw 'Compiler completion filename/capture identity mismatch.' }
    $projectPath=[IO.Path]::GetFullPath((ConvertFrom-GateBase64 $project[1])); $targetPath=[IO.Path]::GetFullPath((ConvertFrom-GateBase64 $target[1]))
    $inputs=@(); $seen=New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($line in $lines[5..($lines.Count-1)]) {
        $parts=$line.Split('|')
        if ($parts.Count -ne 4 -or $parts[0] -cne 'input' -or $parts[2] -notmatch '^\d+$' -or $parts[3] -notmatch '^[0-9A-Fa-f]{64}$') { throw 'Malformed compiler input record.' }
        $inputPath=[IO.Path]::GetFullPath((ConvertFrom-GateBase64 $parts[1])); $length=[long]$parts[2]; $hash=$parts[3].ToLowerInvariant()
        Assert-GateNoReparse $inputPath
        if (-not [IO.File]::Exists($inputPath) -or (Get-Item -LiteralPath $inputPath).Length -ne $length -or (Get-GateHash $inputPath) -cne $hash) { throw 'Compiler input changed since capture.' }
        if ($seen.ContainsKey($inputPath)) {
            $prior=$seen[$inputPath]
            if ($prior.bytes -ne $length -or $prior.sha256 -cne $hash) { throw 'Conflicting duplicate compiler input path in one capture.' }
            continue
        }
        $entry=[pscustomobject]@{path=$inputPath;bytes=$length;sha256=$hash}; $seen.Add($inputPath,$entry); $inputs+=$entry
    }
    if (-not $inputs.Count) { throw 'Empty compiler input capture.' }
    $written=(Get-Item -LiteralPath $Path).LastWriteTimeUtc
    $freshLower=$StartedAt.UtcDateTime.AddSeconds(-2); $freshUpper=$EndedAt.UtcDateTime.AddSeconds(2)
    if ($written -lt $freshLower -or $written -gt $freshUpper) { throw ('Stale compiler completion record: '+$written.ToString('o')+' outside '+$freshLower.ToString('o')+'..'+$freshUpper.ToString('o')) }
    return [pscustomobject][ordered]@{runId=$owner[1];resultId=$owner[2];invocationId=$owner[3];captureId=$captureId;project=$projectPath;tfm=ConvertFrom-GateBase64 $dimensions[1];configuration=ConvertFrom-GateBase64 $dimensions[2];platform=ConvertFrom-GateBase64 $dimensions[3];runtimeIdentifier=ConvertFrom-GateBase64 $dimensions[4];targetPath=$targetPath;inputs=$inputs}
}

function Add-GateCompilerNativeReceipt {
    param($Context,$Result,$Native,[string[]]$ToolArguments,[string]$Stem)
    $null=Add-GateArtifact $Result $Context.runRoot $Native.stdoutPath ($Stem+'-stdout')
    $null=Add-GateArtifact $Result $Context.runRoot $Native.stderrPath ($Stem+'-stderr')
    $Native | Add-Member -NotePropertyName toolArguments -NotePropertyValue @($ToolArguments)
    $Result.provenance.invocations+= $Native
}

function New-GateCompilerInvocation {
    param($Context,$Result,[string]$SdkRoot)
    $nodeRoot=Get-GateNodeRoot $Context.runRoot $Result.resultId
    $parent=Join-Path $nodeRoot 'bi'
    if (-not [IO.Directory]::Exists($parent)) { $null=New-Item -ItemType Directory -Path $parent }
    Assert-GateNoReparse $parent
    $invocationId=[guid]::NewGuid().ToString()
    $root=Join-Path $parent ($invocationId.Replace('-','').Substring(0,12))
    if (Test-Path -LiteralPath $root) { throw 'Compiler invocation directory collision.' }
    $null=New-Item -ItemType Directory -Path $root
    $pending=Join-Path $root 'pending'; $completed=Join-Path $root 'completed'
    $null=New-Item -ItemType Directory -Path $pending
    $null=New-Item -ItemType Directory -Path $completed
    Assert-GateNoReparse $root
    $readerArtifacts=@(); $readerDll=$null
    if (-not $Context.control) {
        $readerRoot=Join-Path $root 'reader'; $output=Join-Path $readerRoot 'out'
        $null=New-Item -ItemType Directory -Path $readerRoot
        $source=Join-Path $Context.repoRoot 'tools/test-gate-compiler-events.cs'
        if (-not [IO.File]::Exists($source)) { throw 'Blocked:CompilerEventReaderSourceMissing' }
        $sourceCopy=Join-Path $readerRoot 'Program.cs'; Copy-Item -LiteralPath $source -Destination $sourceCopy
        $project=Join-Path $readerRoot 'compiler-events.csproj'
        $buildAssembly=[Security.SecurityElement]::Escape((Join-Path $SdkRoot 'Microsoft.Build.dll'))
        $frameworkAssembly=[Security.SecurityElement]::Escape((Join-Path $SdkRoot 'Microsoft.Build.Framework.dll'))
        $projectText=@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><RestoreSources></RestoreSources><RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources></PropertyGroup>
  <ItemGroup><Reference Include="Microsoft.Build"><HintPath>$buildAssembly</HintPath><Private>true</Private></Reference><Reference Include="Microsoft.Build.Framework"><HintPath>$frameworkAssembly</HintPath><Private>true</Private></Reference></ItemGroup>
</Project>
"@
        [IO.File]::WriteAllText($project,$projectText,[Text.UTF8Encoding]::new($false))
        $readerArtifacts+=Add-GateArtifact $Result $Context.runRoot $sourceCopy 'compiler-event-reader-source'
        $readerArtifacts+=Add-GateArtifact $Result $Context.runRoot $project 'compiler-event-reader-project'
        $arguments=@('build',$project,'-c','Release','--nologo','-o',$output)
        $native=Invoke-GateNative $Context.dotnetPath $arguments $root 'reader-build' $Context.repoRoot $Context.timeoutSeconds $Context.cancelSignalPath
        Add-GateCompilerNativeReceipt $Context $Result $native @('compiler-event-reader-build') 'compiler-event-reader-build'
        if ($native.reason -or -not $native.exitConfirmed -or $native.processExitCode -ne 0) { throw 'Blocked:CompilerEventReaderBuildFailed' }
        $readerDll=Join-Path $output 'compiler-events.dll'
        if (-not [IO.File]::Exists($readerDll)) { throw 'Blocked:CompilerEventReaderOutputMissing' }
        foreach ($file in Get-ChildItem -LiteralPath $output -File | Sort-Object Name) { $readerArtifacts+=Add-GateArtifact $Result $Context.runRoot $file.FullName 'compiler-event-reader-output' }
    }
    return [pscustomobject][ordered]@{invocationId=$invocationId;root=$root;pending=$pending;completed=$completed;readerDll=$readerDll;readerArtifacts=@($readerArtifacts)}
}

function Get-GateEventContextKey {
    param($Context)
    foreach ($field in @('submissionId','nodeId','projectInstanceId','projectContextId')) {
        if ($null -eq $Context.$field -or [int]$Context.$field -lt 0) { throw "Compiler event context is incomplete: $field" }
    }
    return ([string]$Context.submissionId)+'|'+([string]$Context.nodeId)+'|'+([string]$Context.projectInstanceId)+'|'+([string]$Context.projectContextId)
}

function Invoke-GateCompilerEventReader {
    param($Context,$Result,$Invocation,[string]$EventLogPath)
    $manifestPath=Join-Path $Invocation.root 'events.json'
    if ([IO.File]::Exists($manifestPath)) { throw 'Compiler event manifest collision.' }
    if ($Context.control) {
        $raw=Get-Content -LiteralPath $EventLogPath -Raw -Encoding UTF8 | ConvertFrom-Json
        [IO.File]::WriteAllText($manifestPath,(ConvertTo-GateJson $raw),[Text.UTF8Encoding]::new($false))
    } else {
        $arguments=@($Invocation.readerDll,$EventLogPath,$manifestPath,$Result.runId,$Result.resultId,$Invocation.invocationId)
        $native=Invoke-GateNative $Context.dotnetPath $arguments $Invocation.root 'reader-run' $Context.repoRoot $Context.timeoutSeconds $Context.cancelSignalPath
        Add-GateCompilerNativeReceipt $Context $Result $native @('compiler-event-reader-replay') 'compiler-event-reader-replay'
        if ($native.reason -or -not $native.exitConfirmed -or $native.processExitCode -ne 0) { throw 'Compiler event log is malformed or incomplete.' }
    }
    if (-not [IO.File]::Exists($manifestPath)) { throw 'Compiler event manifest missing.' }
    return $manifestPath
}

function Complete-GateCompilerEvidence {
    param($Context,$Result,$Invocation,$Native,[string]$TargetPath,[string]$EventLogPath)
    if (-not $Native.exitConfirmed -or $Native.processExitCode -ne 0) { throw 'Native build was not successfully closed before compiler aggregation.' }
    $targetArtifact=Add-GateArtifact $Result $Context.runRoot $TargetPath 'compiler-target'
    $eventLogArtifact=Add-GateArtifact $Result $Context.runRoot $EventLogPath 'compiler-event-log'
    $manifestPath=Invoke-GateCompilerEventReader $Context $Result $Invocation $EventLogPath
    $eventManifestArtifact=Add-GateArtifact $Result $Context.runRoot $manifestPath 'compiler-event-manifest'
    $manifest=Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1 -or -not $manifest.complete -or $manifest.runId -cne $Result.runId -or $manifest.resultId -cne $Result.resultId -or $manifest.invocationId -cne $Invocation.invocationId -or $manifest.buildStarted -ne 1 -or $manifest.buildFinished -ne 1 -or @($manifest.errors).Count) { throw 'Compiler event manifest ownership/completeness mismatch.' }
    Assert-GateNoReparse $Invocation.root; Assert-GateNoReparse $Invocation.pending; Assert-GateNoReparse $Invocation.completed
    if (@(Get-ChildItem -LiteralPath $Invocation.pending -Force).Count) { throw 'Partial compiler capture remained unpublished.' }
    $completionEntries=@(Get-ChildItem -LiteralPath $Invocation.completed -Force)
    if (@($completionEntries | Where-Object PSIsContainer).Count) { throw 'Extra compiler completion material.' }
    $files=@($completionEntries | Where-Object { -not $_.PSIsContainer })
    if (-not $files.Count -or @($files | Where-Object { $_.Name -cnotmatch '^[0-9a-fA-F-]{36}\.complete$' }).Count) { throw 'Missing or extra compiler completion record.' }
    $captures=@(); $captureArtifacts=@(); $captureIds=@{}
    foreach ($file in $files) {
        $capture=Get-GateCompilerCapture $file.FullName $Result.runId $Result.resultId $Invocation.invocationId ([DateTimeOffset]$Native.startedAt) ([DateTimeOffset]$Native.endedAt)
        if ($captureIds.ContainsKey($capture.captureId)) { throw 'Duplicate compiler capture ID.' }
        $captureIds[$capture.captureId]=$true; $captures+=$capture
        $captureArtifacts+=Add-GateArtifact $Result $Context.runRoot $file.FullName 'compiler-capture'
    }
    $messages=@($manifest.captures)
    if ($messages.Count -ne $captures.Count -or @($messages.captureId | Sort-Object -Unique).Count -ne $messages.Count) { throw 'Compiler capture message/completion cardinality mismatch.' }
    $compileTargets=@($manifest.coreCompile)
    $compilerTasks=@($manifest.csc)
    foreach ($compileTarget in $compileTargets) {
        $contextKey=Get-GateEventContextKey $compileTarget.context
        $matchingMessages=@($messages | Where-Object { (Get-GateEventContextKey $_.context) -ceq $contextKey })
        if ($matchingMessages.Count -ne 1) { throw 'CoreCompile event missing exact capture coverage.' }
        if ([IO.Path]::GetFullPath($compileTarget.project) -ine [IO.Path]::GetFullPath($matchingMessages[0].project)) { throw 'CoreCompile/capture project identity mismatch.' }
        $matchingTasks=@($compilerTasks | Where-Object { (Get-GateEventContextKey $_.context) -ceq $contextKey })
        if ($compileTarget.state -ceq 'Finished') {
            if (-not $compileTarget.succeeded -or $matchingTasks.Count -ne 1 -or -not $matchingTasks[0].succeeded) { throw 'Finished CoreCompile requires exactly one successful Csc execution.' }
            if ([IO.Path]::GetFullPath($matchingTasks[0].project) -ine [IO.Path]::GetFullPath($compileTarget.project)) { throw 'CoreCompile/Csc project identity mismatch.' }
        } elseif ($compileTarget.state -ceq 'Skipped') {
            if ($matchingTasks.Count) { throw 'Skipped CoreCompile must not have a Csc execution.' }
        } else { throw 'CoreCompile event state is invalid.' }
    }
    $actualInputs=New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::OrdinalIgnoreCase)
    $instances=@()
    foreach ($message in $messages) {
        $capture=@($captures | Where-Object captureId -ceq $message.captureId)
        if ($capture.Count -ne 1) { throw 'Compiler capture message lacks one completion record.' }
        $capture=$capture[0]
        foreach ($field in @('runId','resultId','invocationId','captureId','tfm','configuration','platform','runtimeIdentifier')) { if ([string]$capture.$field -cne [string]$message.$field) { throw "Compiler capture identity mismatch: $field" } }
        if ([IO.Path]::GetFullPath($capture.project) -ine [IO.Path]::GetFullPath($message.project)) { throw 'Compiler capture project substitution.' }
        $contextKey=Get-GateEventContextKey $message.context
        $projects=@($manifest.projects | Where-Object { (Get-GateEventContextKey $_.context) -ceq $contextKey -and $_.project -ieq $capture.project })
        if ($projects.Count -ne 1 -or -not $projects[0].propertiesFingerprint) { throw 'Compiler project instance identity missing/ambiguous.' }
        $matchingTasks=@($compilerTasks | Where-Object { (Get-GateEventContextKey $_.context) -ceq $contextKey })
        $matchingTargets=@($compileTargets | Where-Object { (Get-GateEventContextKey $_.context) -ceq $contextKey })
        if ($matchingTargets.Count -ne 1) { throw 'Capture lacks exactly one current CoreCompile engine event.' }
        $compileTarget=$matchingTargets[0]
        if ([IO.Path]::GetFullPath($compileTarget.project) -ine [IO.Path]::GetFullPath($capture.project)) { throw 'Compiler capture/CoreCompile project identity mismatch.' }
        $kind=if ($compileTarget.state -ceq 'Finished') { 'Executed' } elseif ($compileTarget.state -ceq 'Skipped') { 'IncrementalSkip' } else { throw 'CoreCompile event state is invalid.' }
        if ($kind -ceq 'Executed') {
            if (-not $compileTarget.succeeded -or $matchingTasks.Count -ne 1 -or -not $matchingTasks[0].succeeded -or -not @($matchingTasks[0].inputs).Count) { throw 'Csc execution inputs absent, unsuccessful, or ambiguous.' }
            if ([IO.Path]::GetFullPath($matchingTasks[0].project) -ine [IO.Path]::GetFullPath($capture.project)) { throw 'Compiler capture/Csc project identity mismatch.' }
            foreach ($input in @($matchingTasks[0].inputs)) {
                $match=@($capture.inputs | Where-Object path -ieq $input.path)
                if ($match.Count -ne 1 -or $match[0].bytes -ne $input.bytes -or $match[0].sha256 -cne $input.sha256) { throw ('Csc event input is unexplained by its completion record: '+$input.path) }
            }
        } elseif ($matchingTasks.Count) { throw 'Skipped CoreCompile must not have a Csc execution.' }
        if (-not [IO.File]::Exists($capture.targetPath)) { throw 'Compiler instance output identity missing.' }
        $target=[pscustomobject]@{path=[IO.Path]::GetFullPath($capture.targetPath);bytes=(Get-Item -LiteralPath $capture.targetPath).Length;sha256=Get-GateHash $capture.targetPath}
        foreach ($input in $capture.inputs) {
            if ($actualInputs.ContainsKey($input.path)) {
                $prior=$actualInputs[$input.path]
                if ($prior.bytes -ne $input.bytes -or $prior.sha256 -cne $input.sha256) { throw 'Conflicting compiler content for one normalized path.' }
            } else { $actualInputs.Add($input.path,$input) }
        }
        $instances+=[pscustomobject][ordered]@{captureId=$capture.captureId;project=$capture.project;tfm=$capture.tfm;configuration=$capture.configuration;platform=$capture.platform;runtimeIdentifier=$capture.runtimeIdentifier;context=$message.context;propertiesFingerprint=$projects[0].propertiesFingerprint;kind=$kind;target=$target;inputs=@($capture.inputs | Sort-Object path)}
    }
    foreach ($task in $compilerTasks) {
        $key=Get-GateEventContextKey $task.context
        $matchingTargets=@($compileTargets | Where-Object { (Get-GateEventContextKey $_.context) -ceq $key })
        $matchingMessages=@($messages | Where-Object { (Get-GateEventContextKey $_.context) -ceq $key })
        if ($matchingTargets.Count -ne 1 -or $matchingMessages.Count -ne 1) { throw 'Csc execution missing exact CoreCompile/capture coverage.' }
        if ($matchingTargets[0].state -cne 'Finished' -or -not $matchingTargets[0].succeeded -or -not $task.succeeded) { throw 'Csc execution is not owned by one successful finished CoreCompile.' }
        if ([IO.Path]::GetFullPath($task.project) -ine [IO.Path]::GetFullPath($matchingTargets[0].project) -or [IO.Path]::GetFullPath($task.project) -ine [IO.Path]::GetFullPath($matchingMessages[0].project)) { throw 'Csc/CoreCompile/capture project identity mismatch.' }
    }
    $compilerInputs=Save-GateFiles $Result $Context.runRoot @($actualInputs.Keys | Sort-Object) 'compiler-input' @($Result.provenance.closure.configPaths)
    $evidence=[pscustomobject][ordered]@{schemaVersion=2;mode='Executed';runId=$Result.runId;resultId=$Result.resultId;invocationId=$Invocation.invocationId;origin=$null;targetArtifact=$targetArtifact;eventLogArtifact=$eventLogArtifact;eventManifestArtifact=$eventManifestArtifact;readerArtifacts=@($Invocation.readerArtifacts);captureArtifacts=@($captureArtifacts);instances=@($instances | Sort-Object project,tfm,captureId);inputFingerprint=Get-GateFingerprint @($actualInputs.Values);aggregateArtifact=$null}
    $aggregatePath=Join-Path $Invocation.root 'compiler-evidence.json'
    [IO.File]::WriteAllText($aggregatePath,(ConvertTo-GateJson $evidence),[Text.UTF8Encoding]::new($false))
    $evidence.aggregateArtifact=Add-GateArtifact $Result $Context.runRoot $aggregatePath 'compiler-evidence'
    return [pscustomobject]@{evidence=$evidence;inputs=$compilerInputs}
}

function Assert-GateCompilerEvidence {
    param($Result,$Context)
    $evidence=$Result.provenance.compilerEvidence
    if (-not $evidence -or $evidence.schemaVersion -ne 2 -or $evidence.mode -cnotin @('Executed','Reused') -or $evidence.runId -cne $Result.runId -or $evidence.resultId -cne $Result.resultId -or -not $evidence.instances.Count -or -not $evidence.captureArtifacts.Count) { throw 'Compiler evidence absent or invalid.' }
    foreach ($artifact in @($evidence.targetArtifact,$evidence.eventLogArtifact,$evidence.eventManifestArtifact)+@($evidence.readerArtifacts)+@($evidence.captureArtifacts)) { $null=Assert-GateArtifact $artifact $Result $Context.runRoot }
    $aggregatePath=Assert-GateArtifact $evidence.aggregateArtifact $Result $Context.runRoot
    $aggregate=Get-Content -LiteralPath $aggregatePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $candidate=ConvertTo-GateJson $evidence | ConvertFrom-Json; $candidate.aggregateArtifact=$null
    Assert-GateEqual $candidate $aggregate 'compiler aggregate receipt'
    $deduplicated=New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($instance in $evidence.instances) {
        if (-not $instance.captureId -or -not $instance.propertiesFingerprint -or $instance.kind -cnotin @('Executed','IncrementalSkip') -or -not [IO.Path]::IsPathRooted($instance.project) -or -not [IO.Path]::IsPathRooted($instance.target.path)) { throw 'Compiler instance manifest invalid.' }
        $output=@($Result.provenance.build.outputs | Where-Object { [IO.Path]::GetFullPath($_.path) -ieq [IO.Path]::GetFullPath($instance.target.path) })
        if ($output.Count -ne 1 -or $output[0].bytes -ne $instance.target.bytes -or $output[0].sha256 -cne $instance.target.sha256) { throw 'Compiler instance output is not in the build producer set.' }
        foreach ($input in $instance.inputs) {
            if ($deduplicated.ContainsKey($input.path)) {
                $prior=$deduplicated[$input.path]
                if ($prior.bytes -ne $input.bytes -or $prior.sha256 -cne $input.sha256) { throw 'Conflicting compiler input identity.' }
            } else { $deduplicated.Add($input.path,$input) }
        }
    }
    if ((Get-GateFingerprint @($deduplicated.Values)) -cne $evidence.inputFingerprint) { throw 'Compiler evidence input fingerprint mismatch.' }
    $inputs=@($Result.provenance.compilerInputs)
    if ($inputs.Count -ne $deduplicated.Count) { throw 'Incomplete compiler input archive membership.' }
    foreach ($entry in $inputs) {
        if (-not $deduplicated.ContainsKey($entry.path) -or $deduplicated[$entry.path].bytes -ne $entry.bytes -or $deduplicated[$entry.path].sha256 -cne $entry.sha256) { throw 'Compiler input archive identity mismatch.' }
        if ($entry.proofKind) { Assert-GateConfigProof $entry @($Result.provenance.closure.configPaths) } else { $null=Assert-GateArtifact $entry.artifact $Result $Context.runRoot }
    }
    if ($evidence.mode -ceq 'Executed' -and ($Result.provenance.build.mode -cne 'Executed' -or $evidence.origin -ne $null)) { throw 'Executed compiler evidence mode contradiction.' }
    if ($evidence.mode -ceq 'Reused' -and ($Result.provenance.build.mode -cne 'Reused' -or -not $evidence.origin)) { throw 'Reused compiler evidence origin missing.' }
}

function Invoke-GateDotNet {
    param($Context, $Result, $Step, [switch]$NoBuild, [switch]$NoRestore, [string]$ReuseManifestPath)
    $Result.times.invokedAt=[DateTimeOffset]::UtcNow.ToString('o')
    $fixtureMissing=$Context.control -and ($Context.control.mode -ceq 'missing-tool' -or ($Context.control.mode -ceq 'optional-missing-tool' -and $Result.kind -ceq 'dotnet-test'))
    if ((-not $Context.dotnetPath -and -not $Context.control) -or $fixtureMissing) {
        Set-GateTerminal $Result $(if ($Step.skipPolicy -ceq 'MissingTool' -and $Step.required -eq $false) { 'Skipped' } else { 'Blocked' }) 'MissingTool'
        return
    }
    $Result.provenance=[pscustomobject][ordered]@{schemaVersion=1; example=[bool]$Context.allowExample; project=$Result.configuration.project; tfm=$Result.configuration.tfm; configuration=$Context.configuration; configSha256=$Result.configuration.configSha256; sdk=$null; source=$null; sourceBeforeRestore=$null; closure=@(); invocations=@(); restore=$null; build=$null; test=$null; assembly=$null; compilerEvidence=$null; compilerInputs=@(); loadedBefore=@(); loadedAfter=@(); trxBinding=$null}
    $nodeRoot=Get-GateNodeRoot $Context.runRoot $Result.resultId
    try {
        $sdkInvocation=Invoke-GateTool $Context $Result @('--version') 'sdk'
        $sdk=(Get-Content -LiteralPath $sdkInvocation.stdoutPath -Raw -Encoding UTF8).Trim()
        if ($sdk -notmatch '^\d+\.\d+\.\d+[-+\w.]*$') { throw 'Invalid SDK identity.' }
        $Result.provenance.sdk=$sdk
        $null=Invoke-GateTool $Context $Result @('--info') 'host-info'
        $sdkList=Invoke-GateTool $Context $Result @('--list-sdks') 'sdk-list'
        $sdkRoots=@(foreach ($line in [IO.File]::ReadAllLines($sdkList.stdoutPath)) {
            if ($line -match '^([^ ]+) \[(.+)\]$' -and $Matches[1] -ceq $sdk) { Join-Path $Matches[2] $sdk }
        })
        if ($sdkRoots.Count -ne 1) { throw 'Ambiguous/missing evaluated SDK root.' }
        $sdkRoot=$sdkRoots[0]
        $Context.sdkInputPaths=@(foreach ($name in @('MSBuild.dll','Microsoft.Build.dll','Microsoft.Build.Framework.dll','Microsoft.Build.Tasks.Core.dll','Microsoft.Build.Utilities.Core.dll','MSBuild.deps.json','MSBuild.runtimeconfig.json')) {
            $file=Join-Path $sdkRoot $name
            if (-not [IO.File]::Exists($file)) { throw 'SDK build tool input missing.' }
            $file
        })
        $compilerRoot=Join-Path $sdkRoot 'Roslyn/bincore'
        if (-not [IO.Directory]::Exists($compilerRoot)) { throw 'SDK compiler input closure absent.' }
        $Context.sdkInputPaths+= @(Get-ChildItem -LiteralPath $compilerRoot -File -Recurse | ForEach-Object { $_.FullName })
        $Result.tools=@([pscustomobject]@{name='dotnet'; version=$sdk; executable=$Context.dotnetPath; sha256=$(if ($Context.dotnetPath) { Get-GateHash $Context.dotnetPath } else { Get-GateHash $Context.control.executor }); synthetic=[bool]$Context.control},[pscustomobject]@{name='PowerShell'; version=$PSVersionTable.PSVersion.ToString(); host=[Environment]::Version.ToString(); os=[Environment]::OSVersion.VersionString})
        $closure=Get-GateProjectClosure $Context $Result $Result.configuration.project 'before'
        $authoredPaths=@($closure.inputPaths | Where-Object { $_ -inotin $closure.restoreOutputs })
        $before=Get-GateSource $Context.repoRoot $authoredPaths $Context.exclusions
        $Result.provenance.sourceBeforeRestore=$before; $Result.provenance.closure=$closure.projects
        $Result.source=[pscustomobject]@{before=$before; after=$null}
        $initialInputs=Save-GateFiles $Result $Context.runRoot $authoredPaths 'input' $closure.configPaths
        $priorOutputs=Save-GateFiles $Result $Context.runRoot @($closure.restoreOutputs | Where-Object { [IO.File]::Exists($_) }) 'prior-restore-output'
        $skipBuild=[bool]$NoBuild -and $Result.kind -ceq 'dotnet-test'
        $reused=$null
        if ($NoRestore -or $skipBuild) { $reused=Use-GateReuse $Context $Result $ReuseManifestPath $sdk $skipBuild }
        $restoreArgs=@('restore',$Result.configuration.project,('-p:Configuration='+$Context.configuration),'--nologo')
        if ($NoRestore -or $skipBuild) { $Result.provenance.restore=$reused.restore }
        else {
            $native=Invoke-GateTool $Context $Result $restoreArgs 'restore'
            Assert-GateInputs $before.inputs
            $assets=Save-GateFiles $Result $Context.runRoot $closure.restoreOutputs 'assets'
            $Result.provenance.restore=New-GateStage 'restore' $native $initialInputs $assets $Result $closure.projects $priorOutputs
            Assert-GateStage $Result.provenance.restore $Result $Context 'restore'
        }
        # Restore can introduce package imports/analyzers. Freeze the resulting
        # evaluated source plus the exact package resolution before building.
        $restored=Get-GateProjectClosure $Context $Result $Result.configuration.project 'restored'
        Assert-GateEqual @($restored.projects.project) @($closure.projects.project) 'restore evaluated project graph'
        $packagePaths=Get-GatePackageInputs $restored.projects
        $inputPaths=@($restored.inputPaths+$packagePaths | Sort-Object -Unique)
        Assert-GateInputs $before.inputs
        $source=Get-GateSource $Context.repoRoot $inputPaths $Context.exclusions
        $Result.source.before=$source; $Result.provenance.source=$source; $Result.provenance.closure=$restored.projects
        $buildInputs=Save-GateFiles $Result $Context.runRoot @($inputPaths+$restored.projects.assetsPath | Sort-Object -Unique) 'build-input' $restored.configPaths
        $assembly=$restored.projects[0].targetPath; $Result.provenance.assembly=$assembly
        if ($skipBuild) {
            $Result.provenance.build=$reused.build
            $Result.provenance.compilerInputs=$reused.compilerInputs
            $Result.provenance.compilerEvidence=$reused.compilerEvidence
        }
        else {
            $compilerInvocation=New-GateCompilerInvocation $Context $Result $sdkRoot
            $targetPath=Join-Path $compilerInvocation.root 'compiler.targets'; $eventLogPath=Join-Path $compilerInvocation.root 'build.binlog'
            Save-GateCompilerTarget $targetPath
            $args=@('build',$Result.configuration.project,'-c',$Context.configuration,'-f',$Result.configuration.tfm,'--no-restore','--nologo',('-m'),('-bl:'+$eventLogPath+';ProjectImports=None'),('-p:CustomAfterMicrosoftCommonTargets='+$targetPath),('-p:AbilityKitCompilerCaptureRoot='+$compilerInvocation.root),('-p:AbilityKitCompilerRunId='+$Result.runId),('-p:AbilityKitCompilerResultId='+$Result.resultId),('-p:AbilityKitCompilerInvocationId='+$compilerInvocation.invocationId))
            $native=Invoke-GateTool $Context $Result $args 'build'
            $Result.command=$native.command; $Result.processExitCode=$native.processExitCode
            $Result.times.executionStartedAt=$native.startedAt; $Result.times.executionEndedAt=$native.endedAt
            Assert-GateInputs $source.inputs; Assert-GateInputs $Result.provenance.restore.outputs
            $compiler=Complete-GateCompilerEvidence $Context $Result $compilerInvocation $native $targetPath $eventLogPath
            $Result.provenance.compilerEvidence=$compiler.evidence
            $Result.provenance.compilerInputs=$compiler.inputs
            $binaryPaths=@(foreach ($item in $restored.projects) {
                if (-not [IO.File]::Exists($item.targetPath)) { throw 'Expected project binary absent.' }
                $item.targetPath
                # Archive all files in the output directory, including deps/runtime,
                # package/generator/analyzer binaries and copied resources.
                Get-ChildItem -LiteralPath (Split-Path $item.targetPath) -File -Recurse | ForEach-Object { $_.FullName }
            })
            $binaries=Save-GateFiles $Result $Context.runRoot $binaryPaths 'binary'
            $Result.provenance.build=New-GateStage 'build' $native $buildInputs $binaries $Result
        }
        Assert-GateInputs $Result.provenance.restore.outputs; Assert-GateInputs $Result.provenance.build.outputs
        if ($Result.kind -ceq 'dotnet-test') {
            $Result.provenance.loadedBefore=Save-GateFiles $Result $Context.runRoot @($Result.provenance.build.outputs.path) 'loaded'
            $trxPath=Join-Path $nodeRoot 'test-results.trx'
            $args=@('test',$Result.configuration.project,'-c',$Context.configuration,'-f',$Result.configuration.tfm,'--no-build','--no-restore','--logger','trx;LogFileName=test-results.trx','--results-directory',$nodeRoot,'--nologo')
            if ($Result.configuration.filter) { $args+=@('--filter',$Result.configuration.filter) }
            $native=Invoke-GateTool $Context $Result $args 'test'
            $Result.command=$native.command; $Result.processExitCode=$native.processExitCode
            $Result.times.executionStartedAt=$native.startedAt; $Result.times.executionEndedAt=$native.endedAt
            if (-not [IO.File]::Exists($trxPath)) { throw 'Missing TRX result.' }
            $trx=Add-GateArtifact $Result $Context.runRoot $trxPath 'trx'
            $Result.tests=Get-GateTrxTests $trxPath $assembly $Result.configuration.filter $native.startedAt $native.endedAt
            Assert-GateTests $Result.tests
            $Result.provenance.test=New-GateStage 'test' $native $Result.provenance.loadedBefore @([pscustomobject]@{path=$trxPath; bytes=$trx.bytes; sha256=$trx.sha256; artifact=$trx}) $Result
            $Result.provenance.trxBinding=[pscustomobject]@{runId=$Result.runId; resultId=$Result.resultId; sourceSha=$source.sha; sha256=$trx.sha256; filter=$Result.configuration.filter}
            Assert-GateInputs $Result.provenance.loadedBefore
            $Result.provenance.loadedAfter=$Result.provenance.loadedBefore
        } else {
            $Result.command=$Result.provenance.build.command; $Result.processExitCode=$Result.provenance.build.processExitCode
            $Result.times.executionStartedAt=$Result.provenance.build.startedAt; $Result.times.executionEndedAt=$Result.provenance.build.endedAt
        }
        $afterClosure=Get-GateProjectClosure $Context $Result $Result.configuration.project 'after'
        $afterPaths=@($afterClosure.inputPaths+(Get-GatePackageInputs $afterClosure.projects) | Sort-Object -Unique)
        Assert-GateEqual $inputPaths $afterPaths 'evaluated input closure before/after'
        $Result.source.after=Get-GateSource $Context.repoRoot $afterPaths $Context.exclusions
        if ($source.sha -cne $Result.source.after.sha -or $source.inputFingerprint -cne $Result.source.after.inputFingerprint) { throw 'Source changed during invocation.' }
        Save-GateProducerReceipt $Result $Context
        Assert-GateProvenance $Result $Context
        Set-GateTerminal $Result 'Passed' $null
    } catch {
        $message=$_.Exception.Message
        $last=$Result.provenance.invocations | Select-Object -Last 1
        $blocked=$message.StartsWith('Blocked:') -or $message.StartsWith('UnknownWriter')
        if ($last -and -not $blocked -and -not $Result.command) {
            $Result.command=$last.command; $Result.processExitCode=$last.processExitCode
            $Result.times.executionStartedAt=$last.startedAt; $Result.times.executionEndedAt=$last.endedAt
        }
        Set-GateTerminal $Result $(if ($blocked) { 'Blocked' } else { 'Failed' }) $message
    } finally {
        # Failed build attempts keep valid independent restore receipts for NoRestore.
        if ($Result.provenance.restore) {
            if (-not $Result.provenance.source -and $Result.source) { $Result.provenance.source=$Result.source.before }
            Save-GateProducerReceipt $Result $Context
        }
    }
}
