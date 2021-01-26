-- InformediaCORE Database Schema
--
-- Pattern gleaned from http://www.sqlteam.com/forums/topic.asp?TOPIC_ID=57227
-- Also see http://msdn.microsoft.com/en-us/library/aa258254(v=sql.80).aspx

CREATE TRIGGER [idvl].[Movies_OnInsert]
ON [idvl].[Movies]
AFTER INSERT 
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE	@NOW datetime;
	SELECT	@NOW = GetDate();

	UPDATE [Movies]
	SET
		[Created]	= @NOW,
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
