# PreToolUse hook: deny WebSearch/WebFetch calls whose query/URL touches the task-10 provider
# blocklist (Upvest). All other lookups (ASP.NET, EF, HTTP signatures RFC, Arazzo, ...) pass through.
#
# TASK-10 VARIANT (2026-11-13). A SEPARATE file, selected per arm via webGuardFile, so no earlier
# task's guard changes. Identical for every task-10 arm - tool policy must never differ between
# arms. Upvest knowledge reaches an agent ONLY through its arm's source (docs MCP, Context Plugin,
# Arazzo packages). Keyword filtering is best-effort; the transcript audit is the backstop.
#
# Why each term:
#   upvest            - docs.upvest.co pages, upvestco/http-signature-examples (a working signer),
#                       third-party Upvest profiles, and apimatic/plugin-marketplace branch
#                       rafay/upvest-v4 (the plugin arm's SDK source). The docs MCP arm reaches
#                       docs.upvest.co/mcp-public through MCP, which this guard does not see.
#   uv-apimatic,      - the earlier generated Upvest SDKs (sdks-io/uv-apimatic-dotnet-sdk[-old],
#   up-v-apimatic       NuGet Up-v-ApimaticSDK).
#   uv-poc            - apimatic/uv-poc-v2, the earlier Upvest SDKs with a hand-written signer.
#
# Deliberately NOT blocked: Bash/curl. The build talks to the live sandbox (UPVEST_BASE_URL) by
# design; this guard only closes the WebFetch/WebSearch channel.
# Keep this file ASCII: hooks run under Windows PowerShell 5.1.

$raw = [Console]::In.ReadToEnd()
try { $evt = $raw | ConvertFrom-Json } catch { exit 0 }

if ($evt.tool_name -ne 'WebSearch' -and $evt.tool_name -ne 'WebFetch') { exit 0 }

$parts = @()
if ($evt.tool_input.query)  { $parts += [string]$evt.tool_input.query }
if ($evt.tool_input.url)    { $parts += [string]$evt.tool_input.url }
if ($evt.tool_input.prompt) { $parts += [string]$evt.tool_input.prompt }
$text = $parts -join ' '

$blocklist = '(?i)upvest|uv-apimatic|up-v-apimatic|uv-poc'

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
