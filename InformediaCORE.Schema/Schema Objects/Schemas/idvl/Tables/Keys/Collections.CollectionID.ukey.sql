-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Collections]
    ADD CONSTRAINT [UK_Collections_CollectionID]
    UNIQUE ([CollectionID])