-- InformediaCORE Database Schema

CREATE TABLE [idvl].[Movies]
(
	[MovieID]		    INT IDENTITY(1,1) NOT NULL,			-- PK
	[MovieName]		    VARCHAR(64) NOT NULL,			    -- UK - Unique movie name
	[CollectionID]	    INT NOT NULL,					    -- FK -> Collections
	[SessionID]		    INT NOT NULL,					    -- FK -> Sessions
	[Abstract]		    VARCHAR(1024) NOT NULL,				-- Display friendly movie description / abstract.
	[Tape]			    INT NOT NULL,					    -- From Densho/TheHistoryMakers attributions.
	[MediaPath]		    VARCHAR(255) NOT NULL,				-- Fully qualified path to the source media.
	[FileType]		    VARCHAR(16) NOT NULL,			    -- File extension of the source media.
	[Duration]		    INT NOT NULL,					    -- Duration of source media specified in milliseconds.
	[Width]			    INT NOT NULL,					    -- Width of source media in pixels.
	[Height]		    INT NOT NULL,					    -- Height of source media in pixels.
	[FPS]			    FLOAT NOT NULL,						-- Frames per second of source media.
	[Created]		    DATETIME2(0) NULL,					-- Set by trigger
	[Modified]		    DATETIME2(0) NULL					-- Set by trigger
)
