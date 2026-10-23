@echo off
title Enviando AnalisePro para o GitHub
echo ========================================================
echo   Enviando AnalisePro para o GitHub (thiagojnunes)
echo ========================================================
echo.
"C:\Program Files\Git\cmd\git.exe" push -u origin main --force
echo.
if %ERRORLEVEL% EQU 0 (
    echo [SUCESSO] Codigo enviado com exito para o GitHub!
) else (
    echo [AVISO] Se precisar autenticar, siga as instrucoes na tela.
)
echo.
pause
