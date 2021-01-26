-- InformediaCORE Database Schema

CREATE TABLE [idvl].[Semaphores]
(
	[SegmentID]			INT NOT NULL,						-- PK - FK -> Segments 
	[PID]				INT NOT NULL,						-- UK\_ Joint unique contraint
	[Hostname]			VARCHAR(32) NOT NULL,				-- UK/ 
	[Created]			DATETIME2							-- Updated by trigger
)
