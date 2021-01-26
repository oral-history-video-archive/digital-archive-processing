-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Sessions]
	ADD CONSTRAINT [FK_Sessions_CollectionID] 
	FOREIGN KEY ([CollectionID])
	REFERENCES [idvl].[Collections] ([CollectionID])	

