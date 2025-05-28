### Introduction
**Application Name:** <~GLOBAL[SITE_NAME]>
**Framework:** OSAFW ASP.NET Core
**Version:** <~GLOBAL[SITE_VERSION]>
**Last Updated:** <~GLOBAL[current_time] date="yyyy-MM-dd">

This documentation describes the structure and deployment of the application built with OSAFW ASP.NET Core. It includes installation, maintenance and troubleshooting guidelines. The same content can be exported as a PDF using the button above.

### Terminology
- **Project Name:** Project Name
- **Project Code:** ProjectCode
  *This short code is reused for repository names, the application folder, the database and the IIS site name.*
- **Short Description:**
  Replace this paragraph with a short functional overview of the system.

### Technology Stack
- **Backend:** .NET Core C#
- **Database:** SQL Server (or MySQL)

### Security Considerations
- **Role-Based Access Control (RBAC):** enabled if the application defines roles, resources and permissions.
- **Access Levels:** used to restrict features for different user types.
- **Multi-Factor Authentication (MFA):** optional.
- **Authentication Method:** Windows authentication, Active Directory or OAuth depending on deployment.

### Uploads
- **Storage Location:** local file system or Amazon S3.

### User Interface
- **Target Screen Resolutions:** Full HD and half-screen layouts.
- **Table Row Button Position:** right side by default.
- **Entity View Screens:** enabled for most entities.
- **Multi-language Support:** optional.
- **Main Dashboard Blocks/Types:** customize per project.
- **Pages Module:** available if content pages are used.
- **Feedback Functionality:** optional, emails sent to `<~GLOBAL[support_email]>`.
- **Display Standards:** dates in mm/dd/yyyy, 24‑hour time and the metric system.
- **Branding:** colors and logos can be adjusted in the theme files.

### Hosting Options
- **Hosting Environment:** intranet, AWS or other cloud provider.
  - **Intranet:** note if VPN access is required.
  - **AWS:** choose between SQL Server Express (no encryption) and the Web edition (supports encryption). Set the correct server time zone.
  - **Other:** specify additional details.

### Email Configuration
- **Support Email:** <~GLOBAL[support_email]>
- **No-reply Email:** <~GLOBAL[mail_from]>
- **SMTP Server Options:** use local SMTP, Outlook or AWS SES credentials as required.
