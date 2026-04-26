xcopy bin %1 /D /E /I /Q /Y /EXCLUDE:excludelist.txt
xcopy core\res %1\res /D /E /I /Q /Y /EXCLUDE:excludelist.txt
xcopy plugins %1\plugins /D /E /I /Q /Y /EXCLUDE:excludelist.txt
xcopy doc\*.* %1 /D /E /I /Q /Y /EXCLUDE:excludelist.txt
xcopy plugins\jp.co.tripod.chiname.lib\src\DummyCars\bin\*.dll %1\plugins\jp.co.tripod.chiname.lib
xcopy plugins\jp.co.tripod.chiname.lib\src\RoadAccessory\bin\*.dll %1\plugins\jp.co.tripod.chiname.lib
pause