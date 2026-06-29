# Phase 3: run Master + Room servers, then print §4 log signals.
# Usage: edit $RoomName if needed; the build output path is fixed for this project.
$base = "C:\J_0\XRCollabDemo\Builds\App\Server\StandaloneWindows64"
$ms   = "$base\MasterAndSpawner"
$rm   = "$base\Room"

Remove-Item "$ms\master.log","$rm\room.log" -ErrorAction SilentlyContinue
Start-Process "$ms\MasterAndSpawner.exe" -WorkingDirectory $ms -ArgumentList "-logFile","$ms\master.log"
"Master launched: " + (Get-Date -Format HH:mm:ss)
Start-Sleep -Seconds 6
Start-Process "$rm\Room.exe" -WorkingDirectory $rm -ArgumentList "-logFile","$rm\room.log"
"Room launched: " + (Get-Date -Format HH:mm:ss)
Start-Sleep -Seconds 8

"===== master.log signals ====="
Select-String -Path "$ms\master.log" -Pattern "listening to","Successfully initialized modules","Spawner successfully created" |
  ForEach-Object { $_.Line.Trim() } | Select-Object -Unique
""
"===== room.log signals ====="
Select-String -Path "$rm\room.log" -Pattern "Online Scene","Room Server started","Room registered successfully" |
  ForEach-Object { $_.Line.Trim() } | Select-Object -First 10

# Cleanup later:  Stop-Process -Name Room,MasterAndSpawner -Force
