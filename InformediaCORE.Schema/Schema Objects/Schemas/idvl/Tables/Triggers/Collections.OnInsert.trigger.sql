-- InformediaCORE Database Schema
--
-- Pattern gleaned from http://www.sqlteam.com/forums/topic.asp?TOPIC_ID=57227
-- Also see http://msdn.microsoft.com/en-us/library/aa258254(v=sql.80).aspx

CREATE TRIGGER [idvl].[Collections_OnInsert]
ON [idvl].[Collections]
AFTER INSERT 
AS
BEGIN
	SET NOCOUNT ON

	DECLARE	@NOW datetime
	SELECT	@NOW = GetDate()

	UPDATE [Collections]
	SET
		[Created]	= @NOW,
		[Modified]	= @NOW
	FROM inserted AS I
		INNER JOIN [idvl].[Collections] AS C
		ON C.CollectionID = I.CollectionID
END;
