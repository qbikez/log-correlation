<#

.SYNOPSIS
Creates an azure event grid subscription that forwards events to your local dev environment

.DESCRIPTION
Uses ngrok to expose your service to the Internet.
Then creates a subscription in azure (using azure CLI). The subscription name will depend on your Azure login.

Requires `ngrok` and `az` CLI to be installed

.PARAMETER url
Url of local event handler
.PARAMETER resourceGroup
Resource group where Event Grid Domain resides
.PARAMETER domain
Event Grid Domain name
.PARAMETER topic
topic
.PARAMETER eventTypes
optional, specify event types that you want to subscribe to, i.e.:
-eventTypes TransactionCompleted
.PARAMETER filter
optional, advanced event filter 
see also: https://docs.microsoft.com/en-us/azure/event-grid/how-to-filter-events?#azure-cli-2 
i.e.: 
-filter "data.metadataKeys StringContains development"
#>
param(
    $url="http://localhost:5003/shipping/events",
    $resourceGroup,
    $domain,
    $topic,
    [string[]]
    $eventTypes = $null,
    [string]
    $filter = $null,
    [switch][bool] $noTest
)

function find-ngrokTunnel($port) {
    try {
        write-host "looking up ngrok url..."
        Start-Sleep -Seconds 5
        $resp = Invoke-RestMethod http://127.0.0.1:4040/api/tunnels
        $tunnels = $resp.tunnels
    }
    catch {
        write-error "Failed to contact ngrok service. Did it start correctly?"
        return $null
    }
    
    $proto = "https"
    $tunnel = $tunnels | ? { $_.config.addr.Contains("$port") -and $_.proto -eq $proto } | select -first 1
    $url = $tunnel.public_url
    
    return $url
}

pushd $PSScriptRoot
$proc = $null

try {
    if (!$noTest) {
        $testUrl = $url
        write-host "verifying event handler endpoint: $testUrl"
        try {
            $body = '[{
            "id": "2d1781af-3a4c-4d7c-bd0c-e34b19da4e66",
            "topic": "/subscriptions/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
            "subject": "",
            "data": {
              "validationCode": "512d38b6-c7b8-40c8-89fe-f46f9e9622b6",
              "validationUrl": "https://rp-eastus2.eventgrid.azure.net:553/eventsubscriptions/estest/validate?id=512d38b6-c7b8-40c8-89fe-f46f9e9622b6&t=2018-04-26T20:30:54.4538837Z&apiVersion=2018-05-01-preview&token=1A1A1A1A"
            },
            "eventType": "Microsoft.EventGrid.SubscriptionValidationEvent",
            "eventTime": "2018-01-25T22:12:19.4556811Z",
            "metadataVersion": "1",
            "dataVersion": "1"
          }]'
            Invoke-RestMethod -Method "POST" $testUrl -ContentType "application/json" -body $body -Headers @{ "aeg-event-type" = "Notification" } -errorAction Stop
        }
        catch {
            write-error $_
            throw "cannot access service at $testUrl. Make sure you started it."
        }

    }

    $uri = (new-object System.Uri $url)
    $ngrokCmd = "ngrok $($uri.Scheme) -host-header=$($uri.Host) $($uri.Port)"
    
    $proc = get-process ngrok -ErrorAction Ignore
    if ($proc) {
        $tunnelUrl = find-ngrokTunnel    
    }

    if (!$tunnelUrl) {
        write-host "starting ngrok: $ngrokCmd"
        Start-Process -FilePath "$env:comspec" -ArgumentList "/c", $ngrokCmd  -Wait:$false
    
        $tunnelUrl = find-ngrokTunnel

        $proc = get-process ngrok -ErrorAction Ignore
    }

    if (!$tunnelUrl) {
        throw "Failed to determine tunnel URL"
    }

    write-host $tunnelUrl
   

    $azAccount = az account show | Out-String | convertFrom-Json
    $subscription = $azAccount.id

    $egd = "/subscriptions/$subscription/resourceGroups/$resourceGroup/providers/Microsoft.EventGrid/domains/$domain/topics/$topic"

    $user = (az account show) | ConvertFrom-Json | select -ExpandProperty "user"
    $name = $user.name.Replace("@guestline.com", "").Replace(".", "")
    $name += "-local"

    $webhookUrl = $tunnelUrl.TrimEnd("/") + "/" + $uri.PathAndQuery.trimStart("/")
    write-host "setting up EventGrid subscription '$name': $resourceGroup/$domain/$topic => $webhookUrl"
    
    $subscriptions = az eventgrid event-subscription list --source-resource-id $egd
    $subscriptions = $subscriptions | ConvertFrom-Json # -Depth 100

    $existing = $subscriptions | ? { $_.name -eq $name }
    if ($existing) {
        #delete?
    }

    $azArgs = @(
        "eventgrid"
        "event-subscription"
        "create"
        "--name", $name
        "--source-resource-id", $egd
        "--endpoint", $webhookUrl
    )
    if ($eventTypes) {
        $azArgs += @(
            "--included-event-types", "$([string]::Join(" ", $eventTypes))"
        )
    }
    if ($filter) {
        $azArgs += @(
            "--advanced-filter", $filter.split(" ")
        )
    }

    write-verbose "az $([string]::Join(" ", $azArgs))" -Verbose

    $eventSub = az @azArgs | out-string | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create a webhook subscription"
    }

    write-warning "Created event WebHook subscription: $($eventSub.name) => $($eventSub.destination.endpointBaseUrl)"
    
    $portalId = $eventSub.id.Replace("/providers/Microsoft.EventGrid/eventSubscriptions/$($eventSub.name)", "")
    write-host ""
    write-host "https://portal.azure.com/#blade/Microsoft_Azure_EventGrid/DomainTopicOverviewBlade/id/$([System.Uri]::EscapeDataString($portalId))"
    write-host ""

    Write-Warning "Press CTRL+C to quit"
    wait-process ngrok
}
finally {
    popd
    if ($proc) {
        write-host "stopping $proc"
        Stop-Process $proc -Verbose
    }
    else {
        write-host "no proc to stop"
    }
}