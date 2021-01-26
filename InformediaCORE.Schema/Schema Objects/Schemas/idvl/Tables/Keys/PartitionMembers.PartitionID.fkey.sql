-- InformediaCORE Database Schema

ALTER TABLE [idvl].[PartitionMembers]
	ADD CONSTRAINT [FK_PartitionMembers_PartitionID] 
	FOREIGN KEY ([PartitionID])
	REFERENCES [idvl].[Partitions] ([PartitionID])

