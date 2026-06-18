@echo off
rem Wrapper podpisuj??cy wywo??ywany przez Inno Setup (SignTool=winmdsign).
rem Generowany automatycznie przez build.ps1 ??? nie edytuj r??cznie.
rem %* = lista plik??w przekazana przez Inno (placeholder $f).
"C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages\microsoft.windows.sdk.buildtools\10.0.26100.7705\bin\10.0.26100.0\x64\signtool.exe" sign /sm /sha1 73BB4C564E8A159034F854A6840CC04E5F77614C /fd sha256 /tr http://timestamp.digicert.com /td sha256 %*
