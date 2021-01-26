-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Segments]
	ADD CONSTRAINT [FK_Segments_MovieID] 
	FOREIGN KEY ([MovieID])
	REFERENCES [idvl].[Movies] ([MovieID])	

