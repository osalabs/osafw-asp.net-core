### Release a change

1. Agree on the release, maintenance window, approver and rollback point. Record the current working revision and secure matching database and attachment backups. Review the release's provider-specific additive updates and configuration changes with the DBA. Fresh-install SQL belongs only on an empty destination.
2. On the deployment host, check the production profile from the application checkout. Confirm its environment, remote branch and target directory.

```powershell
powershell -NoProfile -File scripts\deploy_production.ps1 -Check
```

3. Resolve any tool, branch, path or permission failures. With approval, run the production profile.

```powershell
powershell -NoProfile -File scripts\deploy_production.ps1 -Pause
```

4. The profile builds in a temporary worktree, takes the site offline, resets the live checkout to the approved remote commit and copies the published output. Keep uncommitted edits outside the live checkout. Read the deployment status and log in `osafw-app\App_Data\logs`. Confirm the successful target commit. Sign in, open a representative record, verify an attachment and export a report. Check the application log and confirm scheduled jobs are running once on the intended host.
5. If the deploy or smoke check fails, stop further changes. Keep a partially copied site offline. Read the recorded failure. If the schema is compatible, release a reviewed revert on the profile branch and redeploy. If recovery requires restoring the database and files, agree on the recovery point and the impact on newer records with the owner and DBA. Repeat the smoke checks before reopening traffic.

### Operate and rehearse recovery

- **Monitor the service.** Check site availability, certificate expiry, application errors, free space for files and logs, and the last successful scheduled job. Assign each alert to a person who can investigate or escalate it.
- **Protect the complete service.** The database owner monitors backup jobs and verifies the latest restorable point. <~native_backup if="is_native_sql" inline>If the deployment uses native SQL Server backups, retain the full, differential and log backups required by its recovery plan.</~native_backup><~rds_backup if="is_rds" inline>Check the RDS backup retention and latest restorable time.</~rds_backup><~azure_backup if="is_azure_sql" inline>Check Azure SQL backup retention and the available point-in-time restore window.</~azure_backup> The storage owner protects uploads or object versions. The service owner safeguards environment configuration and arranges protected-settings recovery for a server move.
- **Test a restore.** Restore a selected backup and matching files into an isolated destination. Follow the recovery checks in this guide. Open a known record and download its attachment. If the record opens but its file is missing, check the matching storage backup and access permissions before approving cutover. Record the recovery point, elapsed time, checks and approver. Compare these with the client's agreed data-loss and outage limits.
