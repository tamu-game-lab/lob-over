# WO-L1: USB is optional; Wi-Fi discovery remains available when adb is absent.
$adbCommand = Get-Command adb.exe -ErrorAction SilentlyContinue
$adbPath = if ($adbCommand) { $adbCommand.Source } else { $null }
if (-not $adbPath) {
    $editors = Join-Path $env:ProgramFiles 'Unity/Hub/Editor'
    $adbPath = Get-ChildItem -Path "$editors/*/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}
if ($adbPath) {
    try {
        & $adbPath devices 2>$null | ForEach-Object {
            if ($_ -match '^(\S+)\s+device$') {
                & $adbPath -s $Matches[1] reverse tcp:7331 tcp:7331 2>$null | Out-Null
            }
        }
    } catch { }
}
# The helper opens the browser only after the foreground Node host is listening.
$opener = @'
for ($attempt = 0; $attempt -lt 100; $attempt++) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $client.Connect('127.0.0.1', 7331)
        $client.Close()
        Start-Process 'http://localhost:7331/play.html'
        exit
    } catch { $client.Close(); Start-Sleep -Milliseconds 200 }
}
'@
$encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($opener))
Start-Process powershell.exe -WindowStyle Hidden -ArgumentList '-NoProfile', '-EncodedCommand', $encoded
