-- InformediaCORE Database Schema

ALTER TABLE [idvl].[AnnotationTypes]
    ADD CONSTRAINT [UK_AnnotationTypes_TypeID]
    UNIQUE ([TypeID])