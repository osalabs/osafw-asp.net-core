Users reach the application over HTTPS. IIS sends requests to the ASP.NET Core application, which reads and writes business records in its configured database. Files attached to records have a separate storage lifecycle. A restored database can contain valid attachment references while the corresponding files are still missing.

The application owner approves access and business changes. The IT service owner coordinates the web host, database, attachment storage, certificates and integrations. Report a dependency outage through the IT service owner, who can involve its operator.
