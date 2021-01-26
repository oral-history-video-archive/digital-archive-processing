-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Annotations]
	ADD CONSTRAINT [FK_Annotations_MovieID] 
	FOREIGN KEY ([MovieID])
	REFERENCES [idvl].[Movies] ([MovieID])
	ON DELETE CASCADE
