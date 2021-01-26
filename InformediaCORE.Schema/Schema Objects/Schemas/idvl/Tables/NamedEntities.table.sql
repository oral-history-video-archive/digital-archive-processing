CREATE TABLE [idvl].[NamedEntities]
(
	[SegmentID]			INT NOT NULL,						-- FK - The segment this instance belongs to.
	[Type]				VARCHAR(32) NOT NULL,				-- Identifies the type of entity
	[Value]				VARCHAR(32) NOT NULL				-- The actual text of the named entity.
)
