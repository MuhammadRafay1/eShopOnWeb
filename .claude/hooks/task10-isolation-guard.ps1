# PreToolUse hook: deny file and shell tools that reach folders outside this workspace that hold
# material for this task. Same file for every task-10 arm - tool policy must never differ between arms.
#
# TASK-10 VARIANT (2026-11-13). Wired by new-run.ps1 from the arm's isolationGuardFile.
# Keep the comments neutral: this file is copied into the run's workspace.
# Keep this file ASCII: hooks run under Windows PowerShell 5.1.

$raw = [Console]::In.ReadToEnd()
try { $evt = $raw | ConvertFrom-Json } catch { exit 0 }

$tool = [string]$evt.tool_name
if ($tool -notin @('Bash', 'PowerShell', 'Read', 'Grep', 'Glob', 'Edit', 'Write', 'NotebookEdit')) { exit 0 }

$parts = @()
foreach ($name in @('command', 'file_path', 'path', 'pattern', 'notebook_path', 'glob')) {
    if ($evt.tool_input.PSObject.Properties[$name] -and $evt.tool_input.$name) { $parts += [string]$evt.tool_input.$name }
}
$text = $parts -join ' '

# Any path rooted at <drive>:\poc or /<drive>/poc, in any slash style, except the one folder the
# credentials point into; and the experiment repository by name.
$pocRoot = '(?i)(?:\b[a-z]:|(?<![\w.~-])/[a-z])[\\/]+poc(?![\\/]+upvest-run-secrets(?:[\\/"''\s]|$))(?=[\\/"''\s*]|$)'
$expRepo = '(?i)plugin-experiments'
# No stepping out of the allowed folder with '..'.
$escape  = '(?i)upvest-run-secrets[\\/]+\.\.'
$blocked = "$pocRoot|$expRepo|$escape"

if ($text -match $blocked) {
    $decision = @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = 'That location is outside this workspace and is not available to this task. Work inside the workspace and the sources it provides.'
        }
    }
    Write-Output ($decision | ConvertTo-Json -Depth 5 -Compress)
}
exit 0
