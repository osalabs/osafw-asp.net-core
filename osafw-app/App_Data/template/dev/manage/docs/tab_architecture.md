### High-Level Structure
The `<~GLOBAL[SITE_NAME]>` application is built on the OSAFW (Open Source Application Framework) for ASP.NET Core. It follows a modular MVC-like architecture consisting of the following key components:

- **Models:** Manage the business logic and data interactions.
- **Controllers:** Handle incoming requests and orchestrate responses.
- **Views:** Render the UI using the ParsePage template engine.
- **Database:** SQL Server (or another DB as applicable) for data storage.

### Request Flow
1. **Incoming Request:** The request is processed by the `FW.run()` method.
2. **Routing:** The `fw.dispatch()` function maps the request to the appropriate controller and action.
3. **Controller Execution:** The designated action is executed, possibly interacting with models and the database.
4. **View Rendering:** The view is rendered using the ParsePage engine and returned as the response.
5. **Response Finalization:** `fw.Finalize()` handles any final tasks before the response is sent to the client.

