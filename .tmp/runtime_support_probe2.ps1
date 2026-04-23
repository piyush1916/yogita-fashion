$env:DOTNET_CLI_HOME='e:\yogita fashion\.dotnet_cli_home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:ASPNETCORE_ENVIRONMENT='Development'

function Req($method,$url,$body=$null){
  try {
    if($null -ne $body){
      $json=$body | ConvertTo-Json -Depth 10
      $r=Invoke-WebRequest -Uri $url -Method $method -UseBasicParsing -TimeoutSec 20 -ContentType 'application/json' -Body $json
    } else {
      $r=Invoke-WebRequest -Uri $url -Method $method -UseBasicParsing -TimeoutSec 20
    }
    [PSCustomObject]@{Status=[int]$r.StatusCode;Body=$r.Content}
  }
  catch {
    $status=0; $bodyText=$_.Exception.Message
    if($_.Exception.Response){ try{$status=[int]$_.Exception.Response.StatusCode.value__}catch{}; if($_.ErrorDetails -and $_.ErrorDetails.Message){$bodyText=$_.ErrorDetails.Message} }
    [PSCustomObject]@{Status=$status;Body=$bodyText}
  }
}

$p=Start-Process -FilePath dotnet -ArgumentList 'run --project YogitaFashionAPI\\YogitaFashionAPI.csproj' -WorkingDirectory 'e:\yogita fashion' -PassThru -RedirectStandardOutput 'e:\yogita fashion\logs\qa-backend3.out.log' -RedirectStandardError 'e:\yogita fashion\logs\qa-backend3.err.log'
$probe=$null
for($i=0;$i -lt 60;$i++){
  Start-Sleep -Seconds 2
  $probe=Req 'GET' 'http://127.0.0.1:5037/products'
  if($probe.Status -ne 0){ break }
}
$patch=Req 'PATCH' 'http://127.0.0.1:5037/support/requests/1/status' @{status='Closed'}
$post=Req 'POST' 'http://127.0.0.1:5037/support/requests/1/status' @{status='Closed'}
$get=Req 'GET' 'http://127.0.0.1:5037/support/requests/1/status'
try{ if(-not $p.HasExited){ Stop-Process -Id $p.Id -Force }}catch{}
[PSCustomObject]@{Probe=$probe;Patch=$patch;Post=$post;Get=$get} | ConvertTo-Json -Depth 6
