$env:ASPNETCORE_ENVIRONMENT='Development'
$p=Start-Process -FilePath dotnet -ArgumentList 'bin\\Debug\\net10.0\\YogitaFashionAPI.dll' -WorkingDirectory 'e:\yogita fashion\\YogitaFashionAPI' -PassThru -RedirectStandardOutput 'e:\yogita fashion\\logs\\qa-backend4.out.log' -RedirectStandardError 'e:\yogita fashion\\logs\\qa-backend4.err.log'
for($i=0;$i -lt 20;$i++){
  Start-Sleep -Seconds 2
  try{$r=Invoke-WebRequest -Uri 'http://127.0.0.1:5037/products' -Method GET -UseBasicParsing -TimeoutSec 10; $status=[int]$r.StatusCode}catch{ if($_.Exception.Response){try{$status=[int]$_.Exception.Response.StatusCode.value__}catch{$status=0}} else {$status=0} }
  if($status -ne 0){ break }
}
$out=(Get-Content 'e:\yogita fashion\\logs\\qa-backend4.out.log' -Tail 40) -join "`n"
$err=(Get-Content 'e:\yogita fashion\\logs\\qa-backend4.err.log' -Tail 40) -join "`n"
try{if(-not $p.HasExited){Stop-Process -Id $p.Id -Force}}catch{}
[PSCustomObject]@{Status=$status;Out=$out;Err=$err} | ConvertTo-Json -Depth 4
