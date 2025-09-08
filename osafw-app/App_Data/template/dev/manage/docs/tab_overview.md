### Introduction
**Application Name:** <~GLOBAL[SITE_NAME]>
**Framework:** OSAFW ASP.NET Core
**Version:** <~GLOBAL[SITE_VERSION]>
**Last Updated:** <~GLOBAL[current_time] date="yyyy-MM-dd">

This documentation describes the structure and deployment of the application built with OSAFW ASP.NET Core. It includes installation, maintenance and troubleshooting guidelines. The same content can be exported as a PDF using the button above.

### Terminology
- **Project Name:** <~GLOBAL[SITE_NAME]>
- **Project Code:** <~GLOBAL[PROJECT_CODE]>
  *This short code is reused for repository names, the application folder, the database and the IIS site name.*
- **Short Description:**
  %%Provide a brief description of where, how, and by whom the system will be used. Include a link to the client website if applicable.%%

### Technology Stack
- **Backend:** .NET Core C#
- **Database:** <~db_type>

### Security Considerations
- **Role-Based Access Control (RBAC):** <~/common/sel/yn_bool.sel selvalue="is_rbac">
  If yes, %%list roles, resources, and permissions%%.
- **Access Levels:**
  <ul>
  <~access_levels repeat inline>
    <li><~iname></li>
  </~access_levels>
  </ul>

- **Multi-Factor Authentication (MFA) enforced:** <~/common/sel/yn_bool.sel selvalue="GLOBAL[is_mfa_enforced]"> 
- **Authentication Method:** %%Windows Auth, Active Directory, OAuth%%

### Uploads
- **Storage Location:** <~att_local unless="is_S3" inline>local file system</~att_local> <~att_S3 if="is_S3" inline>AWS S3</~att_S3>

### User Interface
- **Target Screen Resolutions:** Full HD and half-screen layouts.
- **Table Row Button Position:** right side by default.
- **Entity View Screens:** enabled for most entities.
- **Multi-language Support:** optional.
- **Main Dashboard Blocks/Types:** customize per project.
- **Pages Module:** available if content pages are used.
- **Feedback Functionality:** optional, emails sent to `<~GLOBAL[support_email]>`.
- **Display Standards:** dates in <~/common/sel/date_format.sel selvalue="GLOBAL[date_format]"> format, time in <~/common/sel/time_format.sel selvalue="GLOBAL[time_format]"> format and the metric system.
- **Branding:** colors and logos can be adjusted in the theme files.

### Hosting Options
- **Hosting Environment:** intranet, AWS or other cloud provider.
  - **Intranet:** note if VPN access is required.
  - **AWS:** choose between SQL Server Express (no encryption) and the Web edition (supports encryption). Set the correct server time zone (default UTC)
  - **Other:** specify additional details.

### Email Configuration
- **Support Email:** <~GLOBAL[support_email]>
- **No-reply Email:** <~GLOBAL[mail_from]>
- **SMTP Server Options:** use local SMTP, Outlook or AWS SES credentials as required.
