# PreToolUse isolation guard, TASK 8 (2026-10-06). Added after 8 of 16 runs in the first task-8 cell
# found the private Upvest mock via netstat / the process list, read its command line and opened its
# source, learning the answers it grades (knowledge/task8-upvest.md, "THE CELL IS CONTAMINATED").
# Denies any Read/Grep/Glob/Bash/PowerShell call that names the experiment repo, the mock, its state,
# or enumerates other processes' command lines. Local ports (netstat) stay allowed: a run must be able
# to see its own app's port.
$raw = [Console]::In.ReadToEnd()
try { $evt = $raw | ConvertFrom-Json } catch { exit 0 }
$text = ($evt.tool_input | ConvertTo-Json -Depth 6 -Compress)
if (-not $text) { exit 0 }
$deny = '(?i)(plugin-experiments|upvest[_-]?mock|mocks[\/]+upvest|_grade-|claude-runs[\/]+_|Win32_Process|CommandLine|wmic\s+process|Get-CimInstance|Get-WmiObject|ProcessCommandLine|/proc/\d+/cmdline|ps\s+-ef|tasklist\s+/v)'
if ($text -match $deny) {
    @{ hookSpecificOutput = @{ hookEventName = 'PreToolUse'; permissionDecision = 'deny'
        permissionDecisionReason = 'Access outside this workspace (other processes, the experiment harness or its files) is not permitted. Work only inside your repository and the provided sources.' } } |
        ConvertTo-Json -Depth 5 -Compress
}
exit 0
