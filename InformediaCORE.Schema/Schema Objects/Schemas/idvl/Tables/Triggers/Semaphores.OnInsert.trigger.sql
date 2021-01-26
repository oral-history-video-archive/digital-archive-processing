-- InformediaCORE Database Schema

CREATE TRIGGER [idvl].[Semaphores_OnInsert]
ON [idvl].[Semaphores]
AFTER INSERT
AS 
BEGIN
    SET NOCOUNT ON;

	UPDATE [Semaphores]
	SET
		[Created]	= GETDATE()
	FROM inserted AS I
		INNER JOIN [idvl].[Semaphores] AS S
		ON S.SegmentID = I.SegmentID;
END;
