-- InformediaCORE Database Schema

CREATE TABLE [idvl].[Partitions]
(
	[PartitionID]		INT IDENTITY(1,1) NOT NULL,			-- PK
	[WorldID]			INT NOT NULL,						-- FK -> Worlds
	[Name]				VARCHAR(64) NOT NULL,				-- UK -> Unique partition name.
	[Description]		VARCHAR(128) NULL					-- Human friendly description.
)
