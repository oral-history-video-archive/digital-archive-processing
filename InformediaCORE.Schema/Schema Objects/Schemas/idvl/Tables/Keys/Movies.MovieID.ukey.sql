-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Movies]
    ADD CONSTRAINT [UK_Movies_MovieID]
    UNIQUE ([MovieID])