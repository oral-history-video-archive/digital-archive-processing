-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Movies]
	ADD CONSTRAINT [FK_Movies_SessionID] 
	FOREIGN KEY ([SessionID])
	REFERENCES [idvl].[Sessions] ([SessionID])	

