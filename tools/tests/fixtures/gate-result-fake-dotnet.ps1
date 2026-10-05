param([string]$ContextPath,[string]$ArgumentJson)
$ErrorActionPreference='Stop'
$context=Get-Content -LiteralPath $ContextPath -Raw -Encoding UTF8 | ConvertFrom-Json
$argv=[string[]]($ArgumentJson | ConvertFrom-Json)
$stage=[string]$argv[0]
[IO.File]::AppendAllText($context.launchesPath,((ConvertTo-Json -InputObject ([pscustomobject]@{stage=$stage; arguments=$argv; pid=$PID}) -Compress)+"`n"))
if ($context.mode -ceq 'timeout' -and $stage -ceq 'build') { [Console]::Out.WriteLine('owned fake build started'); Start-Sleep -Seconds 30 }
if ($context.mode -ceq 'cancel' -and $stage -ceq 'build') { [IO.File]::WriteAllText($context.signalPath,'cancel'); Start-Sleep -Seconds 30 }
if ($stage -ceq '--version') { [Console]::Out.WriteLine($context.sdk); exit 0 }
if ($stage -ceq '--info') { [Console]::Out.WriteLine('SYNTHETIC SDK/host information: 10.0.300 / 10.0.0'); exit 0 }
if ($stage -ceq '--list-sdks') { [Console]::Out.WriteLine($context.sdk+' ['+(Join-Path $context.fixtureRoot 'sdk')+']'); exit 0 }
$project=@($context.projectData | Where-Object { $_.project -ieq $argv[1] })
if ($project.Count -ne 1) { [Console]::Error.WriteLine('No isolated project data.'); exit 12 }
$p=$project[0]
if ($stage -ceq 'msbuild') {
    if (@($argv | Where-Object { $_ -like '-target:*_GetRestoreSettings*' }).Count) {
        $value=[pscustomobject]@{Properties=[pscustomobject]@{MSBuildProjectFullPath=$p.project; TargetFramework=$p.tfm; TargetFrameworks=''; _OutputConfigFilePaths=($p.configPaths -join ';'); ProjectAssetsFile=$p.assetsPath; MSBuildProjectExtensionsPath=$p.extensionsPath; RestoreTaskAssemblyFile=$(if ($context.mode -ceq 'settings-task-missing') { 'Absent.Build.Tasks.dll' } else { 'NuGet.Build.Tasks.dll' }); NuGetRestoreTargets=$(if ($context.mode -ceq 'settings-target-missing') { Join-Path $context.fixtureRoot 'sdk/10.0.300/Absent.targets' } else { Join-Path $context.fixtureRoot 'sdk/10.0.300/NuGet.targets' })}}
        [Console]::Out.WriteLine((ConvertTo-Json -InputObject $value -Depth 30 -Compress)); exit 0
    }
    $generatedImports=@(foreach ($name in @('.nuget.g.props','.nuget.g.targets')) { $file=Join-Path $p.extensionsPath ((Split-Path $p.project -Leaf)+$name); if ([IO.File]::Exists($file)) { $file } })
    $preprocess=@($argv | Where-Object { $_ -like '-preprocess:*' })
    if ($preprocess.Count) {
        $path=$preprocess[0].Substring('-preprocess:'.Length)
        $commentLines=@('<Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk">', 'This import was added implicitly because the Project element''s Sdk attribute specified "Microsoft.NET.Sdk".', '<Import Project="$(AlternateCommonProps)" Condition="''$(AlternateCommonProps)'' != ''''" />')
        $commentLines+=@($p.imports)+@($p.preprocessedOnlyImports)+$generatedImports
        [IO.File]::WriteAllText($path,('<Project><!--'+"`n"+($commentLines -join "`n")+"`n"+'--></Project>'))
        exit 0
    }
    $items=[ordered]@{}
    foreach ($key in @('Compile','ProjectReference','PackageReference','Analyzer','AdditionalFiles','EmbeddedResource','Content','None','Reference','EditorConfigFiles')) { $items[$key]=@() }
    $items.Compile=@(foreach ($file in $p.compile) { [pscustomobject]@{Identity=$file; FullPath=$file} })
    $items.ProjectReference=@(foreach ($file in $p.references) { [pscustomobject]@{Identity=$file; FullPath=$file} })
    $tfm=if ($context.mode -ceq 'evaluated-tfm') { 'net9.0' } else { $p.tfm }
    $value=[pscustomobject]@{Properties=[pscustomobject]@{MSBuildProjectFullPath=$p.project; MSBuildAllProjects=(@($p.imports)+$generatedImports -join ';'); TargetFramework=$tfm; TargetPath=$p.targetPath; ProjectAssetsFile=$p.assetsPath; AssemblyName=$p.assemblyName; NuGetPackageRoot=$p.packageRoot}; Items=[pscustomobject]$items}
    [Console]::Out.WriteLine((ConvertTo-Json -InputObject $value -Depth 30 -Compress)); exit 0
}
if ($stage -ceq 'restore') {
    $globalTfm=@($argv | Where-Object { $_ -like '-p:TargetFramework=*' })
    foreach ($item in $context.projectData) {
        $null=New-Item -ItemType Directory -Path (Split-Path $item.assetsPath) -Force
        $tfm=if ($globalTfm.Count) { $globalTfm[0].Substring('-p:TargetFramework='.Length) } else { $item.tfm }
        if ($context.mode -ceq 'restore-wrong-reference-tfm' -and $item.project -ine $p.project) { $tfm='net9.0' }
        $frameworks=[pscustomobject]@{($tfm)=[pscustomobject]@{}}
        $restore=[pscustomobject]@{projectUniqueName=$item.project; projectPath=$item.project; originalTargetFrameworks=@($tfm); configFilePaths=@($item.configPaths)}
        $value=[pscustomobject]@{version=3; targets=$frameworks; project=[pscustomobject]@{restore=$restore; frameworks=$frameworks}; libraries=[pscustomobject]@{'Fixture/1.0.0'=[pscustomobject]@{type='package'; path='fixture/1.0.0'; files=@('tool.opaque')}}; packageFolders=[pscustomobject]@{($item.packageRoot)=[pscustomobject]@{}}}
        if ($context.mode -cne 'restore-missing-reference' -or $item.project -ieq $p.project) { [IO.File]::WriteAllText($item.assetsPath,(ConvertTo-Json -InputObject $value -Depth 30 -Compress)) }
        foreach ($extension in @('.nuget.g.props','.nuget.g.targets')) { [IO.File]::WriteAllText((Join-Path $item.extensionsPath ((Split-Path $item.project -Leaf)+$extension)),('<Project><!-- recorded restore output '+$tfm+' --></Project>')) }
    }
    if ($context.mode -ceq 'config-change-restore') { [IO.File]::AppendAllText($p.configPaths[0],'changed effective config') }
    if ($context.mode -ceq 'source-change-restore') { [IO.File]::AppendAllText($p.compile[0],'changed source during restore') }
    if ($context.mode -ceq 'sdk-nuget-change-restore') { [IO.File]::AppendAllText((Join-Path $context.fixtureRoot 'sdk/10.0.300/NuGet.Configuration.dll'),'changed NuGet settings tool') }
    if ($context.mode -ceq 'sdk-change-restore') { [IO.File]::AppendAllText((Join-Path $context.fixtureRoot 'sdk/10.0.300/MSBuild.dll'),'changed SDK') }
    [Console]::Out.WriteLine('restore evidence'); exit 0
}
if ($stage -ceq 'build') {
    [Console]::Out.WriteLine('stdout:'+('O'*8192)); [Console]::Error.WriteLine('stderr:'+('E'*8192))
    $builds=@(Get-Content -LiteralPath $context.launchesPath | ForEach-Object { $_ | ConvertFrom-Json } | Where-Object { $_.stage -ceq 'build' }).Count
    if ($context.mode -ceq 'build-fail' -or ($context.mode -ceq 'build-fail-once' -and $builds -eq 1)) { [Console]::Error.WriteLine('intentional build failure'); exit 7 }
    if ($context.mode -ceq 'build-warning-zero') { [Console]::Error.WriteLine('WARNING: required evidence unavailable; skipping'); exit 0 }
    if ([IO.File]::Exists($p.assetsPath)) {
        $assets=Get-Content -LiteralPath $p.assetsPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($p.tfm -cnotin @($assets.project.frameworks.PSObject.Properties.Name)) { [Console]::Error.WriteLine('NETSDK1005: own project TFM missing from synthetic restore assets'); exit 1 }
    }
    foreach ($item in $context.projectData) {
        $null=New-Item -ItemType Directory -Path (Split-Path $item.targetPath) -Force
        [IO.File]::WriteAllText($item.targetPath,'SYNTHETIC BINARY '+$item.assemblyName)
        [IO.File]::WriteAllText((Join-Path (Split-Path $item.targetPath) 'dependency.dll'),'SYNTHETIC DEPENDENCY')
    }
    $traceArg=@($argv | Where-Object { $_ -like '-p:AbilityKitCompilerTracePath=*' })
    if ($traceArg.Count -ne 1) { throw 'Fake compile requires explicit trace output.' }
    $tracePath=$traceArg[0].Substring('-p:AbilityKitCompilerTracePath='.Length)
    $generated=Join-Path (Split-Path $p.assetsPath) 'Generated.opaque'
    [IO.File]::WriteAllText($generated,'generated actual compiler input')
    $traceInputs=@($generated,$p.project)+@($p.compile)+@($p.imports)
    $trace=@(foreach ($file in $traceInputs) { $file+'|'+(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }) -join "`n"
    [IO.File]::WriteAllText($tracePath,$trace)
    if ($context.mode -ceq 'config-change-build') { [IO.File]::AppendAllText($p.configPaths[0],'changed config during build') }
    if ($context.mode -ceq 'source-change') { [IO.File]::AppendAllText($p.compile[0],'changed') }
    if ($context.mode -ceq 'import-change') { [IO.File]::AppendAllText($p.preprocessedOnlyImports[0],'changed import') }
    exit 0
}
if ($stage -ceq 'test') {
    $pos=[Array]::IndexOf($argv,'--results-directory'); $directory=$argv[$pos+1]; $trx=Join-Path $directory 'test-results.trx'
    if ($context.mode -ceq 'test-missing') { exit 0 }
    if ($context.mode -ceq 'test-empty') { [IO.File]::WriteAllText($trx,''); exit 0 }
    if ($context.mode -ceq 'test-malformed') { [IO.File]::WriteAllText($trx,'<broken>'); exit 0 }
    $outcome='Passed'; $summary='Completed'; $passed=2; $failed=0; $notExecuted=0; $executed=2; $total=2
    if ($context.mode -ceq 'test-failed-zero') { $outcome='Failed'; $summary='Failed'; $passed=0; $failed=2 }
    if ($context.mode -ceq 'test-skipped') { $outcome='NotExecuted'; $passed=0; $notExecuted=2; $executed=0 }
    if ($context.mode -ceq 'test-lowercase') { $outcome='passed' }
    if ($context.mode -ceq 'test-counter') { $passed=3 }
    if ($context.mode -ceq 'test-zero') { $passed=0; $total=0; $executed=0 }
    $start=[DateTimeOffset]::UtcNow.ToString('o'); $finish=$start
    if ($context.mode -ceq 'test-stale') { $start='2000-01-01T00:00:00Z'; $finish=$start }
    $assembly=$p.targetPath
    if ($context.mode -ceq 'test-wrong-assembly') { $assembly=Join-Path (Split-Path $assembly) 'wrong.dll' }
    function XmlEscape([string]$s) { return [Security.SecurityElement]::Escape($s) }
    $results=''; $definitions=''
    if ($context.mode -cne 'test-no-entries' -and $context.mode -cne 'test-zero') {
        $previousExecution=$null
        for ($i=0; $i -lt 2; $i++) {
            $testId=[guid]::NewGuid().ToString(); $execution=[guid]::NewGuid().ToString()
            if ($context.mode -ceq 'test-duplicate' -and $previousExecution) { $execution=$previousExecution }; $previousExecution=$execution
            $results+='<UnitTestResult testId="'+$testId+'" executionId="'+$execution+'" testName="Fixture.Case'+$i+'" outcome="'+$outcome+'" />'
            $definitions+='<UnitTest id="'+$testId+'" name="Fixture.Case'+$i+'" storage="'+(XmlEscape $assembly)+'"><Execution id="'+$execution+'" /><TestMethod codeBase="'+(XmlEscape $assembly)+'" name="Case'+$i+'" /></UnitTest>'
        }
    }
    if ($context.mode -ceq 'test-no-definition') { $definitions='' }
    $completed=if ($context.mode -ceq 'test-completed-counter') { 2 } else { 0 }
    $text='<TestRun id="'+[guid]::NewGuid().ToString()+'"><Times start="'+$start+'" finish="'+$finish+'" /><Results>'+$results+'</Results><TestDefinitions>'+$definitions+'</TestDefinitions><ResultSummary outcome="'+$summary+'"><Counters total="'+$total+'" executed="'+$executed+'" passed="'+$passed+'" failed="'+$failed+'" notExecuted="'+$notExecuted+'" completed="'+$completed+'" /></ResultSummary></TestRun>'
    [IO.File]::WriteAllText($trx,$text)
    if ($context.mode -ceq 'test-replaced-dll') { [IO.File]::WriteAllText($p.targetPath,'REPLACED') }
    if ($context.mode -ceq 'test-native-fail') { exit 2 }
    [Console]::Out.WriteLine('two actual synthetic TRX entries'); exit 0
}
[Console]::Error.WriteLine('Unsupported fake command.'); exit 13
