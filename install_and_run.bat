@echo off
chcp 65001 >nul
echo Markdown to PDF Converter - 起動チェック
echo ============================================
echo.

REM WebView2 Runtimeのチェック
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" >nul 2>&1
if %errorlevel% equ 0 (
	echo ✓ WebView2 Runtime: インストール済み
	goto :run_app
)

echo ⚠ WebView2 Runtimeがインストールされていません
echo.
echo WebView2 Runtimeをインストールしますか？
choice /c YN /n /m "[Y] はい  [N] いいえ: "

if errorlevel 2 goto :end
if errorlevel 1 goto :install_webview2

:install_webview2
echo.
echo WebView2 Runtimeをダウンロードしています...
start https://go.microsoft.com/fwlink/p/?LinkId=2124703
echo.
echo インストール後、このウィンドウを閉じてもう一度実行してください。
pause
goto :end

:run_app
echo.
echo アプリを起動します...
start "" "%~dp0MK_to_PDF.exe"
timeout /t 2 >nul

:end
