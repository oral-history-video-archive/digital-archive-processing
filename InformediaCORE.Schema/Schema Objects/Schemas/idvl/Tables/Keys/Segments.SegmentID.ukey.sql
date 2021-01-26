-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Segments]
    ADD CONSTRAINT [UK_Segments_SegmentID]
    UNIQUE ([SegmentID])