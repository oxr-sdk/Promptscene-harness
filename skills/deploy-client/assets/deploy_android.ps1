# deploy_android.ps1 — install + launch an XRCollabDemo client APK on a connected Android/Quest device.
# Waits out the recurring 'unauthorized' state (user must accept USB-debugging in-headset).
# Usage: pwsh deploy_android.ps1 -Apk <path> [-Pkg com.kisti.xrcollabdemo] [-UnityVer 6000.3.11f1]
param(
  [string]$Apk = "C:\J_0\XRCollabDemo\Builds\App\Client\Meta-v1.3.4\XRCollabDemo.apk",
  [string]$Pkg = "com.kisti.xrcollabdemo",
  [string]$UnityVer = "6000.3.11f1"
)
$adb = "C:\Program Files\Unity\Hub\Editor\$UnityVer\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
if (-not (Test-Path $adb)) { Write-Error "adb not found: $adb"; exit 1 }
if (-not (Test-Path $Apk)) { Write-Error "APK not found: $Apk"; exit 1 }

# 1) wait for authorization (Quest reverts to 'unauthorized' after sleep/replug/kill-server)
Write-Host "Waiting for device authorization (accept 'Allow USB debugging' in the headset)..."
$st = ""
for ($i=0; $i -lt 60; $i++) {
  $line = (& $adb devices | Select-String -NotMatch "List of devices" | Select-Object -First 1)
  if ($line) { $st = ($line -split "\s+")[1] }
  if ($st -eq "device") { Write-Host "AUTHORIZED"; break }
  Start-Sleep -Seconds 5
}
if ($st -ne "device") { Write-Error "device still '$st' — not authorized"; exit 1 }

# 2) install (-r updates same package in place). Signature clash => rename package + rebuild (do NOT uninstall).
Write-Host "=== install -r ==="
$out = & $adb install -r $Apk 2>&1
$out | Select-Object -Last 4
if ($out -match "INSTALL_FAILED_UPDATE_INCOMPATIBLE") {
  Write-Warning "Signature clash. Rebuild with a different applicationIdentifier (see build_client.cs APP_ID) — do not uninstall the existing app."
  exit 2
}

# 3) launch + confirm
Write-Host "=== launch ==="
& $adb logcat -c
& $adb shell monkey -p $Pkg -c android.intent.category.LAUNCHER 1 | Select-Object -Last 1
Start-Sleep -Seconds 6
$appPid = (& $adb shell pidof $Pkg)
if ($appPid) { Write-Host "RUNNING pid=$appPid" } else { Write-Warning "process not found" }

# 4) quick connection check (grep logcat for the master-connect line)
Write-Host "=== connection ==="
& $adb logcat -d | Select-String "ClientToMasterConnector|connected to server" | Select-Object -Last 5
