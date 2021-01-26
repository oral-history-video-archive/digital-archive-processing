-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Movies]
	ADD CONSTRAINT [FK_Movies_CollectionID] 
	FOREIGN KEY ([CollectionID])
	REFERENCES [idvl].[Collections] ([CollectionID])	

