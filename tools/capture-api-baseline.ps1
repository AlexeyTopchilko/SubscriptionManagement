#Requires -Version 7
<#
.SYNOPSIS
    Captures a diffable snapshot of every API response shape and status code.

.DESCRIPTION
    This repository has no test project, so "the API still behaves the same" has
    nothing to be checked against. This script produces that reference: it calls
    every endpoint, including negative paths and the full subscription status
    state machine, and writes the results to a markdown file.

    It is NOT a test suite. It asserts nothing and never fails a build. It is a
    snapshot you diff by hand.

    Volatile values are normalised to placeholders, so two runs against an
    unchanged API produce byte-identical output. What survives normalisation is
    exactly what matters: the key set, the value types, the camelCase naming,
    enums serialised as strings, null-versus-absent, and the status codes.

.PARAMETER BaseUrl
    Root of the running API. Default http://localhost:5000

.PARAMETER OutFile
    Where to write the snapshot. Default docs/api-baseline.md

.EXAMPLE
    # Regenerate the committed baseline
    dotnet run --project backend/src/SubscriptionManager.Api    # in another shell
    ./tools/capture-api-baseline.ps1

.EXAMPLE
    # Check for regressions after a change
    ./tools/capture-api-baseline.ps1 -OutFile after.md
    git diff --no-index docs/api-baseline.md after.md

.NOTES
    IMPORTANT: start the API fresh before running. Storage is in-memory, so a
    previous run's mutations linger in the process and shift the seed data this
    script reads first.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$OutFile = 'docs/api-baseline.md'
)

$ErrorActionPreference = 'Stop'

# Fixed dates in request bodies. Literals, never Get-Date — a moving value would
# make the output differ between runs for no real reason.
$FutureStart = '2027-01-01T00:00:00Z'
$FutureEnd   = '2027-12-31T00:00:00Z'
$UnknownGuid = '00000000-0000-0000-0000-000000000001'

$script:Cases = [System.Collections.Generic.List[object]]::new()

function ConvertTo-StableJson {
    <#
        PowerShell hashtable literals are unordered, so ConvertTo-Json emits their
        keys in whatever order the hashtable happens to enumerate — which differs
        between runs and makes the diff red for no reason. Sorting the keys fixes
        it in one place instead of at every call site. Request-body key order is
        our own input, not part of the API contract, so sorting loses nothing.
    #>
    param($Obj, [int]$Depth = 10, [switch]$Compress)

    if ($Obj -is [System.Collections.IDictionary]) {
        $ordered = [ordered]@{}
        foreach ($key in ($Obj.Keys | Sort-Object)) { $ordered[$key] = $Obj[$key] }
        $Obj = $ordered
    }

    if ($Compress) { $Obj | ConvertTo-Json -Depth $Depth -Compress }
    else           { $Obj | ConvertTo-Json -Depth $Depth }
}

function Normalize-Payload {
    <#
        Replaces values that legitimately change between runs. Everything else is
        left exactly as the API produced it — including number formatting, which
        is one of the things most likely to shift under a storage change
        (decimal 4900 surfacing as 4900.0, for instance).
    #>
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) { return '' }

    $guid = '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}'
    $iso  = '\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?'

    $normalized = $Text -replace $guid, '<guid>'
    $normalized = $normalized -replace $iso, '<timestamp>'

    # ASP.NET stamps a fresh traceId into every ProblemDetails body. It is a
    # correlation id, not part of the contract, and left alone it makes the diff
    # red on every single run.
    $normalized = $normalized -replace '("traceId"\s*:\s*")[^"]*(")', '${1}<traceId>${2}'

    # Pretty-print so the diff is line-oriented rather than one giant line.
    try {
        $normalized | ConvertFrom-Json -Depth 20 | ConvertTo-Json -Depth 20
    }
    catch {
        # Not JSON (empty 204 body, plain-text error). Record it verbatim.
        $normalized
    }
}

function Invoke-Case {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Path,
        $Body
    )

    $params = @{
        Uri                = "$BaseUrl$Path"
        Method             = $Method
        SkipHttpErrorCheck = $true   # we want 4xx recorded, not thrown
    }
    if ($null -ne $Body) {
        $params.Body        = ConvertTo-StableJson $Body -Compress
        $params.ContentType = 'application/json'
    }

    $response = Invoke-WebRequest @params

    $script:Cases.Add([pscustomobject]@{
        Name       = $Name
        Method     = $Method
        Path       = $Path
        Request    = if ($null -ne $Body) { ConvertTo-StableJson $Body } else { $null }
        StatusCode = [int]$response.StatusCode
        Payload    = Normalize-Payload ([string]$response.Content)
    })

    # Returned raw so the caller can chain ids; the recorded copy is normalised.
    if ([string]::IsNullOrWhiteSpace($response.Content)) { return $null }
    try { $response.Content | ConvertFrom-Json -Depth 20 } catch { $null }
}

Write-Host "Capturing baseline from $BaseUrl" -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# 1. Reads against untouched seed data.
#    These run FIRST and deliberately before any mutation: the store is
#    in-memory, so creating anything would change what the collection reads
#    return and break idempotence for reasons unrelated to the API.
# ---------------------------------------------------------------------------

$customers = Invoke-Case 'List all customers' 'GET' '/api/customers'

if (-not $customers -or $customers.Count -lt 1) {
    throw "No seed customers returned. Is the API running at $BaseUrl?"
}

$seedCustomer = $customers[0]
$seedSub      = $seedCustomer.subscriptions[0]

Invoke-Case 'Get customer by id'        'GET' "/api/customers/$($seedCustomer.id)"                             | Out-Null
Invoke-Case 'Get customer, unknown id'  'GET' "/api/customers/$UnknownGuid"                                    | Out-Null
Invoke-Case 'List subscriptions'        'GET' "/api/customers/$($seedCustomer.id)/subscriptions"               | Out-Null
Invoke-Case 'Get subscription by id'    'GET' "/api/customers/$($seedCustomer.id)/subscriptions/$($seedSub.id)" | Out-Null
Invoke-Case 'Get subscription, unknown' 'GET' "/api/customers/$($seedCustomer.id)/subscriptions/$UnknownGuid"   | Out-Null

# ---------------------------------------------------------------------------
# 2. Rejected writes. Captured before the happy path so a validation change
#    cannot be masked by a successful create earlier in the run.
# ---------------------------------------------------------------------------

Invoke-Case 'Create customer, blank name' 'POST' '/api/customers' @{
    name = ''
    email = 'blank-name@test.dev'
} | Out-Null

Invoke-Case 'Create customer, blank email' 'POST' '/api/customers' @{
    name = 'No Email'
    email = ''
} | Out-Null

# Duplicate email is a 409, not a 400. Worth pinning: a unique index in a
# relational store could plausibly turn this into a 500 instead.
Invoke-Case 'Create customer, duplicate email' 'POST' '/api/customers' @{
    name = 'Duplicate'
    email = $seedCustomer.email
} | Out-Null

# ---------------------------------------------------------------------------
# 3. Mutations, all on a throwaway customer created here and deleted at the end.
# ---------------------------------------------------------------------------

$created = Invoke-Case 'Create customer' 'POST' '/api/customers' @{
    name    = 'Baseline Fixture'
    email   = 'baseline@fixture.test'
    company = 'Fixture Co'
    phone   = '+1 (555) 000-0000'
}
$cid = $created.id

Invoke-Case 'Update customer' 'PUT' "/api/customers/$cid" @{
    name    = 'Baseline Fixture Renamed'
    email   = 'baseline@fixture.test'
    company = $null
    phone   = $null
} | Out-Null

Invoke-Case 'Update customer, unknown id' 'PUT' "/api/customers/$UnknownGuid" @{
    name  = 'Nobody'
    email = 'nobody@fixture.test'
} | Out-Null

# StartDate in the future, so the service must derive status = Future.
# BillingCycle deliberately upper-case to pin the lower-casing behaviour.
$sub = Invoke-Case 'Create subscription (future start, upper-case cycle)' 'POST' "/api/customers/$cid/subscriptions" @{
    plan         = 'Pro'
    price        = 4900
    billingCycle = 'MONTHLY'
    startDate    = $FutureStart
    endDate      = $FutureEnd
    notes        = 'baseline fixture'
}
$sid = $sub.id

Invoke-Case 'Create subscription, bad billing cycle' 'POST' "/api/customers/$cid/subscriptions" @{
    plan = 'Pro'; price = 4900; billingCycle = 'weekly'; startDate = $FutureStart
} | Out-Null

Invoke-Case 'Create subscription, negative price' 'POST' "/api/customers/$cid/subscriptions" @{
    plan = 'Pro'; price = -1; billingCycle = 'monthly'; startDate = $FutureStart
} | Out-Null

Invoke-Case 'Create subscription, end before start' 'POST' "/api/customers/$cid/subscriptions" @{
    plan = 'Pro'; price = 4900; billingCycle = 'monthly'
    startDate = $FutureEnd; endDate = $FutureStart
} | Out-Null

Invoke-Case 'Create subscription, blank plan' 'POST' "/api/customers/$cid/subscriptions" @{
    plan = ''; price = 4900; billingCycle = 'monthly'; startDate = $FutureStart
} | Out-Null

# PUT deliberately carries no Status field: the full-update endpoint must not
# touch status. Constitution Principle IV depends on that staying true.
Invoke-Case 'Update subscription (must not change status)' 'PUT' "/api/customers/$cid/subscriptions/$sid" @{
    plan         = 'Enterprise'
    price        = 19900
    billingCycle = 'annual'
    startDate    = $FutureStart
    endDate      = $null
    notes        = $null
} | Out-Null

# ---------------------------------------------------------------------------
# 4. The status state machine, in one unbroken chain.
#    The last case is the one the constitution documents and CLAUDE.md does not:
#    a same-status PATCH is a permitted no-op even from terminal Cancelled.
# ---------------------------------------------------------------------------

$statusPath = "/api/customers/$cid/subscriptions/$sid/status"

Invoke-Case 'Status Future -> Active'                          'PATCH' $statusPath @{ status = 'Active' }    | Out-Null
Invoke-Case 'Status Active -> Paused'                          'PATCH' $statusPath @{ status = 'Paused' }    | Out-Null
Invoke-Case 'Status Paused -> Active'                          'PATCH' $statusPath @{ status = 'Active' }    | Out-Null
Invoke-Case 'Status Active -> Future (backwards, must fail)'   'PATCH' $statusPath @{ status = 'Future' }    | Out-Null
Invoke-Case 'Status Active -> Cancelled'                       'PATCH' $statusPath @{ status = 'Cancelled' } | Out-Null
Invoke-Case 'Status Cancelled -> Active (terminal, must fail)' 'PATCH' $statusPath @{ status = 'Active' }    | Out-Null
Invoke-Case 'Status Cancelled -> Cancelled (no-op, must pass)' 'PATCH' $statusPath @{ status = 'Cancelled' } | Out-Null

# ---------------------------------------------------------------------------
# 5. Deletes. These also clean up the fixture, so a rerun starts from the same
#    seed state as this one did.
# ---------------------------------------------------------------------------

Invoke-Case 'Delete subscription'          'DELETE' "/api/customers/$cid/subscriptions/$sid"         | Out-Null
Invoke-Case 'Delete subscription, unknown' 'DELETE' "/api/customers/$cid/subscriptions/$UnknownGuid" | Out-Null
Invoke-Case 'Delete customer'              'DELETE' "/api/customers/$cid"                            | Out-Null
Invoke-Case 'Delete customer, unknown id'  'DELETE' "/api/customers/$UnknownGuid"                    | Out-Null

# ---------------------------------------------------------------------------
# 6. Write the snapshot.
# ---------------------------------------------------------------------------

$guidPattern = '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}'

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# API baseline')
$lines.Add('')
$lines.Add('Generated by `tools/capture-api-baseline.ps1`. **Do not edit by hand.**')
$lines.Add('')
$lines.Add('A reference snapshot of every endpoint''s status code and response shape, for')
$lines.Add('diffing after a change that could alter the wire contract. This is not a test')
$lines.Add('suite: nothing here asserts, and nothing fails a build.')
$lines.Add('')
$lines.Add('GUIDs render as `<guid>`, timestamps as `<timestamp>` and ASP.NET correlation ids')
$lines.Add('as `<traceId>` — all three change every run and would otherwise make the diff')
$lines.Add('permanently red. Everything else is verbatim, including number formatting, which')
$lines.Add('is among the likeliest things to shift when storage changes.')
$lines.Add('')
$lines.Add('To regenerate: start the API fresh, then run the script. Starting fresh matters —')
$lines.Add('storage is in-memory, so leftover mutations shift the seed data.')
$lines.Add('')
$lines.Add("Cases captured: $($script:Cases.Count)")
$lines.Add('')
$lines.Add('| # | Case | Method | Status |')
$lines.Add('|---|---|---|---|')
$i = 0
foreach ($c in $script:Cases) {
    $i++
    $lines.Add("| $i | $($c.Name) | ``$($c.Method)`` | **$($c.StatusCode)** |")
}
$lines.Add('')
$lines.Add('---')
$lines.Add('')

$i = 0
foreach ($c in $script:Cases) {
    $i++
    $lines.Add("## $i. $($c.Name)")
    $lines.Add('')
    $lines.Add("``$($c.Method) $($c.Path -replace $guidPattern, '<guid>')``")
    $lines.Add('')
    $lines.Add("Status: **$($c.StatusCode)**")
    $lines.Add('')
    if ($c.Request) {
        $lines.Add('Request:')
        $lines.Add('')
        $lines.Add('```json')
        $lines.Add(($c.Request -replace $guidPattern, '<guid>').Trim())
        $lines.Add('```')
        $lines.Add('')
    }
    $lines.Add('Response:')
    $lines.Add('')
    $lines.Add('```json')
    $lines.Add($(if ([string]::IsNullOrWhiteSpace($c.Payload)) { '(empty body)' } else { $c.Payload.Trim() }))
    $lines.Add('```')
    $lines.Add('')
}

$dir = Split-Path -Parent $OutFile
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }

# Explicit LF and no BOM so the file diffs identically regardless of platform.
$content = ($lines -join "`n") + "`n"
[System.IO.File]::WriteAllText(
    [System.IO.Path]::GetFullPath($OutFile),
    $content,
    (New-Object System.Text.UTF8Encoding($false))
)

Write-Host "Wrote $($script:Cases.Count) cases to $OutFile" -ForegroundColor Green
