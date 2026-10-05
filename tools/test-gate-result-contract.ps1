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
    param($Result, [string]$RunRoot, [string[]]$Paths, [string]$Role)
    $entries = @(); $index = 0
    foreach ($path in @($Paths | Sort-Object -Unique)) {
        $full = [IO.Path]::GetFullPath($path)
        Assert-GateNoReparse $full
        if (-not [IO.File]::Exists($full)) { throw "Missing $Role input: $full" }
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
        $projects+=[pscustomobject]@{project=$path; tfm=[string]$properties.TargetFramework; targetPath=[IO.Path]::GetFullPath($properties.TargetPath); assetsPath=[IO.Path]::GetFullPath($properties.ProjectAssetsFile); assemblyName=[string]$properties.AssemblyName; packageRoot=[string]$properties.NuGetPackageRoot}
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
    return [pscustomobject]@{projects=$projects; inputPaths=@($inputs | Sort-Object -Unique)}
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
    param([string]$Name, $Native, [object[]]$Inputs, [object[]]$Outputs, $Result)
    return [pscustomobject][ordered]@{name=$Name; mode='Executed'; runId=$Result.runId; resultId=$Result.resultId; command=$Native.command; startedAt=$Native.startedAt; endedAt=$Native.endedAt; processExitCode=$Native.processExitCode; inputs=@($Inputs); inputFingerprint=Get-GateFingerprint $Inputs; outputs=@($Outputs); outputFingerprint=Get-GateFingerprint $Outputs; reuse=$null}
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
        if ($receipt.inputFingerprint -cne $Stage.inputFingerprint -or $receipt.outputFingerprint -cne $Stage.outputFingerprint) { throw 'Reuse fingerprint mismatch.' }
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
        foreach ($binding in @(@('Configuration',$Result.configuration.configuration),@('TargetFramework',$Result.configuration.tfm))) {
            $prefix='-p:'+$binding[0]+'='
            $values=@($argv | Where-Object { $_.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) })
            if ($values.Count -ne 1 -or $values[0] -cne $prefix+$binding[1]) { throw 'Restore Configuration/TFM argv mismatch.' }
        }
        if (@($argv | Where-Object { $_ -match '^/(p|property):|^--(configuration|framework)$|^-[cf]$' }).Count) { throw 'Ambiguous restore dimension argv.' }
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
            $null=Assert-GateArtifact $entry.artifact $Result $Context.runRoot
            if ($entry.sha256 -cne $entry.artifact.sha256 -or $entry.bytes -ne $entry.artifact.bytes) { throw 'Stage archive contradiction.' }
        }
        if ((Get-GateFingerprint $entries) -cne $Stage.($set.Substring(0,$set.Length-1)+'Fingerprint')) { throw 'Forged stage fingerprint.' }
    }
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
    Assert-GateInputs $Result.source.after.inputs
    Assert-GateStage $p.restore $Result $Context 'restore'
    Assert-GateStage $p.build $Result $Context 'build'
    $terminal=if ($Result.kind -ceq 'dotnet-test') { $p.test } else { $p.build }
    if (-not $terminal -or $terminal.mode -cne 'Executed') { throw 'Terminal stage was not executed.' }
    Assert-GateEqual $Result.command $terminal.command 'terminal summary command'
    if ($Result.processExitCode -ne $terminal.processExitCode -or $Result.times.executionStartedAt -cne $terminal.startedAt -or $Result.times.executionEndedAt -cne $terminal.endedAt) { throw 'Terminal summary native/time mismatch.' }
    if ($p.restore.mode -ceq 'Executed' -and $p.build.mode -ceq 'Executed' -and [DateTimeOffset]$p.build.startedAt -lt [DateTimeOffset]$p.restore.endedAt) { throw 'Restore/build order contradiction.' }
    Assert-GateInputs $p.restore.outputs
    Assert-GateInputs $p.build.outputs
    if (-not $p.compilerInputs -or -not $p.compilerTraceArtifact) { throw 'Actual compiler input trace absent.' }
    $trace=Assert-GateArtifact $p.compilerTraceArtifact $Result $Context.runRoot
    $traced=Get-GateCompilerTrace $trace
    foreach ($entry in $p.compilerInputs) {
        $match=@($traced | Where-Object { $_.path -ieq $entry.path })
        if (-not $match.Count -or @($match | Where-Object { $_.sha256 -cne $entry.sha256 }).Count) { throw 'Compiler input trace/hash mismatch.' }
        $null=Assert-GateArtifact $entry.artifact $Result $Context.runRoot
        if ($entry.artifact.sha256 -cne $entry.sha256) { throw 'Compiler input archive mismatch.' }
    }
    if (@($traced.path | Sort-Object -Unique).Count -ne $p.compilerInputs.Count) { throw 'Incomplete actual compiler closure.' }
    Assert-GateInputs $p.compilerInputs
    $assembly=@($p.build.outputs | Where-Object { $_.path -ieq $p.assembly })
    if ($assembly.Count -ne 1) { throw 'Build target binary absent.' }
    if ($Result.kind -ceq 'dotnet-test') {
        Assert-GateStage $p.test $Result $Context 'test'
        if ($p.build.mode -ceq 'Executed' -and [DateTimeOffset]$p.test.startedAt -lt [DateTimeOffset]$p.build.endedAt) { throw 'Build/test order contradiction.' }
        Assert-GateEqual $p.loadedBefore $p.loadedAfter 'loaded binary stability'
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
        $inputs=Save-GateFiles $Result $Context.runRoot @($oldStage.inputs.path) ('reuse-'+$name+'-inputs')
        $outputs=Save-GateFiles $Result $Context.runRoot @($oldStage.outputs.path) ('reuse-'+$name+'-outputs')
        $reused[$name]=[pscustomobject]@{name=$name; mode='Reused'; runId=$Result.runId; resultId=$Result.resultId; command=$null; startedAt=$null; endedAt=$null; processExitCode=$null; inputs=$inputs; inputFingerprint=Get-GateFingerprint $inputs; outputs=$outputs; outputFingerprint=Get-GateFingerprint $outputs; reuse=[pscustomobject]@{manifestArtifact=$manifestArtifact; originalRunId=$oldStage.runId; originalResultId=$oldStage.resultId; receipt=$oldStage}}
    }
    if ($NeedBuild) {
        if (-not $prior.compilerTraceArtifact -or -not $prior.compilerInputs.Count) { throw 'Reuse compiler trace missing.' }
        $priorTrace=Assert-GateArtifact $prior.compilerTraceArtifact $oldResult $priorRoot
        $currentTrace=Join-Path (Get-GateNodeRoot $Context.runRoot $Result.resultId) 'reused-compiler.txt'
        Copy-Item -LiteralPath $priorTrace -Destination $currentTrace
        $reused.compilerTraceArtifact=Add-GateArtifact $Result $Context.runRoot $currentTrace 'compiler-trace'
        Assert-GateInputs $prior.compilerInputs
        $reused.compilerInputs=Save-GateFiles $Result $Context.runRoot @($prior.compilerInputs.path) 'compiler-input'
    }
    return [pscustomobject]$reused
}

function Get-GateCompilerTrace {
    param([string]$Path)
    if (-not [IO.File]::Exists($Path)) { throw 'Missing compiler input trace.' }
    $entries=@()
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        if (-not $line.Trim()) { continue }
        $parts=$line.Split('|')
        if ($parts.Count -ne 2 -or $parts[1] -notmatch '^[0-9A-Fa-f]{64}$' -or -not [IO.Path]::IsPathRooted($parts[0])) { throw 'Malformed compiler input trace.' }
        $entries+=[pscustomobject]@{path=[IO.Path]::GetFullPath($parts[0]); sha256=$parts[1].ToLowerInvariant()}
    }
    if (-not $entries.Count) { throw 'Empty compiler input trace.' }
    return ,$entries
}

function Save-GateCompilerTarget {
    param([string]$Path)
    # MSBuild's built-in GetFileHash runs immediately before each CoreCompile,
    # including referenced projects. No extra SDK, package or repository target.
    $text=@'
<Project>
  <Target Name="AbilityKitCaptureCompilerInputs" BeforeTargets="CoreCompile">
    <ItemGroup>
      <AbilityKitCompilerInput Include="$(MSBuildProjectFullPath);$(MSBuildAllProjects);@(Compile->'%(FullPath)');@(ReferencePath->'%(FullPath)');@(Analyzer->'%(FullPath)');@(AdditionalFiles->'%(FullPath)');@(EmbeddedResource->'%(FullPath)')" />
      <AbilityKitCompilerInput Remove="@(AbilityKitCompilerInput)" Condition="!Exists('%(Identity)')" />
    </ItemGroup>
    <GetFileHash Files="@(AbilityKitCompilerInput)" Algorithm="SHA256">
      <Output TaskParameter="Items" ItemName="AbilityKitHashedCompilerInput" />
    </GetFileHash>
    <WriteLinesToFile File="$(AbilityKitCompilerTracePath)" Lines="@(AbilityKitHashedCompilerInput->'%(FullPath)|%(FileHash)')" Overwrite="false" Encoding="UTF-8" />
  </Target>
</Project>
'@
    [IO.File]::WriteAllText($Path,$text,[Text.UTF8Encoding]::new($false))
}

function Invoke-GateDotNet {
    param($Context, $Result, $Step, [switch]$NoBuild, [switch]$NoRestore, [string]$ReuseManifestPath)
    $Result.times.invokedAt=[DateTimeOffset]::UtcNow.ToString('o')
    $fixtureMissing=$Context.control -and ($Context.control.mode -ceq 'missing-tool' -or ($Context.control.mode -ceq 'optional-missing-tool' -and $Result.kind -ceq 'dotnet-test'))
    if ((-not $Context.dotnetPath -and -not $Context.control) -or $fixtureMissing) {
        Set-GateTerminal $Result $(if ($Step.skipPolicy -ceq 'MissingTool' -and $Step.required -eq $false) { 'Skipped' } else { 'Blocked' }) 'MissingTool'
        return
    }
    $Result.provenance=[pscustomobject][ordered]@{schemaVersion=1; example=[bool]$Context.allowExample; project=$Result.configuration.project; tfm=$Result.configuration.tfm; configuration=$Context.configuration; configSha256=$Result.configuration.configSha256; sdk=$null; source=$null; closure=@(); invocations=@(); restore=$null; build=$null; test=$null; assembly=$null; compilerTraceArtifact=$null; compilerInputs=@(); loadedBefore=@(); loadedAfter=@(); trxBinding=$null}
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
        $before=Get-GateSource $Context.repoRoot $closure.inputPaths $Context.exclusions
        $Result.source=[pscustomobject]@{before=$before; after=$null}
        $initialInputs=Save-GateFiles $Result $Context.runRoot $closure.inputPaths 'input'
        $skipBuild=[bool]$NoBuild -and $Result.kind -ceq 'dotnet-test'
        $reused=$null
        if ($NoRestore -or $skipBuild) { $reused=Use-GateReuse $Context $Result $ReuseManifestPath $sdk $skipBuild }
        $restoreArgs=@('restore',$Result.configuration.project,('-p:Configuration='+$Context.configuration),('-p:TargetFramework='+$Result.configuration.tfm),'--nologo')
        if ($NoRestore -or $skipBuild) { $Result.provenance.restore=$reused.restore }
        else {
            $native=Invoke-GateTool $Context $Result $restoreArgs 'restore'
            Assert-GateInputs $before.inputs
            $assets=Save-GateFiles $Result $Context.runRoot @($closure.projects.assetsPath) 'assets'
            $Result.provenance.restore=New-GateStage 'restore' $native $initialInputs $assets $Result
        }
        # Restore can introduce package imports/analyzers. Freeze the resulting
        # evaluated source plus the exact package resolution before building.
        $restored=Get-GateProjectClosure $Context $Result $Result.configuration.project 'restored'
        $packagePaths=Get-GatePackageInputs $restored.projects
        $inputPaths=@($restored.inputPaths+$packagePaths | Sort-Object -Unique)
        Assert-GateInputs $before.inputs
        $source=Get-GateSource $Context.repoRoot $inputPaths $Context.exclusions
        $Result.source.before=$source; $Result.provenance.source=$source; $Result.provenance.closure=$restored.projects
        $buildInputs=Save-GateFiles $Result $Context.runRoot @($inputPaths+$restored.projects.assetsPath | Sort-Object -Unique) 'build-input'
        $assembly=$restored.projects[0].targetPath; $Result.provenance.assembly=$assembly
        if ($skipBuild) {
            $Result.provenance.build=$reused.build
            $Result.provenance.compilerInputs=$reused.compilerInputs
            $Result.provenance.compilerTraceArtifact=$reused.compilerTraceArtifact
        }
        else {
            $targetPath=Join-Path $nodeRoot 'compiler.targets'; $tracePath=Join-Path $nodeRoot 'compiler.txt'
            Save-GateCompilerTarget $targetPath
            $null=Add-GateArtifact $Result $Context.runRoot $targetPath 'compiler-target'
            $args=@('build',$Result.configuration.project,'-c',$Context.configuration,'-f',$Result.configuration.tfm,'--no-restore','--nologo',('-p:CustomAfterMicrosoftCommonTargets='+$targetPath),('-p:AbilityKitCompilerTracePath='+$tracePath))
            $native=Invoke-GateTool $Context $Result $args 'build'
            Assert-GateInputs $source.inputs; Assert-GateInputs $Result.provenance.restore.outputs
            $traced=Get-GateCompilerTrace $tracePath
            $Result.provenance.compilerTraceArtifact=Add-GateArtifact $Result $Context.runRoot $tracePath 'compiler-trace'
            $Result.provenance.compilerInputs=Save-GateFiles $Result $Context.runRoot @($traced.path) 'compiler-input'
            foreach ($entry in $traced) {
                if ((Get-GateHash $entry.path) -cne $entry.sha256) { throw 'Compiler input changed since compile.' }
            }
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
            $native=$Result.provenance.invocations | Where-Object { $_.stdoutPath -like '*build.stdout.txt' } | Select-Object -Last 1
            $Result.command=$native.command; $Result.processExitCode=$native.processExitCode
            $Result.times.executionStartedAt=$native.startedAt; $Result.times.executionEndedAt=$native.endedAt
        }
        $afterClosure=Get-GateProjectClosure $Context $Result $Result.configuration.project 'after'
        $afterPaths=@($afterClosure.inputPaths+(Get-GatePackageInputs $afterClosure.projects) | Sort-Object -Unique)
        Assert-GateEqual $inputPaths $afterPaths 'evaluated input closure before/after'
        $Result.source.after=Get-GateSource $Context.repoRoot $afterPaths $Context.exclusions
        if ($source.sha -cne $Result.source.after.sha -or $source.inputFingerprint -cne $Result.source.after.inputFingerprint) { throw 'Source changed during invocation.' }
        Assert-GateProvenance $Result $Context
        Set-GateTerminal $Result 'Passed' $null
    } catch {
        $message=$_.Exception.Message
        $last=$Result.provenance.invocations | Select-Object -Last 1
        if ($last -and -not $message.StartsWith('Blocked:')) {
            $Result.command=$last.command; $Result.processExitCode=$last.processExitCode
            $Result.times.executionStartedAt=$last.startedAt; $Result.times.executionEndedAt=$last.endedAt
        }
        Set-GateTerminal $Result $(if ($message.StartsWith('Blocked:')) { 'Blocked' } else { 'Failed' }) $message
    } finally {
        # Failed build attempts keep valid independent restore receipts for NoRestore.
        if ($Result.provenance.restore) {
            if (-not $Result.provenance.source -and $Result.source) { $Result.provenance.source=$Result.source.before }
            $receiptPath=Join-Path $nodeRoot 'stage-receipt.json'
            [IO.File]::WriteAllText($receiptPath,(ConvertTo-GateJson $Result.provenance),[Text.UTF8Encoding]::new($false))
            $null=Add-GateArtifact $Result $Context.runRoot $receiptPath 'stage-receipt'
        }
    }
}
