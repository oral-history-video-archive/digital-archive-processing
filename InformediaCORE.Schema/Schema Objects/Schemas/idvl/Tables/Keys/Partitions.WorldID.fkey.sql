-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Partitions]
	ADD CONSTRAINT [FK_Partitions_WorldID] 
	FOREIGN KEY ([WorldID])
	REFERENCES [idvl].[Worlds] ([WorldID])	

