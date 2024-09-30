### Prerequisites
- **.NET SDK:** [.NET Core/ASP.NET Core SDK Version]
- **Database Server:** SQL Server [or other]
- **Operating System:** [Windows/Linux/MacOS]

### AWS EC2 Windows Web Server Setup
1. **Launch EC2 Instance:**
   - Start a Windows Server 2022 Datacenter instance (t3.micro or above, t2.micro for free tier).

2. **Setup Web Server:**
   - Add the Web Server role -> IIS, [SMTP (optional)].
   - Install .NET SDK (8.0 x64) from [this link](https://aka.ms/dotnet-download).
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
   - Set the physical path to `C:\inetpub\site.domain.name\bin\Release\net8.0\publish`.
   - Ensure the App Pool is set to "No Managed Code" if applicable.

6. **Set Directory Permissions:**
   - Make logs/uploads directory writable for the `IIS APPPOOL\site.domain.name` user:
     - `C:\inetpub\site.domain.name\App_Data\logs`
     - `C:\inetpub\site.domain.name\upload` or `..\App_Data\upload` (for non-public uploads).

7. **Create Update Script:**
   - Create a batch script `C:\inetpub\site.domain.name.bat` with the following content:
     ```bash
     cd c:\inetpub\site.domain.name
     git pull
     %windir%\system32\inetsrv\appcmd stop apppool "site.domain.name"
     dotnet publish --configuration Release
     %windir%\system32\inetsrv\appcmd start apppool "site.domain.name"
     pause
     ```

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
- **Install wkhtmltopdf** for PDF report generation from [this link](https://wkhtmltopdf.org/downloads.html).
- **Install ACE.OLEDB.12** to work with Access databases from [this link](https://download.microsoft.com/download/2/4/3/24375141-E08D-4803-AB0E-10F2E3A07AAA/AccessDatabaseEngine_X64.exe).
