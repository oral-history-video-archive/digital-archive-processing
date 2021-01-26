-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Partitions]
    ADD CONSTRAINT [UK_Partitions_PartitionID]
    UNIQUE ([PartitionID])