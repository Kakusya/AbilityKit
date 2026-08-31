param(
    [Parameter(Mandatory = $false)]
    [string]$LubanDllPath = "..\\Tools\\Luban\\Luban.dll",

    [Parameter(Mandatory = $false)]
    [string]$LubanConfPath = "MiniTemplate\\luban.conf"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$stageJsonDir = [System.IO.Path]::GetFullPath((Join-Path $root ".generated\\json"))
$stageCodeDir = [System.IO.Path]::GetFullPath((Join-Path $root ".generated\\code"))

New-Item -ItemType Directory -Force -Path $stageJsonDir | Out-Null
New-Item -ItemType Directory -Force -Path $stageCodeDir | Out-Null

Write-Host "[export_moba_configs] CandidateJsonDir: $stageJsonDir"
Write-Host "[export_moba_configs] StageCodeDir: $stageCodeDir"

$absLubanDll = [System.IO.Path]::GetFullPath((Join-Path $root $LubanDllPath))
$absConf = [System.IO.Path]::GetFullPath((Join-Path $root $LubanConfPath))

Write-Host "[export_moba_configs] LubanDll: $absLubanDll"
Write-Host "[export_moba_configs] Conf: $absConf"

if (!(Test-Path $absLubanDll)) {
    Write-Host "[export_moba_configs] Luban dll not found: $absLubanDll"
    exit 1
}

if (!(Test-Path $absConf)) {
    Write-Host "[export_moba_configs] luban.conf not found: $absConf"
    exit 1
}

# JSON stays in staging for review. Production MOBA JSON is authored under the
# package Resources root and published to Console replicas by the sync tool.
dotnet $absLubanDll -t all -d json --conf $absConf -x outputDataDir=$stageJsonDir

# 生成 cs-newtonsoft-json 代码（使用 Newtonsoft.Json）
dotnet $absLubanDll -t all -c cs-newtonsoft-json --conf $absConf -x outputCodeDir=$stageCodeDir

Write-Host "[export_moba_configs] JSON generated as review candidate: $stageJsonDir"
Write-Host "[export_moba_configs] Promote reviewed JSON into package Resources, then run tools\\sync_moba_json_configs.ps1 -DryRun/-Apply to publish Console replicas."

# 拷贝代码到 LubanGen
$lubanGenDir = [System.IO.Path]::GetFullPath((Join-Path $root "..\..\Unity\Packages\com.abilitykit.demo.moba.runtime\Runtime\Impl\Moba\Config\LubanGen"))
New-Item -ItemType Directory -Force -Path $lubanGenDir | Out-Null
Copy-Item -Path (Join-Path $stageCodeDir "*") -Destination $lubanGenDir -Recurse -Force
Write-Host "[export_moba_configs] Code copied to LubanGen: $lubanGenDir"
