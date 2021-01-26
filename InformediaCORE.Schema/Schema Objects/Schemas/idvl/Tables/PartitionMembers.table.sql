-- InformediaCORE Database Schema

CREATE TABLE [idvl].[PartitionMembers]
(
	[CollectionID]		INT NOT NULL,						-- FK -> Collections \_ Joint PK
	[PartitionID]		INT NOT NULL						-- FK -> Partitions	 /
)
