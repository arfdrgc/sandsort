@echo off
setlocal

set "scriptDir=%~dp0"
cd /d "%scriptDir%"

set "targetLink=%USERPROFILE%\.upmconfig.toml"
set "sourceFile=%CD%\upmconfig.toml"

if exist tempfile (
    del tempfile
)

if exist "%targetLink%" (

    dir %targetLink% | find "<SYMLINK>" 1>nul && (
       type nul > tempfile
    )
    if exist tempfile (
        echo old link found %targetLink% to %sourceFile%, replacing
        del "%targetLink%"
        del tempfile

    ) else (
        echo old token file found, backing up to %targetLink%_back
        move /y "%targetLink%" "%targetLink%_back"
    )
)


mklink "%targetLink%" "%sourceFile%"
endlocal
