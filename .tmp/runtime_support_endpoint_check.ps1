$env:ASPNETCORE_ENVIRONMENT='Development'
$p=Start-Process -FilePath dotnet -ArgumentList 'bin\\Debug\\net10.0\\YogitaFashionAPI.dll' -WorkingDirectory 'e:\yogita fashion\\YogitaFashionAPI' -PassThru -RedirectStandardOutput 'e:\yogita fashion\\logs\\qa-backend5.out.log' -RedirectStandardError 'e:\yogita fashion\\logs\\qa-backend5.err.log'
Start-Sleep -Seconds 3
function Req($method,$url){
  try{$r=Invoke-WebRequest -Uri $url -Method $method -UseBasicParsing -TimeoutSec 15; [PSCustomObject]@{Status=[int]$r.StatusCode;Body=$r.Content}}
  catch{$status=0;$body=$_.Exception.Message;if($_.Exception.Response){try{$status=[int]$_.Exception.Response.StatusCode.value__}catch{}; if($_.ErrorDetails -and $_.ErrorDetails.Message){$body=$_.ErrorDetails.Message}}; [PSCustomObject]@{Status=$status;Body=$body}}
}
$r1=Req 'PATCH' 'http://127.0.0.1:5037/support/requests/1/status'
$r2=Req 'GET' 'http://127.0.0.1:5037/support/requests/1/status'
$r3=Req 'POST' 'http://127.0.0.1:5037/support/requests/1/status'
try{if(-not $p.HasExited){Stop-Process -Id $p.Id -Force}}catch{}
[PSCustomObject]@{Patch=$r1;Get=$r2;Post=$r3} | ConvertTo-Json -Depth 5
