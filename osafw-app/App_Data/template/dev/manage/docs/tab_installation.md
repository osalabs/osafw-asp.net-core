Decide what must happen to the business records before preparing a destination. <mark>Restore an existing system</mark> to keep those records. Go straight from destination preparation to **Recovery from backup**. <mark>Start clean</mark> only with the owner's approval for an empty replacement.

Use the system profile to confirm the provider. Run the release commands from the root of the approved application checkout.

### Prepare the destination

1. Obtain the approved release, deployment profile, intended site URL, TLS certificate and environment configuration. Arrange access to the database, the attachment store and protected configuration through the designated owners.
2. Prepare the Windows host. Install IIS, then the ASP.NET Core Hosting Bundle for the application's target framework. The supplied deployment profile also requires Git, PowerShell, Robocopy and the matching .NET SDK. Install Microsoft Edge for PDF export. The Hosting Bundle provides the IIS hosting module. [Microsoft IIS hosting guide](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0)
3. Retain the release templates, SQL files and static assets. In IIS Manager, assign a dedicated **No Managed Code** pool, bind the HTTPS hostname and certificate, and point the site at the profile's publish target (by default `osafw-app\bin\Release\net10.0\publish`). Keep the pool stopped while preparing the database.
4. Give the deployment account access to the checkout, build/temp directories, publish target and deploy logs. The app identity needs read access to the app and write access to logs, uploads and temp files. Have the DBA grant the required schema permissions. Verify the host, database and environment before proceeding.

### New installation or approved clean start

1. For a clean start, stop writes and scheduled jobs, secure the previous database and files, and verify a recoverable backup. Obtain the owner's approval to start without the old business records.
2. Have the DBA create an empty destination database and map the appropriate identities. Set the intended production connection, host configuration and stable Data Protection application name through the approved deployment configuration.
<~fresh if="has_fresh_schema" inline nolang>3. On the destination, run the initializer once. Select the production environment explicitly.

```powershell
$env:DOTNET_ENVIRONMENT = "Production"
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run --no-launch-profile --project osafw-app -- database-init
```

4. The initializer creates the fresh schema and prints a bootstrap administrator password. Transfer it securely to the authorized administrator and rotate it after sign-in. If initialization reports existing tables, stop and have the DBA verify the target. Fresh-schema scripts can drop tables.

</~fresh><~custom_fresh unless="has_fresh_schema" inline>3. Have the database owner install the approved schema for this provider. Stop if the release lacks a verified fresh-install procedure. Confirm that an authorized administrator can sign in to the isolated destination.

</~custom_fresh>Deploy the release and start the pool. Verify sign-in and an attachment before cutover. Keep the old installation until the owner accepts its replacement.

### Recovery from backup

1. Agree on the recovery point with the owner. Identify the matching application release, database backup, attachment backup, and protected configuration. For a planned server move, export Site Settings from the old host while its keys still work. The export contains decrypted credentials and belongs in secure storage.
2. Keep the destination pool and background jobs stopped. Restore into a separate database using the database owner's restore procedure. <~native_restore if="is_native_sql" inline>For native SQL Server backups, open **Restore Database** in SQL Server Management Studio, choose the backup device, and enter a new destination database name. Check the data and log file destinations. Restore the full backup, a matching differential if used, and the log backups in order. Keep the database in the restoring state until the last backup, then recover it at the chosen point. [SQL Server restore guide](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/back-up-and-restore-of-sql-server-databases)</~native_restore><~rds_restore if="is_rds" inline>Restore the selected [RDS snapshot into a new instance](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/USER_RestoreFromSnapshot.html). Verify its endpoint and network access from the isolated site.</~rds_restore><~azure_restore if="is_azure_sql" inline>In the Azure portal, use Azure SQL point-in-time restore for the source database. Select the agreed time and a new destination database. Verify the target database name and connection from the isolated site.</~azure_restore> Verify the recovered database is online and contains expected records.
3. Restore the matching attachment files or object versions. Point the isolated site at the restored database and storage. Reinstate the deployment configuration and the stable `DATA_PROTECTION_APPLICATION_NAME`. Skip the empty-database initializer when restoring an existing schema.
4. On the original host, verify that the existing protection keys can be read. On a different Windows host, the old machine's DPAPI-wrapped keys cannot decrypt protected settings. For a planned move, secure the backup and Settings export, stop the old pool, and have the DBA clear only the destination copy's `fwkeys` rows with `itype=10`. Start the isolated destination, sign in locally and import the secured Settings export under **Admin / Settings**. The destination creates its own key ring, and the import protects credentials with those keys. Users must sign in again. If the export or old keys are unavailable, coordinate credential replacement with the owners before enabling integrations.
5. Verify local sign-in, representative records, an existing attachment download, report export, and a controlled integration check. Keep outgoing notifications and duplicate scheduled jobs isolated during rehearsal. Switch traffic only after the owners confirm the restored data and files. If records, attachments or credentials cannot be recovered, keep the original service or backups available and escalate.
