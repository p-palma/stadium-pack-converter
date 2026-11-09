@echo off
rem Builds "stadium-pack-converter.exe" with the C# compiler that ships with Windows (.NET Framework 4).
cd /d "%~dp0"
set RES=/resource:CREDITS.txt,CREDITS.txt
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 /win32icon:app.ico /win32manifest:app.manifest /out:"stadium-pack-converter.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll %RES% StadiumPackConverter.cs
