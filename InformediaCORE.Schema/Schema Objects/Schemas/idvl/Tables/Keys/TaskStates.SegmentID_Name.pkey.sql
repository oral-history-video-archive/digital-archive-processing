-- InformediaCORE Database Schema

ALTER TABLE [idvl].[TaskStates]
	ADD CONSTRAINT [PK_TaskStates_SegmentID_Name]
	PRIMARY KEY ([SegmentID],[Name])