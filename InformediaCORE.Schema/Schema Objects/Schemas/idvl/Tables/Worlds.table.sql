-- InformediaCORE Database Schema

CREATE TABLE [idvl].[Worlds]
(
	[WorldID]		    INT IDENTITY(1,1) NOT NULL,			-- PK
	[Name]			    VARCHAR(64) NOT NULL,			    -- UK - Unique world name
	[Description]	    VARCHAR(128) NULL				    -- Friendly description
)
