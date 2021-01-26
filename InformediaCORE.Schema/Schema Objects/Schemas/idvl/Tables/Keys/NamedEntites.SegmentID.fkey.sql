-- InformediaCORE Database Schema

ALTER TABLE [idvl].[NamedEntities]
	ADD CONSTRAINT [FK_NamedEntities_SegmentID]
	FOREIGN KEY ([SegmentID])
	REFERENCES [idvl].[Segments] ([SegmentID])
	ON DELETE CASCADE