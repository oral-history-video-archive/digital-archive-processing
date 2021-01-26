-- InformediaCORE Database Schema

CREATE TABLE [idvl].[Collections]
(
	[CollectionID]	    INT IDENTITY(1,1) NOT NULL,			-- PK
	[Accession]			VARCHAR(64) NOT NULL,		        -- UK - Unique accession number
	[DescriptionShort]	VARCHAR(1024) NOT NULL,				-- Display friendly short description about the collection
	[BiographyShort]	VARCHAR(2028) NOT NULL DEFAULT(''),	-- Display friendly short bio of the interview subject
	[FirstName]			VARCHAR(64) NOT NULL DEFAULT(''),	-- First name of interview subject
	[LastName]		    VARCHAR(64) NOT NULL,			    -- Last name of interview subject
	[PreferredName]	    VARCHAR(128) NOT NULL,				-- Display friendly name of narrator
	[Gender]		    CHAR NOT NULL,						-- F=Female, M=Male
	[WebsiteURL]		VARCHAR(128) NULL,					-- URL referring back to The HistoryMaker's website
	[Region]			VARCHAR(64) NULL,					-- Region of current residence
	[BirthCity]			VARCHAR(64) NULL,					-- City of birth
	[BirthState]		VARCHAR(64) NULL,					-- State of birth
	[BirthCountry]		VARCHAR(64) NULL,					-- Country of birth
	[BirthDate]			DATE NULL,							-- Narrator's full birth date	
	[DeceasedDate]		DATE NULL,							-- Narrator's full deceased date.
	[FileType]			CHAR(3) NULL,						-- Portrait image file type (jpg, png, gif)
	[Portrait]		    VARBINARY(MAX) NULL,				-- A serialized portrait image.
	[Created]		    DATETIME2(0) NULL,					-- Set by trigger
	[Modified]		    DATETIME2(0) NULL,					-- Set by trigger
	[Published]		    DATETIME2(0) NULL,					-- When the collection was published
	[Phase]				CHAR(1) NOT NULL DEFAULT('D')		-- Current phase within the publishing cycle
)