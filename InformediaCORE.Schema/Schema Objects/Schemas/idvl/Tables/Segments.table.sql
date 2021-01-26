-- InformediaCORE Database Schema

CREATE TABLE [idvl].[Segments]
(
	[SegmentID]		    INT IDENTITY(1,1) NOT NULL,			-- UK
	[SegmentName]	    VARCHAR(80) NOT NULL,			    -- PK - Unique segment name
	[CollectionID]	    INT NOT NULL,					    -- FK -> Collections
	[SessionID]		    INT NOT NULL,					    -- FK -> Sessions
	[MovieID]		    INT NOT NULL,					    -- FK -> Movies
	[Title]			    VARCHAR(256) NOT NULL,				-- Display friendly segment title.
	[Abstract]		    VARCHAR(1024) NOT NULL,				-- Display friendly segment description / abstract.
	[StartTime]		    INT NOT NULL,					    -- Start of segment within parent media specified in milliseconds.
	[EndTime]		    INT NOT NULL,					    -- End of segment within parent media specified in milliseconds.
	[MediaPath]		    VARCHAR(255) NULL,					-- Fully qualified path to segment media.
	[Duration]		    INT NULL,						    -- Duration of segment media specified in milliseconds.
	[Width]			    INT NULL,						    -- Width of segment media in pixels.
	[Height]		    INT NULL,						    -- Height of segment media in pixels.
	[FPS]			    FLOAT NULL,							-- Frames per second of segment media.
	[URL]			    VARCHAR(255) NULL,					-- Fully qualified URL to deployed media.
	[SegmentOrder]	    INT NULL,						    -- Order of segment as it occurs within the interview.
	[NextSegmentID]     INT NULL,						    -- ID of next segment in the interview.
	[PrevSegmentID]     INT NULL,						    -- ID of the previous segment in the interview.
	[TranscriptLength]  INT NULL,					        -- Length of transcript in characters.
	[TranscriptText]    NTEXT NULL,							-- Transcript text corresponding to the segment.
	[TranscriptSync]    VARBINARY(MAX) NULL,				-- A serialized TSync object containing syncrhonization data.
	[Keyframe]		    VARBINARY(MAX) NULL,				-- A serialized keyframe image.
	[Created]		    DATETIME2(0) NULL,					-- Set by trigger
	[Modified]		    DATETIME2(0) NULL,					-- Set by trigger
	[Ready]			    CHAR(1) NOT NULL DEFAULT('N')		-- Processing is complete, ready for indexing.
)
