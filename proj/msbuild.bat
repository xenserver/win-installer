call "%VS%\VC\vcvarsall.bat" x86
msbuild.exe /p:Configuration="%CONFIGURATION%" /p:Platform="%PLATFORM%" /t:"%TARGET%" %SOLUTION%.sln

