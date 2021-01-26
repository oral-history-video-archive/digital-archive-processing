-- InformediaCORE Database Schema
--
-- Pattern gleaned from http://www.sqlteam.com/forums/topic.asp?TOPIC_ID=57227
-- Also see http://msdn.microsoft.com/en-us/library/aa258254(v=sql.80).aspx

CREATE TRIGGER [idvl].[QueuedUpdates_OnInsert]
ON [idvl].[QueuedUpdates]
AFTER INSERT 
AS
BEGIN
	SET NOCOUNT ON

	UPDATE [QueuedUpdates]
	SET
		[Created]	= GetDate()
	FROM inserted AS I
		INNER JOIN [idvl].[QueuedUpdates] AS Q
		ON Q.Accession = I.Accession
END;