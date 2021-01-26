-- InformediaCORE Database Schema

CREATE TRIGGER [idvl].[Movies_OnUpdate]
ON [idvl].[Movies]
AFTER UPDATE 
AS 
BEGIN
	SET NOCOUNT ON;

	DECLARE	@NOW datetime;
	SELECT	@NOW = GetDate();

	UPDATE [Movies]
	SET
		[Modified]	= @NOW
	FROM inserted AS I
		INNER JOIN [idvl].[Movies] AS M
		ON M.MovieID = I.MovieID;

	-- CHANGES TO A MOVIE PROPAGATE UP TO THE PARENT SESSION
	UPDATE [Sessions]
	SET
		[Modified]	= @NOW
	FROM inserted AS I
		INNER JOIN [idvl].[Sessions] AS S
		ON S.SessionID = I.SessionID;
END;
