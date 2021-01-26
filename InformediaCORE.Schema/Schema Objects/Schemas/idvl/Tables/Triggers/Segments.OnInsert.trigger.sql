-- InformediaCORE Database Schema
--
-- Pattern gleaned from http://www.sqlteam.com/forums/topic.asp?TOPIC_ID=57227
-- Also see http://msdn.microsoft.com/en-us/library/aa258254(v=sql.80).aspx

CREATE TRIGGER [idvl].[Segments_OnInsert]
ON [idvl].[Segments]
AFTER INSERT 
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE	@NOW datetime;
	SELECT	@NOW = GetDate();

	UPDATE [Segments]
	SET
		[Created]	= @NOW,
		[Modified]	= @NOW
	FROM inserted AS I
		INNER JOIN [idvl].[Segments] AS S
		ON S.SegmentID = I.SegmentID;

	-- CHANGES TO A SEGMENT PROPAGATE UP TO THE PARENT MOVIE
	UPDATE [Movies]
	SET
		[Modified] = @NOW
	FROM inserted AS I
		INNER JOIN [idvl].[Movies] AS M
		ON M.MovieID = I.MovieID;
END;
