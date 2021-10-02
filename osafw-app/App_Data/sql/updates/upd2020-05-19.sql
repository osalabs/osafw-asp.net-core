EXEC sys.sp_rename 
    @objname = N'dbo.users.notes', 
    @newname = 'idesc', 
    @objtype = 'COLUMN'
GO
