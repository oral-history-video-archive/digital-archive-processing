-- InformediaCORE Database Schema

ALTER TABLE [idvl].[Annotations]
	ADD CONSTRAINT [FK_Annotations_TypeID] 
	FOREIGN KEY ([TypeID])
	REFERENCES [idvl].[AnnotationTypes] ([TypeID])

