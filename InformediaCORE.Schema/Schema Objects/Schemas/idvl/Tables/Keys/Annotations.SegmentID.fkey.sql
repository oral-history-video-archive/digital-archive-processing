-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Annotations]
	ADD CONSTRAINT [FK_Annotations_SegmentID] 
	FOREIGN KEY ([SegmentID])
	REFERENCES [idvl].[Segments] ([SegmentID])
	ON DELETE CASCADE
