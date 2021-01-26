-- InformediaCORE Database Schema

CREATE TABLE [idvl].[Sessions]
(
	[SessionID]			INT IDENTITY(1,1) NOT NULL,			-- PK
	[CollectionID]		INT NOT NULL,						-- FK -> Collection
	[SessionOrder]		INT NOT NULL,						-- Relative order of this session within the collection
	[Interviewer]		VARCHAR(128) NOT NULL,				-- Name of person who conducted the interview
	[InterviewDate]		DATE NOT NULL,						-- Date interview was conducted
	[Location]			VARCHAR(256) NOT NULL,				-- Location where interview was conducted
	[Videographer]		VARCHAR(128) NOT NULL DEFAULT(''),	-- Session videographer
	[Sponsor]			VARCHAR(64) NULL,					-- (Optional) Interview sponsor
	[SponsorURL]		VARCHAR(128) NULL,					-- (Optional) URL to sponsor's website
	[SponsorImage]		VARBINARY(MAX) NULL,				-- (Optional) Sponsor picture or logo
	[ImageType]			CHAR(3) NULL,						-- (Optional) Sponsor image type (jpg, png, gif)
	[Created]		    DATETIME2(0) NULL,					-- Set by trigger
	[Modified]		    DATETIME2(0) NULL,					-- Set by trigger
	[Published]		    DATETIME2(0) NULL,					-- When the session was published.
	[Phase]				CHAR(1) NOT NULL DEFAULT('D')		-- Current phase within the publishing cycle
)
