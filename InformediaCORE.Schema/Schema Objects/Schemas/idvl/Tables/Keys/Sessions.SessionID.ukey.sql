-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Sessions]
    ADD CONSTRAINT [UK_Sessions_SessionID]
    UNIQUE ([SessionID])