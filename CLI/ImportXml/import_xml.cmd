@ECHO OFF
REM 2017-10-12 bm3n@andrew.cmu.edu
REM Imports all the xml files in the given directory

IF [%1]==[] GOTO USAGE

REM Import AnnotationTypes
CALL :IMPORT %1\*.annotationType.xml

REM Import Worlds and Partitions
CALL :IMPORT %1\*.world.xml

REM Import Collections and Sessions
CALL :IMPORT %1\*.collection.xml

REM Import Movies and Segments
CALL :IMPORT %1\*.movie.xml

ECHO Done.
GOTO :EOF

:IMPORT

REM %1 = Set specifier

FOR %%i IN (%1) DO CALL ImportXml.exe "%%i"
GOTO :EOF

:USAGE
ECHO.
ECHO Missing parameter:
ECHO    You must specify the path to the directory containing the XML files.
ECHO.
ECHO Example:
ECHO   ^>IMPORT_XML path_to_xml
EXIT /B 1