-- InformediaCORE Database Schema

ALTER TABLE [idvl].[TaskStates]
	ADD CONSTRAINT [FK_TaskStates_SegmentID] 
	FOREIGN KEY ([SegmentID])
	REFERENCES [idvl].[Segments] ([SegmentID])	
	ON DELETE CASCADE
