# Creates isolated TEMP project data consumed by a real fake-tool subprocess.
function New-GateControlFixture {
    param([string]$Executor,[string]$Mode='valid',[string]$Shape='pair')
    $fixtureRoot=Join-Path ([IO.Path]::GetTempPath()) ('abilitykit-gate-control-'+[guid]::NewGuid().ToString())
    $null=New-Item -ItemType Directory -Path $fixtureRoot
    $sdkRoot=Join-Path $fixtureRoot 'sdk/10.0.300'
    $null=New-Item -ItemType Directory -Path (Join-Path $sdkRoot 'Roslyn/bincore') -Force
    foreach ($name in @('MSBuild.dll','Microsoft.Build.dll','Microsoft.Build.Framework.dll','Microsoft.Build.Tasks.Core.dll','Microsoft.Build.Utilities.Core.dll','MSBuild.deps.json','MSBuild.runtimeconfig.json','Roslyn/bincore/csc.dll')) {
        [IO.File]::WriteAllText((Join-Path $sdkRoot $name),'SYNTHETIC SDK INPUT '+$name)
    }
    foreach ($name in @('NuGet.Build.Tasks.dll','NuGet.Configuration.dll')) { [IO.File]::WriteAllText((Join-Path $sdkRoot $name),'synthetic SDK NuGet task binary '+$name) }
    [IO.File]::WriteAllText((Join-Path $sdkRoot 'NuGet.targets'),'<Project><UsingTask TaskName="NuGet.Build.Tasks.GetRestoreSettingsTask" AssemblyFile="$(RestoreTaskAssemblyFile)" /></Project>')
    $task=[pscustomobject]@{dependencies=[pscustomobject]@{'NuGet.Configuration'='1.0.0'}; runtime=[pscustomobject]@{'lib/net10.0/NuGet.Build.Tasks.dll'=[pscustomobject]@{}}}
    $configuration=[pscustomobject]@{runtime=[pscustomobject]@{'lib/net10.0/NuGet.Configuration.dll'=[pscustomobject]@{}}}
    $metadata=[pscustomobject]@{targets=[pscustomobject]@{'.NETCoreApp,Version=v10.0'=[pscustomobject]@{'NuGet.Build.Tasks/1.0.0'=$task;'NuGet.Configuration/1.0.0'=$configuration}}}
    [IO.File]::WriteAllText((Join-Path $sdkRoot 'MSBuild.deps.json'),(ConvertTo-GateJson $metadata))
    $packageRoot=Join-Path $fixtureRoot 'packages'; $null=New-Item -ItemType Directory -Path (Join-Path $packageRoot 'fixture/1.0.0') -Force
    [IO.File]::WriteAllText((Join-Path $packageRoot 'fixture/1.0.0/tool.opaque'),'immutable package analyzer input')
    $shared=Join-Path $fixtureRoot 'Unity/Packages/fixture/source.unusual'
    $null=New-Item -ItemType Directory -Path (Split-Path $shared) -Force
    [IO.File]::WriteAllText($shared,'actual linked Compile Include with no extension whitelist')
    $import=Join-Path $fixtureRoot 'custom-import.opaque'; [IO.File]::WriteAllText($import,'<Project />')
    $configPath=Join-Path $fixtureRoot 'effective-settings/NuGet.Config'
    $null=New-Item -ItemType Directory -Path (Split-Path $configPath)
    [IO.File]::WriteAllText($configPath,'<configuration><!-- synthetic credential-free settings --></configuration>')
    $data=@()
    foreach ($name in @('Library','Tests')) {
        $directory=Join-Path $fixtureRoot $name; $null=New-Item -ItemType Directory -Path $directory
        $project=Join-Path $directory ($name+'.csproj')
        [IO.File]::WriteAllText($project,'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
        $source=Join-Path $directory 'Code.cs'; [IO.File]::WriteAllText($source,'class Fixture {}')
        $target=Join-Path $directory ('bin/Debug/net10.0/'+$name+'.dll')
        $assets=Join-Path $directory 'obj/project.assets.json'
        $data+=[pscustomobject]@{project=$project; tfm='net10.0'; targetPath=$target; assetsPath=$assets; assemblyName=$name; compile=@($source,$shared); imports=@($import); references=@(); packageRoot=$packageRoot; configPaths=@($configPath); extensionsPath=(Join-Path $directory 'obj')}
    }
    $data[1].references=@($data[0].project)
    if ($Shape -ceq 'mixed') {
        $data[0].tfm='netstandard2.0'; $data[0].targetPath=$data[0].targetPath.Replace('net10.0','netstandard2.0')
        [IO.File]::WriteAllText($data[0].project,'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.0</TargetFramework></PropertyGroup></Project>')
    }
    $build=[pscustomobject]@{name='Build'; kind='dotnet-build'; project=$data[0].project; tfm=$data[0].tfm; coverage=@('build-library')}
    $test=[pscustomobject]@{name='Tests'; kind='dotnet-test'; project=$data[1].project; tfm='net10.0'; filter='Gate=CookingKitchenLoop'; coverage=@('test-project')}
    $gates=@([pscustomobject]@{name='control'; requiredCoverage=@('build-library','test-project'); optionalCoverage=@(); steps=@($build,$test)})
    if ($Shape -ceq 'test') { $gates[0].requiredCoverage=@('test-project'); $gates[0].steps=@($test) }
    if ($Shape -ceq 'build') { $gates[0].requiredCoverage=@('build-library'); $gates[0].steps=@($build) }
    if ($Shape -ceq 'nested') {
        $inner=[pscustomobject]@{name='inner'; requiredCoverage=@('build-library','test-project'); optionalCoverage=@(); steps=@($build,$test)}
        $gates=@([pscustomobject]@{name='control'; requiredCoverage=@('build-library','test-project'); optionalCoverage=@(); steps=@([pscustomobject]@{name='Nested'; kind='gate'; gate='inner'})},$inner)
    }
    if ($Shape -ceq 'repeated') {
        $inner=[pscustomobject]@{name='inner'; requiredCoverage=@('build-library'); optionalCoverage=@(); steps=@($build)}
        # Repeated invocations legitimately bind the same token to different leaves.
        $gates=@([pscustomobject]@{name='control'; requiredCoverage=@('build-library'); optionalCoverage=@(); steps=@([pscustomobject]@{name='Nested'; kind='gate'; gate='inner'},[pscustomobject]@{name='Nested'; kind='gate'; gate='inner'})},$inner)
    }
    if ($Shape -ceq 'optional') {
        $test | Add-Member -NotePropertyName required -NotePropertyValue $false
        $test | Add-Member -NotePropertyName skipPolicy -NotePropertyValue 'MissingTool'
        $gates[0].requiredCoverage=@('build-library'); $gates[0].optionalCoverage=@('test-project')
    }
    $context=[pscustomobject]@{schemaVersion=1; example=$true; fixtureRoot=$fixtureRoot; executor=$Executor; executorSha256=Get-GateHash $Executor; mode=$Mode; sdk='10.0.300'; projectData=$data; launchesPath=(Join-Path $fixtureRoot 'launches.jsonl'); signalPath=(Join-Path $fixtureRoot 'cancel.signal')}
    $contextPath=Join-Path $fixtureRoot 'context.json'
    $configPath=Join-Path $fixtureRoot 'config.json'
    [IO.File]::WriteAllText($contextPath,(ConvertTo-GateJson $context))
    [IO.File]::WriteAllText($configPath,(ConvertTo-GateJson ([pscustomobject]@{schemaVersion=1; example=$true; defaultGate='control'; gates=$gates})))
    return [pscustomobject]@{root=$fixtureRoot; context=$context; contextPath=$contextPath; configPath=$configPath; config=[pscustomobject]@{schemaVersion=1; example=$true; defaultGate='control'; gates=$gates}; data=$data}
}

function Save-GateControlFixture {
    param($Fixture)
    [IO.File]::WriteAllText($Fixture.contextPath,(ConvertTo-GateJson $Fixture.context))
    [IO.File]::WriteAllText($Fixture.configPath,(ConvertTo-GateJson $Fixture.config))
}

function New-GateRealCompilerFixture {
    param([string]$Root)
    $Root=[IO.Path]::GetFullPath($Root)
    if (Test-Path -LiteralPath $Root) { throw 'Real compiler fixture root must be new.' }
    $null=New-Item -ItemType Directory -Path $Root
    foreach ($name in @('Shared','Left','Right')) { $null=New-Item -ItemType Directory -Path (Join-Path $Root $name) }
    $shared=@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><Flavor Condition="'$(Flavor)' == ''">Default</Flavor><AssemblyName>Shared.$(Flavor)</AssemblyName><DefaultItemExcludes>$(DefaultItemExcludes);obj/**;bin/**</DefaultItemExcludes></PropertyGroup>
</Project>
'@
    $props=@'
<Project>
  <PropertyGroup Condition="'$(MSBuildProjectName)' == 'Shared'">
    <BaseOutputPath>bin/$(Flavor)/</BaseOutputPath>
    <BaseIntermediateOutputPath>obj/$(Flavor)/</BaseIntermediateOutputPath>
    <MSBuildProjectExtensionsPath>obj/$(Flavor)/</MSBuildProjectExtensionsPath>
  </PropertyGroup>
</Project>
'@
    $side=@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><Flavor>__SIDE__</Flavor></PropertyGroup>
  <ItemGroup><ProjectReference Include="../Shared/Shared.csproj" AdditionalProperties="Flavor=__SIDE__" /></ItemGroup>
</Project>
'@
    $rootProject=@'
<Project>
  <ItemGroup>
    <BuildProject Include="Left/Left.csproj"><AdditionalProperties>Flavor=Left</AdditionalProperties></BuildProject>
    <BuildProject Include="Right/Right.csproj"><AdditionalProperties>Flavor=Right;AbilityKitCompilerCaptureDisabled=$(BypassSecondCapture)</AdditionalProperties></BuildProject>
  </ItemGroup>
  <Target Name="Restore" Condition="'$(SkipFixtureRestore)' != 'true'"><MSBuild Projects="@(BuildProject)" Targets="Restore" BuildInParallel="true" Properties="Configuration=$(Configuration)" /></Target>
  <Target Name="Build" DependsOnTargets="Restore"><MSBuild Projects="@(BuildProject)" Targets="Build" BuildInParallel="true" Properties="Configuration=$(Configuration)" /></Target>
</Project>
'@
    $generated=@'
<Project>
  <Target Name="AbilityKitGenerateFixtureInput" BeforeTargets="AbilityKitCaptureCompilerInputs">
    <MakeDir Directories="$(IntermediateOutputPath)" />
    <WriteLinesToFile File="$(IntermediateOutputPath)Generated.$(MSBuildProjectName).$(Flavor).g.cs" Lines="namespace CompilerFixture { internal static class Generated_$(MSBuildProjectName)_$(Flavor) { } }" Overwrite="true" Encoding="UTF-8" Condition="!Exists('$(IntermediateOutputPath)Generated.$(MSBuildProjectName).$(Flavor).g.cs')" />
    <ItemGroup><Compile Include="$(IntermediateOutputPath)Generated.$(MSBuildProjectName).$(Flavor).g.cs" /></ItemGroup>
  </Target>
</Project>
'@
    $barrier=@'
param([string]$Root,[string]$CaptureId,[string]$Project,[string]$Mode)
$ErrorActionPreference='Stop'
$null=New-Item -ItemType Directory -Path $Root -Force
[IO.File]::WriteAllText((Join-Path $Root ($CaptureId+'.ready')),$Project)
$deadline=[DateTime]::UtcNow.AddSeconds(12)
while (@(Get-ChildItem -LiteralPath $Root -Filter '*.ready').Count -lt 2) {
    if ([DateTime]::UtcNow -gt $deadline) { throw 'BarrierTimeout' }
    Start-Sleep -Milliseconds 25
}
if ($Mode -ceq 'OldSharedAppend') {
    $leader=Join-Path $Root 'leader'
    try { $claim=[IO.File]::Open($leader,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None); $claim.Dispose(); $isLeader=$true } catch [IO.IOException] { $isLeader=$false }
    $locked=Join-Path $Root 'locked'; $shared=Join-Path $Root 'legacy-shared.txt'
    if ($isLeader) {
        $stream=[IO.File]::Open($shared,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try { [IO.File]::WriteAllText($locked,'locked'); Start-Sleep -Seconds 3 } finally { $stream.Dispose() }
    } else {
        while (-not [IO.File]::Exists($locked)) { if ([DateTime]::UtcNow -gt $deadline) { throw 'LegacyLockTimeout' }; Start-Sleep -Milliseconds 20 }
        [IO.File]::AppendAllText($shared,$CaptureId)
    }
}
'@
    [IO.File]::WriteAllText((Join-Path $Root 'Root.proj'),$rootProject,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $Root 'Directory.Build.props'),$props,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $Root 'Directory.Build.targets'),$generated,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $Root 'barrier.ps1'),$barrier,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $Root 'Shared/Shared.csproj'),$shared,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $Root 'Shared/Shared.cs'),'namespace CompilerFixture; public static class SharedValue { public static int Value => 1; }',[Text.UTF8Encoding]::new($false))
    foreach ($name in @('Left','Right')) {
        [IO.File]::WriteAllText((Join-Path $Root ($name+'/'+$name+'.csproj')),$side.Replace('__SIDE__',$name),[Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $Root ($name+'/'+$name+'.cs')),('namespace CompilerFixture; public static class '+$name+'Value { public static int Value => SharedValue.Value; }'),[Text.UTF8Encoding]::new($false))
    }
    return [pscustomobject]@{root=$Root;project=(Join-Path $Root 'Root.proj');barrier=(Join-Path $Root 'barrier.ps1')}
}
