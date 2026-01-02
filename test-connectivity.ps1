# Script para testar conectividade com srv01.automais.io
# Execute: .\test-connectivity.ps1

Write-Host "`n🔍 Testando Conectividade com srv01.automais.io`n" -ForegroundColor Cyan

# Testar ChirpStack
Write-Host "1️⃣  Testando ChirpStack (porta 8080)..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://srv01.automais.io:8080" -TimeoutSec 5 -ErrorAction Stop
    Write-Host "   ✅ ChirpStack está acessível (Status: $($response.StatusCode))" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 401) {
        Write-Host "   ✅ ChirpStack está acessível (requer autenticação)" -ForegroundColor Green
    } else {
        Write-Host "   ❌ ChirpStack não está acessível: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Testar EMQX Dashboard
Write-Host "`n2️⃣  Testando EMQX Dashboard (porta 18083)..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://srv01.automais.io:18083" -TimeoutSec 5 -ErrorAction Stop
    Write-Host "   ✅ EMQX Dashboard está acessível (Status: $($response.StatusCode))" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 401) {
        Write-Host "   ✅ EMQX Dashboard está acessível (requer autenticação)" -ForegroundColor Green
    } else {
        Write-Host "   ❌ EMQX Dashboard não está acessível: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Testar porta MQTT
Write-Host "`n3️⃣  Testando porta MQTT (1883)..." -ForegroundColor Yellow
$mqttTest = Test-NetConnection -ComputerName srv01.automais.io -Port 1883 -InformationLevel Quiet -WarningAction SilentlyContinue
if ($mqttTest) {
    Write-Host "   ✅ Porta MQTT 1883 está aberta" -ForegroundColor Green
} else {
    Write-Host "   ❌ Porta MQTT 1883 não está acessível" -ForegroundColor Red
}

# Testar porta WebSocket MQTT
Write-Host "`n4️⃣  Testando porta WebSocket MQTT (8083)..." -ForegroundColor Yellow
$wsTest = Test-NetConnection -ComputerName srv01.automais.io -Port 8083 -InformationLevel Quiet -WarningAction SilentlyContinue
if ($wsTest) {
    Write-Host "   ✅ Porta WebSocket 8083 está aberta" -ForegroundColor Green
} else {
    Write-Host "   ❌ Porta WebSocket 8083 não está acessível" -ForegroundColor Red
}

# Resumo
Write-Host "`n" -NoNewline
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✨ Testes Concluídos!" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor Cyan

Write-Host "`n📋 URLs Úteis:" -ForegroundColor Yellow
Write-Host "   • ChirpStack:      http://srv01.automais.io:8080" -ForegroundColor White
Write-Host "   • EMQX Dashboard:  http://srv01.automais.io:18083" -ForegroundColor White
Write-Host "   • MQTT Broker:     mqtt://srv01.automais.io:1883" -ForegroundColor White
Write-Host ""

