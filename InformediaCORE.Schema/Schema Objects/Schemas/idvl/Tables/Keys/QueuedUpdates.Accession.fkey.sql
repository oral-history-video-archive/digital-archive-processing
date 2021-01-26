-- InformediaCORE Database Schema

ALTER TABLE [idvl].[QueuedUpdates]
	ADD CONSTRAINT [FK_QueuedUpdates_Accession]
	FOREIGN KEY ([Accession])
	REFERENCES [idvl].[Collections] ([Accession])
	ON DELETE CASCADE