-- InformediaCORE Database Schema

CREATE TABLE [idvl].[QueuedUpdates]
(
	[Accession]			VARCHAR(64) NOT NULL,		        -- PK - Unique accession number
	[Created]		    DATETIME2(0) NULL,					-- Set by trigger
)
