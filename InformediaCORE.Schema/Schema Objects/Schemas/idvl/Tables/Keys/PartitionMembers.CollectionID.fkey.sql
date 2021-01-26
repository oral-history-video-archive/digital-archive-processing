-- InformediaCORE Database Schema

ALTER TABLE [idvl].[PartitionMembers]
	ADD CONSTRAINT [FK_PartitionMembers_CollectionID] 
	FOREIGN KEY ([CollectionID])
	REFERENCES [idvl].[Collections] ([CollectionID])
	ON DELETE CASCADE

