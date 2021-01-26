-- InformediaCORE Database Schema

CREATE TABLE [idvl].[AnnotationTypes]
(
    [TypeID]			INT IDENTITY(1,1) NOT NULL,			-- UK - Unique identifier for table linking.
    [Name]				VARCHAR(32) NOT NULL,				-- PK - Type name is primary key.
    [Scope]				CHAR(1) NOT NULL,					-- C=Collection, M=Movie, S=Segment
    [Description]		VARCHAR(128) NOT NULL				-- Friendly description.
)