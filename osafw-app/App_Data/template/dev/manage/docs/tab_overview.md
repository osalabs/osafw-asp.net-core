### Introduction
**Application Name:** [Your Application Name]
**Framework:** OSAFW ASP.NET Core
**Version:** [Application Version]
**Last Updated:** [Last Updated Date]

This document provides a technical overview of the [Your Application Name], including its architecture, installation, and maintenance instructions. Additionally, it includes detailed steps for deploying the application on an AWS EC2 Windows Server instance.

### Terminology
- **Project Name:** [Your Project Name]
- **Project Code:** [Your Project Code]
  *Note: The Project Code is used across various components, including the repository name, app folder, database, and web server site name.*
- **Short Description:**
  [Provide a brief description of where, how, and by whom the system will be used. Include a link to the client website if applicable.]

### Technology Stack
- **Backend:** .NET Core C# (default) or PHP
- **Database:** SQL Server (default) or MySQL

### Security Considerations
- **Role-Based Access Control (RBAC):** [Yes/No]
  If yes, define roles, resources, and permissions.
- **Access Levels:** [Yes/No]
  If yes, define what can be accessed at each level.
- **Multi-Factor Authentication (MFA):** [Yes/No]
- **Authentication Method:** [Windows Auth, Active Directory, OAuth]

### Uploads Storage Options
- **Storage Location:** [Local, S3]

### User Interface (UI)
- **Target Screen Resolutions:** [FullHD, HD, tablet, phone]
  *Default: FullHD and Half-screen*
- **Table Row Button Position:** [Right (default) or Left]
- **Entity View Screens:** [Yes (default), No, or Depends on Entity]
- **Multi-language Support:** [Yes/No]
- **Main Dashboard Blocks/Types:** [Describe the required dashboard blocks/types]
- **Pages Module:** [Yes/No]
- **Feedback Functionality:** [Yes/No]
  *If yes, provide feedback email address.*
- **Display Standards:**
  - Date Format: [mm/dd or dd/mm]
  - Time Format: [24-hour or AM/PM]
  - Measurement Units: [miles/km]
- **Branding:** [Specify company/brand colors]

### Hosting Options
- **Hosting Environment:** [Intranet, AWS, Other]
  - **Intranet:**
    *Specify if VPN access is needed.*
  - **AWS:**
    *Specify if using SQL Server Express (no encryption) or Web license (encryption). Include time zone information for servers.*
  - **Other:** [Provide details]

### Email Configuration
- **Support Email:** [Email address]
- **No-reply Email:** [noreply@domain.com]
- **SMTP Server Options:**
  - [Their SMTP Server (host, port, SSL, username/password)]
  - [Their Outlook]
  - [AWS SES]