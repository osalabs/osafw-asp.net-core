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
   - Copy `scripts\deploy_sample_v2.bat` to a server-local path such as `C:\inetpub\deploy.staging.bat`.
   - Edit the project-specific settings at the top of the copied script:
     - `PROJECT_ROOT=C:\inetpub\site.domain.name`
     - `PROJECT_FILE=osafw-app.csproj`
     - `TARGET_FOLDER=C:\inetpub\site.domain.name\bin\Release\net10.0\publish`
     - `GIT_BRANCH=staging` (or the branch for this server)
     - `DEPLOY_NAME=deploy-staging`
   - Add a Windows Scheduled Task every 5-10 minutes running the copied script. Use a Windows account that can access Git, the .NET SDK, IIS target folders, and the log/state folder.
   - The script builds in a temporary git worktree, deploys only after a successful publish, uses `app_offline.htm` during the final copy, and records the last successfully deployed commit.
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


