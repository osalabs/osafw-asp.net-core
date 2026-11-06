# Maintaining the User and Administrator Guide

Site administrators can open `/Dev/Manage/(Docs)` to read or export the User and Administrator Guide for the running application. Use the same guide at project handover and for everyday support. Both outputs read the same chapters under `osafw-app/App_Data/template/dev/manage/docs`. Maintain these files in the copied application alongside the features they describe.

## When a project changes

Update the affected chapter in the same change that ships the feature or operating procedure. Trace one real task from the reader's starting point through the action, the expected result and the point where support should take over. For example, after a restore, opening a record checks the database. Downloading a known attachment from that record also checks that its matching file was recovered.

| Change | Update and verify |
| --- | --- |
| Purpose, users, business screens or workflows | Edit `tab_overview.md` and `tab_using.md`. Use the actual menu labels and the steps and results a user will see. Remove retired tasks. |
| Sign-in, access, infrastructure, storage or integrations | Edit `tab_architecture.md`. Record the actual service and escalation owners, how users obtain access, deployed attachment storage and approved links. Check which roles can complete the documented tasks. |
| Install, move, deploy, back up or recover | Edit `tab_installation.md` and `tab_maintenance.md`. Use the project's actual profile, paths, database and recovery method. Include the backup owner, schedule, retention, recovery objectives and last verified rehearsal. |
| Troubleshooting, support or development | Edit `tab_troubleshooting.md` and `tab_resources.md`. Check each symptom against the current screen or log, and each link from a client account. |

**Keep the export specific to the installation.** Remove sample tasks, services and recovery paths that the project does not use. For an intranet-only IIS installation with self-managed SQL Server, keep the local restore procedure and omit cloud instructions. The Docs action checks the server name in the active SQL Server connection string. Standard Amazon RDS and Azure SQL endpoint names select the matching backup and restore paragraphs. A private DNS alias or IP address can hide the service. For these deployments, put the verified procedure directly into the copied application's Markdown. Replace the sample runbook when the project uses another provider or deploy process. Record where operators request vault access. Keep passwords, decrypted exports and private infrastructure identifiers out of the public framework.

**Use the generated facts.** The Docs action reads the configured product, available release, date, provider, MFA, access levels, active role grants, sender and support addresses. Review these in the running app. Correct stale addresses in Site Settings. Fill in the project's deployment arrangement and owners in Markdown. The S3 build flag only describes a capability. It cannot establish where an installation keeps its attachments.

## Check the guide before shipping

1. Open the affected tab as a Site Administrator. Confirm that links, permissions, commands and success criteria match the release. Download the PDF from the same action. Inspect the cover, contents links, section starts and footer. For publisher branding edit the two page-margin content strings in the Docs `head.css`.
2. Walk through the reader's choices. A manager should find the purpose, users and support route. An operator restoring records should reach the backup procedure without running `database-init`. An approved clean start should lead to an empty destination and preserve the old installation. Remove instructions for environments outside the project's scope.
3. When a deployment or recovery procedure changes, have client IT rehearse the affected path on a disposable target. Check records, an attachment and protected settings after restore. Record the date, owner and actual result in the copied app. At handover, export the approved release and agree with the client who maintains the guide after delivery.
