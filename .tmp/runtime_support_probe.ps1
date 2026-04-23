$env:DOTNET_CLI_HOME='e:\yogita fashion\.dotnet_cli_home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:ASPNETCORE_ENVIRONMENT='Development'

function Req($method,$url){
  try { $r=Invoke-WebRequest -Uri $url -Method $method -UseBasicParsing -TimeoutSec 20; [PSCustomObject]@{Status=[int]$r.StatusCode;Body=$r.Content} }
  catch { 
    $status=0; $body=$_.Exception.Message
    if($_.Exception.Response){ try{$status=[int]$_.Exception.Response.StatusCode.value__}catch{}; if($_.ErrorDetails){$body=$_.ErrorDetails.Message} }
    [PSCustomObject]@{Status=$status;Body=$body}
  }
}

$p=Start-Process -FilePath dotnet -ArgumentList 'run --project YogitaFashionAPI\\YogitaFashionAPI.csproj' -WorkingDirectory 'e:\yogita fashion' -PassThru -RedirectStandardOutput 'e:\yogita fashion\logs\qa-backend2.out.log' -RedirectStandardError 'e:\yogita fashion\logs\qa-backend2.err.log'
Start-Sleep -Seconds 12
$probe=Req 'GET' 'http://127.0.0.1:5037/products'
$s1=Req 'PATCH' 'http://127.0.0.1:5037/support/requests/1/status'
$s2=Req 'POST' 'http://127.0.0.1:5037/support/requests/1/status'
$s3=Req 'GET' 'http://127.0.0.1:5037/support/requests/1/status'
try{ if(-not $p.HasExited){ Stop-Process -Id $p.Id -Force }}catch{}
[PSCustomObject]@{Probe=$probe;Patch=$s1;Post=$s2;Get=$s3} | ConvertTo-Json -Depth 6
