-- InformediaCORE Database Schema

ALTER TABLE [idvl].[TagImportStatus]
    ADD CONSTRAINT [FK_TagImportStatus_SegmentID]
    FOREIGN KEY ([SegmentID])
    REFERENCES [idvl].[Segments] ([SegmentID])
    ON DELETE CASCADE