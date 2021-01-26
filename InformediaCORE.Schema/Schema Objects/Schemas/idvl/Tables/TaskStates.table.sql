-- InformediaCORE Database Schema

CREATE TABLE [idvl].[TaskStates]
(
    [SegmentID]         INT NOT NULL,					    -- PK - FK -> Segments
    [Name]              VARCHAR(32) NOT NULL,			    -- PK - Joint primary key with SegmentID
    [State]             CHAR(1),						    -- Pending -> Queued -> Running -> [Complete | Failed]
    [Modified]          DATETIME2						    -- Updated by trigger
)
