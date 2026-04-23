$ErrorActionPreference = 'Continue'

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Url,
        [object]$Body = $null,
        [string]$Token = ''
    )

    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers['Authorization'] = "Bearer $Token"
    }

    $status = 0
    $respBody = ''

    try {
        if ($null -ne $Body) {
            $json = $Body | ConvertTo-Json -Depth 20
            $resp = Invoke-WebRequest -Uri $Url -Method $Method -Headers $headers -ContentType 'application/json' -Body $json -UseBasicParsing -TimeoutSec 30
        }
        else {
            $resp = Invoke-WebRequest -Uri $Url -Method $Method -Headers $headers -UseBasicParsing -TimeoutSec 30
        }

        $status = [int]$resp.StatusCode
        $respBody = [string]$resp.Content
    }
    catch {
        $ex = $_.Exception
        if ($ex.Response) {
            try { $status = [int]$ex.Response.StatusCode.value__ } catch { try { $status = [int]$ex.Response.StatusCode } catch { $status = 0 } }
            if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
                $respBody = [string]$_.ErrorDetails.Message
            }
            else {
                try {
                    $stream = $ex.Response.GetResponseStream()
                    if ($stream) {
                        $reader = New-Object System.IO.StreamReader($stream)
                        $respBody = [string]$reader.ReadToEnd()
                        $reader.Close()
                    }
                    else {
                        $respBody = [string]$ex.Message
                    }
                }
                catch {
                    $respBody = [string]$ex.Message
                }
            }
        }
        else {
            $status = 0
            $respBody = [string]$ex.Message
        }
    }

    if ($null -eq $respBody) { $respBody = '' }
    if ($respBody.Length -gt 1200) { $respBody = $respBody.Substring(0, 1200) }

    return [PSCustomObject]@{
        Status = $status
        Body = $respBody
    }
}

$tests = New-Object System.Collections.Generic.List[object]
function Add-Test {
    param(
        [string]$Flow,
        [string]$Step,
        [object]$Res,
        [string]$Notes = ''
    )

    $tests.Add([PSCustomObject]@{
        Flow = $Flow
        Step = $Step
        Status = $Res.Status
        Body = $Res.Body
        Notes = $Notes
    }) | Out-Null
}

$backendProc = $null
$storeProc = $null
$adminProc = $null

try {
    $root = 'e:\yogita fashion'
    $base = 'http://127.0.0.1:5037'

    $env:DOTNET_CLI_HOME = 'e:\yogita fashion\.dotnet_cli_home'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'

    $backendOut = "$root\\logs\\qa-backend.out.log"
    $backendErr = "$root\\logs\\qa-backend.err.log"
    if (Test-Path $backendOut) { Remove-Item $backendOut -Force }
    if (Test-Path $backendErr) { Remove-Item $backendErr -Force }

    $backendProc = Start-Process -FilePath dotnet -ArgumentList 'run --project YogitaFashionAPI\\YogitaFashionAPI.csproj' -WorkingDirectory $root -PassThru -RedirectStandardOutput $backendOut -RedirectStandardError $backendErr

    $apiReady = $false
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Seconds 2
        $probe = Invoke-Api -Method 'GET' -Url "$base/products"
        if ($probe.Status -ne 0) {
            $apiReady = $true
            Add-Test -Flow 'Bootstrap' -Step 'Backend readiness probe /products' -Res $probe
            break
        }
    }

    if (-not $apiReady) {
        $outTail = ''
        $errTail = ''
        if (Test-Path $backendOut) { $outTail = (Get-Content $backendOut -Tail 60) -join "`n" }
        if (Test-Path $backendErr) { $errTail = (Get-Content $backendErr -Tail 60) -join "`n" }
        Add-Test -Flow 'Bootstrap' -Step 'Backend failed to become reachable' -Res ([PSCustomObject]@{Status=0;Body=($outTail + "`n" + $errTail)})
    }

    $email = "qauser_$(Get-Random)@example.com"
    $password = 'Pass@1234'
    $token = ''
    $userId = 0

    # 1. Authentication
    $r = Invoke-Api -Method 'POST' -Url "$base/api/Auth/register" -Body @{name='QA User'; email=$email; password=$password; phone='9999999999'; city='Pune'}
    Add-Test -Flow 'Authentication' -Step 'User signup' -Res $r

    $r = Invoke-Api -Method 'POST' -Url "$base/api/Auth/register" -Body @{name='QA User'; email=$email; password=$password; phone='9999999999'; city='Pune'}
    Add-Test -Flow 'Authentication' -Step 'Duplicate signup' -Res $r

    $loginRes = Invoke-Api -Method 'POST' -Url "$base/api/Auth/login" -Body @{email=$email; password=$password}
    Add-Test -Flow 'Authentication' -Step 'Login with correct credentials' -Res $loginRes

    if ($loginRes.Status -eq 200) {
        try {
            $loginObj = $loginRes.Body | ConvertFrom-Json
            $token = [string]$loginObj.token
            $userId = [int]$loginObj.user.id
        }
        catch {}
    }

    $r = Invoke-Api -Method 'POST' -Url "$base/api/Auth/login" -Body @{email=$email; password='Wrong@123'}
    Add-Test -Flow 'Authentication' -Step 'Login with wrong credentials' -Res $r

    $r = Invoke-Api -Method 'GET' -Url "$base/api/Auth/me"
    Add-Test -Flow 'Authentication' -Step 'Access protected route without login' -Res $r

    if (-not [string]::IsNullOrWhiteSpace($token)) {
        $r = Invoke-Api -Method 'GET' -Url "$base/api/Auth/me" -Token $token
        Add-Test -Flow 'Authentication' -Step 'Access protected route with token' -Res $r

        $r = Invoke-Api -Method 'PUT' -Url "$base/api/Auth/profile/$userId" -Token $token -Body @{name='QA User Updated'; email=$email; phone='8888888888'; city='Mumbai'; password='123'}
        Add-Test -Flow 'Authentication' -Step 'Password update with invalid short password' -Res $r

        $r = Invoke-Api -Method 'PUT' -Url "$base/api/Auth/profile/$userId" -Token $token -Body @{name='QA User Updated'; email=$email; phone='8888888888'; city='Mumbai'; password='NewPass@123'}
        Add-Test -Flow 'Authentication' -Step 'Profile+password update valid' -Res $r

        $r = Invoke-Api -Method 'GET' -Url "$base/api/Auth/me" -Token $token
        Add-Test -Flow 'Authentication' -Step 'Profile persistence after update' -Res $r
    }

    $r = Invoke-Api -Method 'GET' -Url "$base/api/Auth/me"
    Add-Test -Flow 'Authentication' -Step 'Protected route after logout/no token' -Res $r

    # 2. Store Product Flow
    $r = Invoke-Api -Method 'GET' -Url "$base/products"
    Add-Test -Flow 'Store Product' -Step 'Product listing load' -Res $r

    $r = Invoke-Api -Method 'GET' -Url "$base/products/1"
    Add-Test -Flow 'Store Product' -Step 'Product details load' -Res $r

    $r = Invoke-Api -Method 'POST' -Url "$base/products/1/stock-alerts" -Body @{email=$email; whatsAppNumber='9999999999'}
    Add-Test -Flow 'Store Product' -Step 'Stock alert subscription' -Res $r

    # 3. Cart Flow (API-side constraints via order creation)
    $r = Invoke-Api -Method 'POST' -Url "$base/orders" -Body @{name='QA User'; phone='9999999999'; email=$email; address='Street 1'; city='Pune'; pincode='411001'; payment='COD'; items=@()}
    Add-Test -Flow 'Cart' -Step 'Checkout with empty cart/items' -Res $r

    # 4. Coupon + Checkout
    $r = Invoke-Api -Method 'POST' -Url "$base/coupons/validate" -Body @{code='SAVE10'; subtotal=1000; userId=$userId}
    Add-Test -Flow 'Coupon+Checkout' -Step 'Apply valid coupon' -Res $r

    $r = Invoke-Api -Method 'POST' -Url "$base/coupons/validate" -Body @{code='INVALID999'; subtotal=1000; userId=$userId}
    Add-Test -Flow 'Coupon+Checkout' -Step 'Apply invalid coupon' -Res $r

    $orderPayload = @{name='QA User'; phone='9999999999'; email=$email; address='Street 1'; city='Pune'; pincode='411001'; payment='COD'; items=@(@{productId='1'; title='Test Product'; qty=1; size='M'; color='Black'})}
    $orderNoCoupon = Invoke-Api -Method 'POST' -Url "$base/orders" -Body $orderPayload
    Add-Test -Flow 'Coupon+Checkout' -Step 'Order placement without coupon' -Res $orderNoCoupon

    $orderCouponPayload = @{name='QA User'; phone='9999999999'; email=$email; address='Street 1'; city='Pune'; pincode='411001'; payment='COD'; couponCode='SAVE10'; items=@(@{productId='1'; title='Test Product'; qty=1; size='M'; color='Black'})}
    $orderWithCoupon = Invoke-Api -Method 'POST' -Url "$base/orders" -Body $orderCouponPayload
    Add-Test -Flow 'Coupon+Checkout' -Step 'Order placement with coupon' -Res $orderWithCoupon

    $orderId = 0
    $orderNumber = ''
    if ($orderNoCoupon.Status -eq 200) {
        try {
            $obj = $orderNoCoupon.Body | ConvertFrom-Json
            $orderId = [int]$obj.id
            $orderNumber = [string]$obj.orderNumber
        }
        catch {}
    }

    # 5. Order Flow
    $r = Invoke-Api -Method 'GET' -Url "$base/orders/me"
    Add-Test -Flow 'Order' -Step '/orders/me without login' -Res $r

    if (-not [string]::IsNullOrWhiteSpace($token)) {
        $r = Invoke-Api -Method 'GET' -Url "$base/orders/me" -Token $token
        Add-Test -Flow 'Order' -Step '/orders/me with login' -Res $r
    }

    $r = Invoke-Api -Method 'POST' -Url "$base/orders/track" -Body @{orderId='99999999'; contact='nobody@example.com'}
    Add-Test -Flow 'Order' -Step 'Track order with invalid identity/contact' -Res $r

    if ($orderId -gt 0) {
        $r = Invoke-Api -Method 'POST' -Url "$base/orders/track" -Body @{orderId="$orderId"; contact=$email}
        Add-Test -Flow 'Order' -Step 'Track order with valid identity/contact' -Res $r
    }

    # 6. Return Flow
    $r = Invoke-Api -Method 'POST' -Url "$base/returns" -Body @{orderId=1; itemProductId='1'; reason='Size issue'; customerRemark='Need exchange'}
    Add-Test -Flow 'Return' -Step 'Unauthorized return attempt' -Res $r

    if (-not [string]::IsNullOrWhiteSpace($token) -and $orderId -gt 0) {
        $r = Invoke-Api -Method 'POST' -Url "$base/returns" -Token $token -Body @{orderId=$orderId; itemProductId='1'; reason='Size issue'; customerRemark='Need exchange'}
        Add-Test -Flow 'Return' -Step 'Return request from logged-in user' -Res $r
    }

    # 7. Support Flow
    $r = Invoke-Api -Method 'POST' -Url "$base/support/requests" -Body @{name='Guest User'; contact='9999999999'; subject='Need help'; orderId=''; message='Support request test'}
    Add-Test -Flow 'Support' -Step 'Create support request guest' -Res $r

    $r = Invoke-Api -Method 'POST' -Url "$base/support/requests" -Body @{name='Guest User'; contact='9999999999'; subject='Need help'; orderId=''; message=''}
    Add-Test -Flow 'Support' -Step 'Create support request with invalid payload' -Res $r

    $r = Invoke-Api -Method 'GET' -Url "$base/support/requests/my"
    Add-Test -Flow 'Support' -Step 'View my support history without login' -Res $r

    if (-not [string]::IsNullOrWhiteSpace($token)) {
        $r = Invoke-Api -Method 'GET' -Url "$base/support/requests/my" -Token $token
        Add-Test -Flow 'Support' -Step 'View my support history with login' -Res $r
    }

    # 8. Address Flow
    $r = Invoke-Api -Method 'POST' -Url "$base/addresses" -Body @{fullName='QA'; phone='9999999999'; city='Pune'; state='MH'; pincode='411001'; street='Street'; line2=''; landmark=''; addressType='Home'; isDefault=$true}
    Add-Test -Flow 'Address' -Step 'Add address without login' -Res $r

    $addressId = 0
    if (-not [string]::IsNullOrWhiteSpace($token)) {
        $r = Invoke-Api -Method 'POST' -Url "$base/addresses" -Token $token -Body @{fullName='QA'; phone='9999999999'; city='Pune'; state='MH'; pincode='411001'; street='Street'; line2=''; landmark=''; addressType='Home'; isDefault=$true}
        Add-Test -Flow 'Address' -Step 'Add address with login' -Res $r
        if ($r.Status -eq 200) {
            try { $addressId = [int](($r.Body | ConvertFrom-Json).id) } catch {}
        }

        if ($addressId -gt 0) {
            $r = Invoke-Api -Method 'PATCH' -Url "$base/addresses/$addressId" -Token $token -Body @{fullName='QA Updated'; phone='8888888888'; city='Mumbai'; state='MH'; pincode='400001'; street='Street 2'; line2='Near Park'; landmark='Park'; addressType='Work'; isDefault=$false}
            Add-Test -Flow 'Address' -Step 'Edit address' -Res $r

            $r = Invoke-Api -Method 'DELETE' -Url "$base/addresses/$addressId" -Token $token
            Add-Test -Flow 'Address' -Step 'Delete address' -Res $r
        }
    }

    # 9. Wishlist Flow
    $r = Invoke-Api -Method 'POST' -Url "$base/wishlist" -Body @{productId=1}
    Add-Test -Flow 'Wishlist' -Step 'Add to wishlist without login' -Res $r

    if (-not [string]::IsNullOrWhiteSpace($token)) {
        $r = Invoke-Api -Method 'POST' -Url "$base/wishlist" -Token $token -Body @{productId=1}
        Add-Test -Flow 'Wishlist' -Step 'Add to wishlist with login' -Res $r

        $r = Invoke-Api -Method 'POST' -Url "$base/wishlist" -Token $token -Body @{productId=1}
        Add-Test -Flow 'Wishlist' -Step 'Duplicate add to wishlist' -Res $r

        $r = Invoke-Api -Method 'DELETE' -Url "$base/wishlist/1" -Token $token
        Add-Test -Flow 'Wishlist' -Step 'Remove from wishlist' -Res $r
    }

    # 10. Profile Flow
    $r = Invoke-Api -Method 'GET' -Url "$base/api/Auth/me"
    Add-Test -Flow 'Profile' -Step 'Profile data load without login' -Res $r

    if (-not [string]::IsNullOrWhiteSpace($token)) {
        $r = Invoke-Api -Method 'GET' -Url "$base/api/Auth/me" -Token $token
        Add-Test -Flow 'Profile' -Step 'Profile data load with login' -Res $r
    }

    # 11. Admin Flow
    $adminToken = ''
    $adminLogin = Invoke-Api -Method 'POST' -Url "$base/api/Auth/login" -Body @{email='admin@yogitafashion.com'; password='ChangeMe@123'}
    Add-Test -Flow 'Admin' -Step 'Admin login' -Res $adminLogin

    if ($adminLogin.Status -eq 200) {
        try { $adminToken = [string](($adminLogin.Body | ConvertFrom-Json).token) } catch {}
    }

    $r = Invoke-Api -Method 'GET' -Url "$base/audit-logs"
    Add-Test -Flow 'Admin' -Step 'Audit logs without admin token' -Res $r

    if (-not [string]::IsNullOrWhiteSpace($adminToken)) {
        $r = Invoke-Api -Method 'GET' -Url "$base/audit-logs" -Token $adminToken
        Add-Test -Flow 'Admin' -Step 'Audit logs with admin token' -Res $r

        $r = Invoke-Api -Method 'POST' -Url "$base/coupons" -Token $adminToken -Body @{code='QA10'; type='percent'; value=10; minOrderAmount=100; maxUses=10; maxUsesPerUser=1; isActive=$true}
        Add-Test -Flow 'Admin' -Step 'Coupon create' -Res $r

        $r = Invoke-Api -Method 'POST' -Url "$base/products" -Token $adminToken -Body @{name='QA Product'; description='desc'; category='Cat'; brand='Brand'; price=199; originalPrice=299; stock=10; imageUrl='https://example.com/a.jpg'}
        Add-Test -Flow 'Admin' -Step 'Product create' -Res $r

        $r = Invoke-Api -Method 'PATCH' -Url "$base/support/requests/1/status" -Token $adminToken -Body @{status='Closed'}
        Add-Test -Flow 'Admin' -Step 'Support status update endpoint check' -Res $r
    }

    # 12. UI runtime checks (dev server startup + page availability)
    $storeOut = "$root\\logs\\qa-store.out.log"
    $storeErr = "$root\\logs\\qa-store.err.log"
    $adminOut = "$root\\logs\\qa-admin.out.log"
    $adminErr = "$root\\logs\\qa-admin.err.log"

    foreach ($f in @($storeOut,$storeErr,$adminOut,$adminErr)) {
        if (Test-Path $f) { Remove-Item $f -Force }
    }

    $storeProc = Start-Process -FilePath npm.cmd -ArgumentList 'run','dev','--','--host','127.0.0.1','--port','5173','--strictPort' -WorkingDirectory "$root\\store-frontend" -PassThru -RedirectStandardOutput $storeOut -RedirectStandardError $storeErr
    $adminProc = Start-Process -FilePath npm.cmd -ArgumentList 'run','dev','--','--host','127.0.0.1','--port','5174','--strictPort' -WorkingDirectory "$root\\admin-frontend" -PassThru -RedirectStandardOutput $adminOut -RedirectStandardError $adminErr

    $storeReady = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 2
        $probe = Invoke-Api -Method 'GET' -Url 'http://127.0.0.1:5173'
        if ($probe.Status -eq 200) { $storeReady = $true; Add-Test -Flow 'UI Store' -Step 'Store frontend root load' -Res $probe; break }
    }

    $adminReady = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 2
        $probe = Invoke-Api -Method 'GET' -Url 'http://127.0.0.1:5174'
        if ($probe.Status -eq 200) { $adminReady = $true; Add-Test -Flow 'UI Admin' -Step 'Admin frontend root load' -Res $probe; break }
    }

    if ($storeReady) {
        foreach ($path in @('/','/login','/register','/cart','/checkout','/orders','/support','/track-order')) {
            $r = Invoke-Api -Method 'GET' -Url ("http://127.0.0.1:5173" + $path)
            Add-Test -Flow 'UI Store' -Step ("Store page load " + $path) -Res $r
        }
    }
    else {
        $storeLogs = ''
        if (Test-Path $storeOut) { $storeLogs += ((Get-Content $storeOut -Tail 60) -join "`n") }
        if (Test-Path $storeErr) { $storeLogs += "`n" + ((Get-Content $storeErr -Tail 60) -join "`n") }
        Add-Test -Flow 'UI Store' -Step 'Store frontend failed to start' -Res ([PSCustomObject]@{Status=0;Body=$storeLogs})
    }

    if ($adminReady) {
        foreach ($path in @('/','/login','/dashboard','/products','/orders','/returns','/support-requests')) {
            $r = Invoke-Api -Method 'GET' -Url ("http://127.0.0.1:5174" + $path)
            Add-Test -Flow 'UI Admin' -Step ("Admin page load " + $path) -Res $r
        }
    }
    else {
        $adminLogs = ''
        if (Test-Path $adminOut) { $adminLogs += ((Get-Content $adminOut -Tail 60) -join "`n") }
        if (Test-Path $adminErr) { $adminLogs += "`n" + ((Get-Content $adminErr -Tail 60) -join "`n") }
        Add-Test -Flow 'UI Admin' -Step 'Admin frontend failed to start' -Res ([PSCustomObject]@{Status=0;Body=$adminLogs})
    }

    # 13. DB consistency via runtime observations
    $r = Invoke-Api -Method 'GET' -Url "$base/products"
    Add-Test -Flow 'DatabaseConsistency' -Step 'Post-operation data readback probe /products' -Res $r

    if (-not [string]::IsNullOrWhiteSpace($adminToken)) {
        $r = Invoke-Api -Method 'GET' -Url "$base/audit-logs?page=1&pageSize=20" -Token $adminToken
        Add-Test -Flow 'DatabaseConsistency' -Step 'Audit log readback after critical actions' -Res $r
    }

}
finally {
    foreach ($proc in @($storeProc,$adminProc,$backendProc)) {
        if ($null -ne $proc) {
            try {
                if (-not $proc.HasExited) {
                    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                }
            }
            catch {}
        }
    }
}

$tests | ConvertTo-Json -Depth 8
