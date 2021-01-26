-- InformediaCORE Database Schema

CREATE TRIGGER [idvl].[Collections_OnUpdate]
ON [idvl].[Collections]
AFTER UPDATE 
AS 
IF NOT (UPDATE(Published) OR UPDATE(Phase))
BEGIN
    SET NOCOUNT ON

	UPDATE [Collections]
	SET
		[Modified]	= GETDATE()
	FROM inserted AS I
		INNER JOIN [idvl].[Collections] AS C
		ON C.CollectionID = I.CollectionID
END;
