param([Parameter(Mandatory = $true)][string]$LockPath)
try {
    $probeStream = [System.IO.File]::Open($LockPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    $probeStream.Dispose()
    exit 0
} catch [System.IO.IOException] {
    exit 23
}
