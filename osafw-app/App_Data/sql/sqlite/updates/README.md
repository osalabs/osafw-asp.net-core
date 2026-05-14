# SQLite Updates

Put SQLite-specific incremental update scripts here.

Fresh SQLite installs use `App_Data/sql/sqlite/*.sql`. Existing SQLite databases load only scripts from this folder through `FwUpdates`; SQL Server updates are not replayed against SQLite.
