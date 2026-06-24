### Prerequisites
- **.NET SDK:** .NET 10 SDK
- **Database Server:** <~db_type>
- **Operating System:** [Windows/Linux/MacOS]

### AWS EC2 Windows Web Server Setup
1. **Launch EC2 Instance:**
   - Start a Windows Server 2022 Datacenter instance (t3.micro or above, t2.micro for free tier).

2. **Setup Web Server:**
   - Add the Web Server role -> IIS, [SMTP (optional)].
   - Install .NET SDK (10.0 x64) from [this link](https://aka.ms/dotnet-download).
   - Install the .NET Core Hosting Bundle from [this link](https://dotnet.microsoft.com/permalink/dotnetcore-current-windows-runtime-bundle-installer).

3. **Install Git and Setup SSH:**
   - Install Git from [this link](https://git-scm.com/download/win).
   - Setup SSH keys for Git access:
     - Generate SSH key: `ssh-keygen`.
     - Add the public key to the Bitbucket repository under Repository Settings -> Access Keys.
     - Test connection: `ssh -T git@bitbucket.org`.

4. **Clone and Publish the Application:**
   - Create a directory for your site: `mkdir C:\inetpub\site.domain.name`.
   - Clone your repository: 
     ```bash
     git clone git@bitbucket.org:REPOSITORY/PROJECTNAME.git ./
     ```
   - Publish the application:
     ```bash
     dotnet publish --configuration Release
     ```

5. **Configure IIS Website:**
   - Create a new website named `site.domain.name`.
   - Set the physical path to `C:\inetpub\site.domain.name\bin\Release\net10.0\publish`.
   - Ensure the App Pool is set to "No Managed Code" if applicable.

6. **Set Directory Permissions:**
   - Make logs/uploads directory writable for the `IIS APPPOOL\site.domain.name` user:
     - `C:\inetpub\site.domain.name\App_Data\logs`
     - `C:\inetpub\site.domain.name\upload` or `..\App_Data\upload` (for non-public uploads).

7. **Create Scheduled Deploy Script:**
   - See `docs\deploy.md` for the full production/staging/develop deployment runbook.
   - Keep deploy scripts in the repo under `scripts\`; do not copy them to `C:\inetpub`. Updates to deploy scripts are applied on the next run after the server repo is reset to the newer commit.
   - Edit committed profile scripts once per project/environment:
     - `scripts\deploy_production.ps1` for manual production deployments.
     - `scripts\deploy_scheduled_develop.ps1` for a scheduled develop deployment.
     - `scripts\deploy_scheduled_staging.ps1` for a scheduled staging deployment when needed.
   - Profile scripts usually only set `$EnvironmentName`, `$GitBranch`, and `$DeployName`; `scripts\deploy_core.ps1` auto-detects repo root, project file, publish target, logs, state, status, and lock paths.
   - Validate the scheduled profile before registering the task:
     ```powershell
     powershell -NoProfile -ExecutionPolicy Bypass -File scripts\deploy_scheduled_develop.ps1 -Check
     ```
   - Register or update the scheduled task from an elevated PowerShell prompt. By default this registers `scripts\deploy_scheduled_develop.ps1` every 10 minutes:
     ```powershell
     powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup_deploy_scheduled_task.ps1 -RunAsUser "DOMAIN\deploy-user"
     ```
   - Run production manually when needed:
     ```powershell
     powershell -NoProfile -ExecutionPolicy Bypass -File scripts\deploy_production.ps1 -Pause
     ```
   - Use a dedicated deploy account when possible. It needs Git credentials and Modify rights to the repo, publish target, temp folder, and `osafw-app\App_Data\logs`; local Administrator is not required when those rights are granted. Use `-UseSystem` only when Git credentials are configured for LocalSystem.
   - The script builds in a temporary git worktree, deploys only after a successful publish, uses `app_offline.htm` during the final copy, and records logs/status/last-successful commit under `osafw-app\App_Data\logs`.
   - The script does not run `git clean`; untracked runtime files under `App_Data\db`, `App_Data\logs`, and `upload` are preserved.

8. **Install Let's Encrypt for SSL:**
   - Install the Let's Encrypt client from [this link](https://www.win-acme.com/).
   - Point the domain name to the server and configure HTTPS.

9. **Database Setup:**
   - Launch RDS unless SQL Server is installed on the same instance.
     - Use SQL Server Express Edition (db.t3.small) for simple installations.
     - Use SQL Server Web Edition (db.t3.medium) if storage encryption is required.
   - Create a database user:
     - For trusted connections, add `IIS APPPOOL\site.domain.name` to SQL Server with `db_owner` permissions.
     - For non-trusted connections:
       ```sql
       USE master;
       CREATE LOGIN yyyyyy WITH PASSWORD = 'XXXX', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
       USE zzzzzz;
       CREATE USER yyyyyy FOR LOGIN yyyyyy;
       EXEC sp_addrolemember N'db_owner', N'yyyyyy';
       ```

10. **Update appsettings.json:**
    - Configure the connection string for your database and SMTP settings.

### Optional Components

- Configure `FwCronService` for scheduled tasks if background processing is required.
- To store uploads in Amazon S3, set the S3 credentials in `appsettings.json`.
- Open firewall ports only as needed for HTTP/HTTPS and database access.


