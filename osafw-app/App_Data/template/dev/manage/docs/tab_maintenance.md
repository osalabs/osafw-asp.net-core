### Regular Tasks
- **Database Backups:** Schedule regular backups of the database.
- **Log Monitoring:** Review application logs located in `[Log Directory]` for errors or issues.
- **Dependency Updates:** Periodically update NuGet packages and dependencies.
- **Security Audits:** Conduct regular security reviews and apply patches as necessary.

### Updating the Application
1. **Pull Latest Changes:**
 ```bash
  git pull origin main
  ```
2. Rebuild and Deploy:

```bash
dotnet build
dotnet publish -o [Deployment Directory]
```

3. **Database Migrations:**
   Run any new migration scripts if there are schema changes.

4. **Clear Cache:**
   Use the *Clear Application Cache* link under `/Dev/Manage` after deploying.

5. **Review Logs:**
   Check `/App_Data/logs/main.log` for errors after each update.

