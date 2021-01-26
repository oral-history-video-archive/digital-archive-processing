-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Semaphores]
    ADD CONSTRAINT [UK_Semaphores_PID_Hostname]
    UNIQUE ([PID],[Hostname])