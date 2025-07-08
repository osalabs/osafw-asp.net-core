exec msdb.dbo.rds_restore_database 
@restore_db_name='DBNAME', 
@s3_arn_to_restore_from='arn:aws:s3:::BUCKETNAME/dbYYYY-MM-DD.bak';

exec msdb.dbo.rds_backup_database 
@source_db_name='DBNAME', @s3_arn_to_backup_to='arn:aws:s3:::BUCKETNAME/dbYYYY-MM-DD.bak', 
@overwrite_S3_backup_file=1;