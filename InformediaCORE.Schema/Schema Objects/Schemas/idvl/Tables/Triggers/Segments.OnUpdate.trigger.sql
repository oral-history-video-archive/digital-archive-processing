-- InformediaCORE Database Schema

CREATE TRIGGER [idvl].[Segments_OnUpdate]
ON [idvl].[Segments]
AFTER UPDATE 
AS 
BEGIN
	SET NOCOUNT ON;

	DECLARE	@NOW datetime;
	SELECT	@NOW = GetDate();

	UPDATE [Segments]
	SET
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
