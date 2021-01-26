-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Segments]
	ADD CONSTRAINT [FK_Segments_SessionID] 
	FOREIGN KEY (SessionID)
	REFERENCES [idvl].[Sessions] (SessionID)	

