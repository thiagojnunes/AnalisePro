@echo off
echo ==============================================
echo   Compilando AnalisePro (Versao Portatil .exe)
echo ==============================================

set MSBUILD="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"

if not exist %MSBUILD% (
    echo [ERRO] MSBuild nao encontrado no caminho padrao do Windows.
    pause
    exit /b 1
)

%MSBUILD% AnalisePro.csproj /p:Configuration=Release /v:minimal

if %ERRORLEVEL% NEQ 0 (
    echo [ERRO] Falha na compilacao!
    pause
    exit /b %ERRORLEVEL%
)

copy /y bin\Release\AnalisePro.exe AnalisePro.exe >nul
echo.
echo [SUCESSO] Executavel portatil atualizado: AnalisePro.exe
echo Tamanho: 
dir AnalisePro.exe | findstr AnalisePro.exe
echo.
