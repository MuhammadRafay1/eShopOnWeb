# PreToolUse hook (WebSearch/WebFetch). TASK-8 VARIANT (2026-10-05). Owner decision: web is open
# for every arm, but Upvest itself is blocked for every arm, so Upvest knowledge reaches an agent
# ONLY through its arm's source (docs MCP, Context Plugin, Arazzo packages).
# 'upvest'                     docs.upvest.co, api/sandbox.upvest.co, and any search naming it.
# 'uv-apimatic|up-v-apimatic'  the plugin's generated SDK (github sdks-io/uv-apimatic-dotnet-sdk,
#                              NuGet Up-v-ApimaticSDK): without this an MCP arm could read the
#                              plugin arm's SDK off the web. The plugin arm clones it with git,
#                              which this hook does not see, exactly as its skills instruct.
# NOT blocked: the mock (localhost, via Bash) and generic topics (HTTP signatures RFC, Arazzo).
$raw = [Console]::In.ReadToEnd()
try { $evt = $raw | ConvertFrom-Json } catch { exit 0 }

if ($evt.tool_name -ne 'WebSearch' -and $evt.tool_name -ne 'WebFetch') { exit 0 }

$parts = @()
if ($evt.tool_input.query)  { $parts += [string]$evt.tool_input.query }
if ($evt.tool_input.url)    { $parts += [string]$evt.tool_input.url }
if ($evt.tool_input.prompt) { $parts += [string]$evt.tool_input.prompt }
$text = $parts -join ' '

$blocklist = 'upvest|uv-apimatic|up-v-apimatic'

if ($text -match $blocklist) {
    $decision = @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = 'Upvest-related web lookups are not permitted in this workspace. Use only the knowledge sources provided inside the workspace.'
        }
    }
    Write-Output ($decision | ConvertTo-Json -Depth 5 -Compress)
}
exit 0
