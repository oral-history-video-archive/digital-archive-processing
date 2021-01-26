-- InformediaCORE Database Schema

CREATE TRIGGER [idvl].[TaskStates_OnInsertOrUpdate]
ON [idvl].[TaskStates]
FOR INSERT, UPDATE 
AS 
BEGIN
    SET NOCOUNT ON;

	DECLARE	@NOW datetime;
	SELECT	@NOW = GetDate();

	UPDATE S
	SET
		[Modified]	= @NOW
	FROM inserted AS I
		INNER JOIN [idvl].[TaskStates] AS S
		ON S.SegmentID = I.SegmentID AND S.Name = I.Name;
END;
