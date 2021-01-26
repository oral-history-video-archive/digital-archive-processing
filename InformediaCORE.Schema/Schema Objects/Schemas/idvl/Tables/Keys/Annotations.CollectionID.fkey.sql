-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Annotations]
	ADD CONSTRAINT [FK_Annotations_CollectionID] 
	FOREIGN KEY ([CollectionID])
	REFERENCES  [idvl].[Collections] ([CollectionID])	
	ON DELETE CASCADE
