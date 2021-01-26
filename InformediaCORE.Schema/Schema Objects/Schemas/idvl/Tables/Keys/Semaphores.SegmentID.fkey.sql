-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Semaphores]
	ADD CONSTRAINT [FK_Semaphores_SegmentID] 
	FOREIGN KEY ([SegmentID])
	REFERENCES [idvl].[Segments] ([SegmentID])	
	ON DELETE CASCADE
