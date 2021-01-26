-- InformediaCORE Database Schema

CREATE TABLE [idvl].[TagImportStatus]
(
	[SegmentID] INT NOT NULL,                       -- PK - FK -> Segments
    [FirebaseTimestamp] INT NULL,                   -- From Firebase, indicates when tags were last edited
    [LastChecked] DATETIME2(0) NULL,                -- When tags were last checked by processing
    [LastStatus]  NVARCHAR(128) NULL,               -- Status of last check - i.e. Error, NullResult, HTTP 500
    [LastUpdated] DATETIME2(0) NULL                 -- When tags were last updated by processing
)
