-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Sessions]
	ADD CONSTRAINT [PK_Sessions_CollectionID_SessionOrder]
	PRIMARY KEY ([CollectionID],[SessionOrder])