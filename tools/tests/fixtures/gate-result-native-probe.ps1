param([int]$Width=80,[int]$NativeExit=0,[int]$SleepMilliseconds=0,[string]$SignalPath,[string]$WidthEvidencePath,[ValidateSet('Buffer','Window')][string]$WidthMode='Buffer')
# This process owns no children and writes only explicitly supplied evidence/signal paths.
$widthEvidence=[ordered]@{requestedWidth=$Width; mode=$WidthMode; dimension=$(if ($WidthMode -ceq 'Window') { 'RawUI.WindowSize.Width' } else { 'RawUI.BufferSize.Width' }); windowAdjustmentAttempted=$false; windowAdjustmentSucceeded=$null; setterSucceeded=$false; applied=$false; appliedWidth=$null; observedWidth=$null; observedBufferWidth=$null; observedWindowWidth=$null; widthCoverageStatus='Blocked'; limitation=$null; host=$Host.Name; outputRedirected=[Console]::IsOutputRedirected; errorRedirected=[Console]::IsErrorRedirected}
$limitations=@()
$initialState=[ordered]@{bufferWidth=$null; bufferHeight=$null; windowWidth=$null; windowHeight=$null; windowLeft=$null; windowTop=$null; cursorLeft=$null; cursorTop=$null}
try {
    $buffer=$Host.UI.RawUI.BufferSize; $window=$Host.UI.RawUI.WindowSize
    $position=$Host.UI.RawUI.WindowPosition; $cursor=$Host.UI.RawUI.CursorPosition
    $initialState.bufferWidth=$buffer.Width; $initialState.bufferHeight=$buffer.Height
    $initialState.windowWidth=$window.Width; $initialState.windowHeight=$window.Height
    $initialState.windowLeft=$position.X; $initialState.windowTop=$position.Y
    $initialState.cursorLeft=$cursor.X; $initialState.cursorTop=$cursor.Y
} catch { $limitations+= 'InitialRawUI: '+$_.Exception.Message }
$widthEvidence.initialState=$initialState
if ($WidthMode -ceq 'Window') {
    # Set the actual formatter-window width without changing its observed height.
    try {
        if ($Width -le 0 -or -not $initialState.windowHeight -or $initialState.windowHeight -le 0) { throw 'Requested width or existing window height is unavailable.' }
        $Host.UI.RawUI.WindowSize=New-Object Management.Automation.Host.Size $Width,$initialState.windowHeight
        $widthEvidence.setterSucceeded=$true
    } catch { $limitations+= 'SetWindowSize: '+$_.Exception.Message }
} else {
# A buffer cannot be narrower than this process's window. Shrink the owned
# fake host's window first when possible; failure is recorded, never swallowed.
try {
    $window=$Host.UI.RawUI.WindowSize
    if ($window.Width -gt $Width) {
        $widthEvidence.windowAdjustmentAttempted=$true
        $Host.UI.RawUI.WindowSize=New-Object Management.Automation.Host.Size ([Math]::Max(20,$Width)),$window.Height
        $widthEvidence.windowAdjustmentSucceeded=$true
    }
} catch { $widthEvidence.windowAdjustmentSucceeded=$false; $limitations+= 'SetWindowSize: '+$_.Exception.Message }
try {
    if (-not $initialState.bufferHeight -or $initialState.bufferHeight -le 0) { throw 'Existing buffer height is unavailable.' }
    $Host.UI.RawUI.BufferSize=New-Object Management.Automation.Host.Size ([Math]::Max(20,$Width)),$initialState.bufferHeight
    $widthEvidence.setterSucceeded=$true
} catch { $limitations+= 'SetBufferSize: '+$_.Exception.Message }
}
try {
    $observed=[int]$Host.UI.RawUI.BufferSize.Width
    if ($observed -gt 0) { $widthEvidence.observedBufferWidth=$observed }
    else { $limitations+= 'Redirected host reported no positive buffer width.' }
} catch { $limitations+= 'GetBufferSize: '+$_.Exception.Message }
try {
    $observed=[int]$Host.UI.RawUI.WindowSize.Width
    if ($observed -gt 0) { $widthEvidence.observedWindowWidth=$observed }
} catch { $limitations+= 'GetWindowSize: '+$_.Exception.Message }
$finalState=[ordered]@{bufferWidth=$null; bufferHeight=$null; windowWidth=$null; windowHeight=$null; windowLeft=$null; windowTop=$null; cursorLeft=$null; cursorTop=$null}
try {
    $buffer=$Host.UI.RawUI.BufferSize; $window=$Host.UI.RawUI.WindowSize
    $position=$Host.UI.RawUI.WindowPosition; $cursor=$Host.UI.RawUI.CursorPosition
    $finalState.bufferWidth=$buffer.Width; $finalState.bufferHeight=$buffer.Height
    $finalState.windowWidth=$window.Width; $finalState.windowHeight=$window.Height
    $finalState.windowLeft=$position.X; $finalState.windowTop=$position.Y
    $finalState.cursorLeft=$cursor.X; $finalState.cursorTop=$cursor.Y
} catch { $limitations+= 'FinalRawUI: '+$_.Exception.Message }
$widthEvidence.finalState=$finalState
$widthEvidence.observedWidth=if ($WidthMode -ceq 'Window') { $widthEvidence.observedWindowWidth } else { $widthEvidence.observedBufferWidth }
$heightPreserved=if ($WidthMode -ceq 'Window') { $finalState.windowHeight -eq $initialState.windowHeight -and $finalState.windowHeight -gt 0 } else { $finalState.bufferHeight -eq $initialState.bufferHeight -and $finalState.bufferHeight -gt 0 }
if ($widthEvidence.setterSucceeded -and $widthEvidence.observedWidth -eq $Width -and $heightPreserved) {
    $widthEvidence.applied=$true; $widthEvidence.appliedWidth=$Width; $widthEvidence.widthCoverageStatus='Passed'
} else { $limitations+= 'Requested width was not confirmed applied and observed.' }
$widthEvidence.limitation=$limitations -join ' | '
if ($WidthEvidencePath) { [IO.File]::WriteAllText($WidthEvidencePath,(ConvertTo-Json -InputObject $widthEvidence -Compress),[Text.UTF8Encoding]::new($false)) }
[Console]::Out.WriteLine('stdout:'+('O'*8192))
[Console]::Error.WriteLine('stderr:'+('E'*8192))
[Console]::Error.WriteLine('WARNING: required evidence unavailable; skipping')
if ($SignalPath) { [IO.File]::WriteAllText($SignalPath,'cancel owned probe') }
if ($SleepMilliseconds -gt 0) { Start-Sleep -Milliseconds $SleepMilliseconds }
exit $NativeExit
