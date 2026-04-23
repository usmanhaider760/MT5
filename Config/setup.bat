@echo off
echo ============================================
echo  MT5 Trading Bot — Setup
echo ============================================
echo.

:: Create signal folders
mkdir "C:\MT5Bot\signals\executed" 2>nul
mkdir "C:\MT5Bot\signals\rejected" 2>nul
mkdir "C:\MT5Bot\signals\error"    2>nul
echo [OK] Signal folders created: C:\MT5Bot\signals\

:: Create desktop shortcut (optional)
echo.
echo [OK] Setup complete.
echo.
echo NEXT STEPS:
echo 1. Build MT5TradingBot.csproj in Visual Studio
echo 2. Copy TradingBotEA.mq5 to MT5\MQL5\Experts\
echo 3. Compile the EA in MetaEditor (F7)
echo 4. Attach EA to a GBPUSD chart in MT5
echo 5. Enable AutoTrading (green button in MT5)
echo 6. Run MT5TradingBot.exe
echo 7. Click Connect
echo.
pause
