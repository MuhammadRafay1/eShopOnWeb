# PreToolUse hook (WebSearch|WebFetch) for the task-10 DOCS arm only (task10-docs, 2026-11-24).
# The arm's knowledge source IS Upvest's public docs website, so unlike hooks/task10-web-guard.ps1
# (used by the other three task-10 arms, unchanged) this guard lets WebFetch read docs.upvest.co
# PAGES. Owner decisions 2026-11-24:
#   - pages only: the OpenAPI download (/_bundle/...), Redocly's page-data/app-data JSON (which
#     carries the API description) and any .json/.yaml file on the docs host are denied;
#   - the docs MCP endpoints (/mcp, /mcp-public) are denied;
#   - every other Upvest-related lookup (web search, other hosts such as upvestco on GitHub, the
#     earlier generated SDKs) is denied exactly as in task10-web-guard.ps1.
# Bash/PowerShell fetches are covered by hooks/task10-docs-isolation-guard.ps1.
# Keep this file ASCII: hooks run under Windows PowerShell 5.1.

$raw = [Console]::In.ReadToEnd()
try { $evt = $raw | ConvertFrom-Json } catch { exit 0 }

if ($evt.tool_name -ne 'WebSearch' -and $evt.tool_name -ne 'WebFetch') { exit 0 }

function Deny([string]$reason) {
    $decision = @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = $reason
        }
    }
    Write-Output ($decision | ConvertTo-Json -Depth 5 -Compress)
    exit 0
}

$blocklist = '(?i)upvest|uv-apimatic|up-v-apimatic|uv-poc'
$docsHost  = '(?i)^https?://docs\.upvest\.co(:\d+)?(/|$)'
$docsDeny  = '(?i)^https?://docs\.upvest\.co(:\d+)?/(mcp|_bundle|page-data|app-data)|\.(json|ya?ml)(\?|#|$)'

if ($evt.tool_name -eq 'WebFetch') {
    $url = [string]$evt.tool_input.url
    if ($url -match $docsHost) {
        if ($url -match $docsDeny) {
            Deny 'Only the documentation pages of docs.upvest.co are available in this workspace: not the OpenAPI description file, page data, or the MCP server.'
        }
        exit 0
    }
    $text = $url + ' ' + [string]$evt.tool_input.prompt
    if ($text -match $blocklist) {
        Deny 'Upvest-related lookups outside docs.upvest.co are not permitted in this workspace. Use the documentation pages of docs.upvest.co.'
    }
    exit 0
}

# WebSearch
if (([string]$evt.tool_input.query) -match $blocklist) {
    Deny 'Upvest-related web searches are not permitted in this workspace. Use the documentation pages of docs.upvest.co.'
}
exit 0
