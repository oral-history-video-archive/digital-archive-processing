-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Segments]
	ADD CONSTRAINT [FK_Segments_CollectionID] 
	FOREIGN KEY (CollectionID)
	REFERENCES [idvl].[Collections] ([CollectionID])	

