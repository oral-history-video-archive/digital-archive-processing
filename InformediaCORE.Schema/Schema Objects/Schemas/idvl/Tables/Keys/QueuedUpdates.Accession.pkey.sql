-- InformediaCORE Database Schema

ALTER TABLE [idvl].[QueuedUpdates]
	ADD CONSTRAINT [PK_QueuedUpdates_Accession]
	PRIMARY KEY ([Accession])