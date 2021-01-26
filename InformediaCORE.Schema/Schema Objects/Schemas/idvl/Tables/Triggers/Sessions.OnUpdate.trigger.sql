-- InformediaCORE Database Schema

CREATE TRIGGER [idvl].[Sessions_OnUpdate]
ON [idvl].[Sessions]
AFTER UPDATE AS
IF NOT (UPDATE(Published) OR UPDATE(Phase))
BEGIN
	SET NOCOUNT ON;

	DECLARE	@NOW datetime;
	SELECT	@NOW = GetDate();

	IF UPDATE(Phase)
	BEGIN		
		-- Reset the PublishingDate to NULL
		UPDATE [Sessions]
		SET
			Published = NULL
		FROM [idvl].[Sessions] AS S
			INNER JOIN inserted AS I
			ON S.SessionID = I.SessionID;

		-- Set Published date only when the state changes to 'P'
		UPDATE [Sessions]
		SET
			[Published] = @NOW
		FROM [idvl].[Sessions] AS S
			INNER JOIN deleted AS D
			ON S.SessionID = D.SessionID
			WHERE S.Phase = 'P';
	END;

	UPDATE [Sessions]
	SET
		[Modified]	= @NOW
	FROM inserted AS I
		INNER JOIN [idvl].[Sessions] AS S
		ON S.SessionID = I.SessionID;

	-- CHANGES TO A SESSION PROPAGATE UP TO THE PARENT COLLECTION
	UPDATE [Collections]
	SET
		[Modified]	= @NOW
	FROM inserted AS I
		INNER JOIN [idvl].[Collections] AS C
		ON C.CollectionID = I.CollectionID;
END;