# PreToolUse hook: during the PLAN phase of a two-phase run, deny every file-writing tool call
# whose target is not the plan file. Outside the plan phase it does nothing.
#
# TWO-PHASE RUNS (2026-09-29). A two-phase arm (arm config `twoPhase` block) runs the task twice
# in one workspace: first a plan session that may write only PLAN.md, then a build session on a
# different model that implements it. This hook is layer one of keeping the plan session
# read-only. It covers the file tools only. Layer two is the git-status check run-phase.ps1 does
# after the plan session exits, which also catches writes made through Bash/PowerShell - a hook
# cannot tell every shell write apart from a read, so that check is the one that decides.
#
# ACTIVE ONLY WHILE THE MARKER EXISTS. run-phase.ps1 -Phase plan writes
# <workspace>\.plan-phase-active before the agent starts and removes it afterwards, so the same
# registered hook is inert during the build phase. The marker is outside the repo, so it is never
# committed and the agent never sees it in the tree. It holds the absolute path of the one file
# the plan session may write.
#
# Wired for two-phase arms only. Every single-phase arm's settings.json is unchanged.

$raw = [Console]::In.ReadToEnd()
try { $evt = $raw | ConvertFrom-Json } catch { exit 0 }

$writeTools = @('Write', 'Edit', 'MultiEdit', 'NotebookEdit')
if ($writeTools -notcontains [string]$evt.tool_name) { exit 0 }

# The hook lives at <workspace>\repo\.claude\hooks\; the marker sits at <workspace>\.
$workspace = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$marker = Join-Path $workspace '.plan-phase-active'
if (-not (Test-Path -LiteralPath $marker)) { exit 0 }

$allowed = ([System.IO.File]::ReadAllText($marker)).Trim()
$target = [string]$evt.tool_input.file_path
if (-not $target) { $target = [string]$evt.tool_input.notebook_path }

$norm = {
    param([string]$p)
    if (-not $p) { return '' }
    try { return [System.IO.Path]::GetFullPath($p).TrimEnd('\', '/').ToLowerInvariant() } catch { return $p.ToLowerInvariant() }
}
if ($target -and $allowed -and ((& $norm $target) -eq (& $norm $allowed))) { exit 0 }

$decision = @{
    hookSpecificOutput = @{
        hookEventName            = 'PreToolUse'
        permissionDecision       = 'deny'
        permissionDecisionReason = 'This session is the planning phase. The only file it may write is PLAN.md at the repository root.'
    }
}
Write-Output ($decision | ConvertTo-Json -Depth 5 -Compress)
exit 0
