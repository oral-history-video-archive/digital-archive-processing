-- InformediaCORE Database Schema

CREATE TABLE [idvl].[Annotations]
(
    [AnnotationID]		INT IDENTITY(1,1) NOT NULL,			-- PK
    [CollectionID]		INT NULL,							-- FK -> Collections
    [MovieID]			INT NULL,							-- FK -> Movies
    [SegmentID]			INT NULL,							-- FK -> Segments
    [TypeID]			INT NOT NULL,						-- FK -> AnnotationTypes
    [Value]				VARCHAR(1024)						-- The actual value of the annotation
)
