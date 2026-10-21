$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$issues = [System.Collections.Generic.List[object]]::new()
function Read-Section([string]$Name, [scriptblock]$Body) {
    try { & $Body } catch { $issues.Add([ordered]@{ area = $Name; message = $_.Exception.Message }) }
}
function Read-Events($Filter, [int]$Maximum) {
    try {
        @(Get-WinEvent -FilterHashtable $Filter -MaxEvents $Maximum -ErrorAction Stop | ForEach-Object {
            [ordered]@{ timeUtc = $_.TimeCreated.ToUniversalTime().ToString('o'); id = $_.Id; provider = $_.ProviderName; message = $_.Message }
        })
    } catch { if ($_.FullyQualifiedErrorId -notlike 'NoMatchingEventsFound*') { throw } }
}
$maximum=[int]$env:WD_MAX
$events=@(Read-Section 'system-events' { Read-Events @{LogName='System';Id=41,6005,6006,6008,1074,1001,12,13,19,20,21;StartTime=(Get-Date).AddDays(-[int]$env:WD_DAYS)} ($maximum+1) })
$rawLimitReached=$events.Count -gt $maximum
$events=@($events | Where-Object {
    ($_.provider -eq 'Microsoft-Windows-Kernel-Power' -and $_.id -eq 41) -or
    ($_.provider -eq 'EventLog' -and $_.id -in 6005,6006,6008) -or
    ($_.provider -eq 'User32' -and $_.id -eq 1074) -or
    ($_.provider -eq 'Microsoft-Windows-WER-SystemErrorReporting' -and $_.id -eq 1001) -or
    ($_.provider -eq 'Microsoft-Windows-Kernel-General' -and $_.id -in 12,13) -or
    ($_.provider -eq 'Microsoft-Windows-WindowsUpdateClient' -and $_.id -in 19,20,21)
})
$limitReached=$rawLimitReached
$events=@($events|Select-Object -First $maximum|Sort-Object timeUtc)
$boot=Read-Section 'last-boot' { (Get-CimInstance Win32_OperatingSystem).LastBootUpTime.ToUniversalTime().ToString('o') }
@{evidence=@{lastBootUtc=$boot;events=$events;limitReached=$limitReached};issues=@($issues.ToArray())}|ConvertTo-Json -Depth 10 -Compress
