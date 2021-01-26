@ECHO OFF
REM 2017-10-12 bm3n@andrew.cmu.edu
REM Updates an existing movie with the given .movie.xml file

IF [%1]==[] GOTO USAGE

REM Update Movie and Segments
CALL ImportXml.exe %1 /Update

ECHO Done.
GOTO :EOF

:USAGE
ECHO.
ECHO Missing parameter:
ECHO    You must provide the full path a .movie.xml file.
ECHO.
ECHO Example:
ECHO    ^>update_movie path_to.movie.xml
EXIT /B 1