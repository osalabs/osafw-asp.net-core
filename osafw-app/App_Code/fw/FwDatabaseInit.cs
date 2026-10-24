using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace osafw;

/// <summary>
/// Explicit development-only empty-database bootstrap. It bypasses the normal web session and
/// durable-key pipeline so a fresh database can create those prerequisites.
/// </summary>
internal static class FwDatabaseInit
{
    internal const int EXIT_SUCCESS = 0;
    internal const int EXIT_ERROR = 1;
    internal const int EXIT_USAGE = 2;
    internal const int EXIT_ENVIRONMENT = 3;

    private const string COMMAND = "database-init";

    internal static bool isCommand(string[] args)
    {
        return args.Length > 0 && string.Equals(args[0], COMMAND, StringComparison.OrdinalIgnoreCase);
    }

    internal static int run(
        string[] args,
        IConfiguration configuration,
        TextWriter? output = null,
        TextWriter? error = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;

        if (!isCommand(args) || args.Length != 1)
        {
            error.WriteLine("Error: use database-init without additional arguments.");
            return EXIT_USAGE;
        }

        FW? fw = null;
        try
        {
            fw = new FW(null, configuration);
            var password = initialize(fw);

            // This is the one intentional secret output: the operator needs the generated
            // bootstrap password from this explicit local maintenance command.
            output.WriteLine(password);
            return EXIT_SUCCESS;
        }
        catch (EnvironmentException)
        {
            error.WriteLine("Error: database-init requires the resolved configuration to have IS_DEV=true.");
            return EXIT_ENVIRONMENT;
        }
        catch (Exception)
        {
            error.WriteLine("Error: database initialization failed. Verify that the configured database is empty and reachable.");
            return EXIT_ERROR;
        }
        finally
        {
            if (fw != null)
            {
                try { fw.endRequest(); }
                catch { }
                try { fw.Dispose(); }
                catch { }
            }
        }
    }

    /// <summary>
    /// Guarded initialization seam. Optional delegates are for deterministic tests and do not
    /// alter the production command's configured database or initializer.
    /// </summary>
    internal static string initialize(
        FW fw,
        Func<StrList>? listTables = null,
        Func<FwDict>? initializeDatabase = null)
    {
        ArgumentNullException.ThrowIfNull(fw);
        if (!fw.config("IS_DEV").toBool())
            throw new EnvironmentException();

        listTables ??= fw.db.tables;
        if (listTables().Count != 0)
            throw new UserException("Database initialization requires an empty database.");

        if (initializeDatabase == null)
        {
            var controller = new CliDevConfigureController();
            controller.init(fw);
            initializeDatabase = controller.runInitialization;
        }

        var result = initializeDatabase();
        var password = result["pwd"].toStr();
        if (password.Length == 0)
            throw new UserException("Database initialization did not create a bootstrap password.");

        return password;
    }

    private sealed class CliDevConfigureController : DevConfigureController
    {
        public FwDict runInitialization()
        {
            return initDatabase();
        }
    }

    internal sealed class EnvironmentException : Exception
    {
    }
}
