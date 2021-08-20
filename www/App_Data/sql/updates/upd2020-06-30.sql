EXEC sys.sp_rename 
    @objname = N'dbo.user_views.screen', 
    @newname = 'icode', 
    @objtype = 'COLUMN'
GO
