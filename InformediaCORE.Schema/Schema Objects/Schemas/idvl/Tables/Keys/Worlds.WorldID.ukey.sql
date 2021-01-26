-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Worlds]
    ADD CONSTRAINT [UK_Worlds_WorldID]
    UNIQUE ([WorldID])