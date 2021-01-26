-- InformediaCORE Database Schema

ALTER TABLE [idvl].[PartitionMembers]
	ADD CONSTRAINT [PK_PartitionMembers_CollectionID_PartitionID]
	PRIMARY KEY ([CollectionID],[PartitionID])