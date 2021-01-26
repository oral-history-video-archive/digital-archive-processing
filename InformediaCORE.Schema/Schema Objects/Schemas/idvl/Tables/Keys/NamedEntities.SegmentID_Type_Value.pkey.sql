-- InformediaCORE Database Schema

ALTER TABLE [idvl].[NamedEntities]
	ADD CONSTRAINT [PK_NamedEntities_SegmentID_Type_Value]
	PRIMARY KEY  ([SegmentID],[Type],[Value])
