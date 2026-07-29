using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace osafw;

internal static class DevCli
{
    internal const int EXIT_SUCCESS = 0;
    internal const int EXIT_ERROR = 1;
    internal const int EXIT_USAGE = 2;
    internal const int EXIT_ENVIRONMENT = 3;

    private const string COMMAND = "scaffold";
    private static readonly Regex TableNameRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
    private static readonly Regex ModelNameRegex = new("^[A-Z][A-Za-z0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex ControllerUrlRegex = new("^/[A-Za-z][A-Za-z0-9_]*/[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
    private static readonly Regex ReportCodeRegex = new("^[A-Za-z][A-Za-z0-9_]*(?:-[A-Za-z0-9_]+)*$", RegexOptions.CultureInvariant);

    internal sealed class Options
    {
        public string Operation { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string ControllerUrl { get; set; } = string.Empty;
        public string ControllerTitle { get; set; } = string.Empty;
        public string ControllerType { get; set; } = string.Empty;
        public bool Force { get; set; }
        public bool Help { get; set; }
    }

    internal sealed record ControllerPlan(
        string Url,
        string Title,
        string Type,
        string ClassName,
        string SourcePath,
        string TemplatePath,
        bool IsLookup);

    internal static bool isCommand(string[] args)
    {
        return args.Length > 0 && string.Equals(args[0], COMMAND, StringComparison.OrdinalIgnoreCase);
    }

    internal static void useDefaultDevelopmentEnvironment()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
            return;

        var dotnetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        Environment.SetEnvironmentVariable(
            "ASPNETCORE_ENVIRONMENT",
            string.IsNullOrWhiteSpace(dotnetEnvironment) ? Environments.Development : dotnetEnvironment.Trim());
    }

    internal static int run(
        string[] args,
        IConfiguration configuration,
        TextWriter? output = null,
        TextWriter? error = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;

        if (!tryParse(args, out var options, out var parseError))
        {
            error.WriteLine("Error: " + parseError);
            writeUsage(error);
            return EXIT_USAGE;
        }

        if (options.Help)
        {
            writeUsage(output);
            return EXIT_SUCCESS;
        }

        if (options.ControllerType == "api")
        {
            error.WriteLine("Error: API controller scaffolding is not yet available; the option is reserved for future support.");
            return EXIT_ERROR;
        }

        FW? fw = null;
        try
        {
            var settings = FwConfig.settingsForEnvironment(configuration);
            if (!settings["IS_DEV"].toBool())
            {
                error.WriteLine("Error: scaffolding is available only when the resolved app configuration has IS_DEV=true.");
                return EXIT_ENVIRONMENT;
            }

            fw = FW.initOffline(configuration);
            switch (options.Operation)
            {
                case "crud":
                    scaffoldCrud(fw, options, output);
                    break;
                case "model":
                    scaffoldModel(fw, options, output);
                    break;
                case "controller":
                    scaffoldController(fw, options, output);
                    break;
                case "report":
                    scaffoldReport(fw, options, output);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported scaffold operation.");
            }

            return EXIT_SUCCESS;
        }
        catch (Exception ex)
        {
            error.WriteLine("Error: " + ex.Message);
            return EXIT_ERROR;
        }
        finally
        {
            if (fw != null)
            {
                fw.endRequest();
                fw.Dispose();
            }
        }
    }

    internal static bool tryParse(string[] args, out Options options, out string error)
    {
        options = new Options();
        error = string.Empty;

        if (!isCommand(args))
        {
            error = "Expected the scaffold command.";
            return false;
        }

        if (args.Length == 1 || args.Skip(1).Any(isHelpOption))
        {
            options.Help = true;
            return true;
        }

        options.Operation = args[1].Trim().ToLowerInvariant();
        if (options.Operation is not ("crud" or "model" or "controller" or "report"))
        {
            error = $"Unknown scaffold operation '{args[1]}'.";
            return false;
        }

        if (args.Length < 3 || args[2].StartsWith("--", StringComparison.Ordinal))
        {
            error = $"The {options.Operation} operation requires a name.";
            return false;
        }

        options.Subject = args[2].Trim();
        if (options.Subject.Length == 0)
        {
            error = $"The {options.Operation} operation requires a non-empty name.";
            return false;
        }

        var usedOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 3; i < args.Length; i++)
        {
            var option = args[i].Trim().ToLowerInvariant();
            if (!option.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unexpected argument '{args[i]}'.";
                return false;
            }
            if (!usedOptions.Add(option))
            {
                error = $"Option '{option}' was specified more than once.";
                return false;
            }

            if (option == "--force")
            {
                options.Force = true;
                continue;
            }

            if (option is not ("--model" or "--name" or "--url" or "--title" or "--type"))
            {
                error = $"Unknown option '{args[i]}'.";
                return false;
            }
            if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Option '{option}' requires a value.";
                return false;
            }

            var value = args[i].Trim();
            if (value.Length == 0)
            {
                error = $"Option '{option}' requires a non-empty value.";
                return false;
            }

            switch (option)
            {
                case "--model":
                case "--name":
                    options.ModelName = value;
                    break;
                case "--url":
                    options.ControllerUrl = value;
                    break;
                case "--title":
                    options.ControllerTitle = value;
                    break;
                case "--type":
                    options.ControllerType = value;
                    break;
            }
        }

        var allowedOptions = options.Operation switch
        {
            "crud" => new HashSet<string>(["--model", "--url", "--title", "--type", "--force"], StringComparer.OrdinalIgnoreCase),
            "model" => new HashSet<string>(["--name", "--force"], StringComparer.OrdinalIgnoreCase),
            "controller" => new HashSet<string>(["--url", "--title", "--type", "--force"], StringComparer.OrdinalIgnoreCase),
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };
        var unsupportedOption = usedOptions.FirstOrDefault(option => !allowedOptions.Contains(option));
        if (unsupportedOption != null)
        {
            error = $"Option '{unsupportedOption}' is not supported by scaffold {options.Operation}.";
            return false;
        }

        if (options.Operation is "crud" or "model")
        {
            if (!isValidTableName(options.Subject))
            {
                error = "Table names must contain only letters, numbers, and underscores, and cannot start with a number.";
                return false;
            }
        }
        else if (options.Operation == "controller" && !isValidModelName(options.Subject))
        {
            error = "Model names must be PascalCase C# identifiers containing only letters and numbers.";
            return false;
        }
        else if (options.Operation == "report" && !isValidReportCode(options.Subject))
        {
            error = "Report codes must start with a letter and contain only letters, numbers, underscores, or single hyphen-separated parts.";
            return false;
        }

        if (options.ModelName.Length > 0 && !isValidModelName(options.ModelName))
        {
            error = "Model names must be PascalCase C# identifiers containing only letters and numbers.";
            return false;
        }
        if (options.ControllerUrl.Length > 0 && !isValidControllerUrl(options.ControllerUrl))
        {
            error = "Controller URLs must have exactly two letter-led route segments, for example /Admin/Orders.";
            return false;
        }
        if (options.ControllerTitle.Contains('\r') || options.ControllerTitle.Contains('\n') || options.ControllerTitle.Length > 255)
        {
            error = "Controller titles must be one line and no longer than 255 characters.";
            return false;
        }
        if (!tryNormalizeControllerType(options.ControllerType, out var controllerType))
        {
            error = "Controller type must be dynamic, vue, lookup, or api.";
            return false;
        }
        options.ControllerType = controllerType;

        return true;
    }

    internal static void writeUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  dotnet run --project osafw-app -- scaffold crud <table> [--model <name>] [--url </Prefix/Controller>] [--title <title>] [--type dynamic|vue|lookup|api] [--force]");
        writer.WriteLine("  dotnet run --project osafw-app -- scaffold model <table> [--name <model>] [--force]");
        writer.WriteLine("  dotnet run --project osafw-app -- scaffold controller <model> [--url </Prefix/Controller>] [--title <title>] [--type dynamic|vue|lookup|api] [--force]");
        writer.WriteLine("  dotnet run --project osafw-app -- scaffold report <code>");
        writer.WriteLine();
        writer.WriteLine("Scaffolding uses the resolved Development configuration by default and refuses to run unless IS_DEV=true.");
        writer.WriteLine("Existing generated model/controller targets are preserved unless --force is explicit.");
        writer.WriteLine("The api controller type is reserved for future support and currently exits without generating output.");
    }

    private static void scaffoldCrud(FW fw, Options options, TextWriter output)
    {
        var entity = entityForTable(fw, options.Subject);
        var modelName = options.ModelName.Length > 0 ? options.ModelName : entity["model_name"].toStr();
        requireValidDerivedModelName(modelName);
        entity["model_name"] = modelName;

        var modelPath = modelSourcePath(fw, modelName);
        ensureModelTargetAvailable(modelName, modelPath, options.Force);

        var plan = controllerPlan(fw, options, modelName);
        ensureControllerTargetsAvailable(plan, options.Force);
        applyControllerPlan(entity, plan, options.Force);
        var entities = contextEntities(fw, entity);

        var codeGen = DevCodeGen.init(fw);
        codeGen.createModel(entity);
        output.WriteLine("Generated model: " + modelPath);

        var created = codeGen.createController(entity, entities);
        writeControllerResult(output, plan, created);
    }

    private static void scaffoldModel(FW fw, Options options, TextWriter output)
    {
        var entity = entityForTable(fw, options.Subject);
        var modelName = options.ModelName.Length > 0 ? options.ModelName : entity["model_name"].toStr();
        requireValidDerivedModelName(modelName);
        entity["model_name"] = modelName;

        var modelPath = modelSourcePath(fw, modelName);
        ensureModelTargetAvailable(modelName, modelPath, options.Force);

        DevCodeGen.init(fw).createModel(entity);
        output.WriteLine("Generated model: " + modelPath);
    }

    private static void scaffoldController(FW fw, Options options, TextWriter output)
    {
        if (!DevEntityBuilder.listModels().Contains(options.Subject, StringComparer.Ordinal))
            throw new UserException($"Model '{options.Subject}' is not compiled. Generate it first, then run this command without --no-build or rebuild before retrying.");

        var model = fw.model(options.Subject);
        if (string.IsNullOrWhiteSpace(model.table_name))
            throw new UserException($"Model '{options.Subject}' does not define a table name.");

        var entity = entityForTable(fw, model.table_name);
        entity["model_name"] = options.Subject;
        var plan = controllerPlan(fw, options, options.Subject);
        ensureControllerTargetsAvailable(plan, options.Force);
        applyControllerPlan(entity, plan, options.Force);

        var created = DevCodeGen.init(fw).createController(entity, contextEntities(fw, entity));
        writeControllerResult(output, plan, created);
    }

    private static void scaffoldReport(FW fw, Options options, TextWriter output)
    {
        var reportClass = FwReportsBase.repcodeToClass(options.Subject);
        var reportsPath = Path.Combine(fw.config("site_root").toStr(), "App_Code", "models", "Reports");
        var reportPath = Path.Combine(reportsPath, reportClass.Replace("Report", string.Empty) + ".cs");
        var templatePath = Path.Combine(fw.config("template").toStr(), "admin", "reports", options.Subject.ToLowerInvariant());

        if (File.Exists(reportPath))
            throw new UserException($"Report source already exists: {reportPath}");
        if (Directory.Exists(templatePath))
            throw new UserException($"Report template directory already exists: {templatePath}");

        DevCodeGen.init(fw).createReport(options.Subject);
        output.WriteLine("Generated report: " + reportPath);
        output.WriteLine("Generated templates: " + templatePath);
    }

    private static FwDict entityForTable(FW fw, string requestedTable)
    {
        var tables = fw.db.tables();
        tables.AddRange(fw.db.views());
        var tableName = tables.FirstOrDefault(table => string.Equals(table, requestedTable, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(tableName))
            throw new UserException($"Table or view '{requestedTable}' was not found in the configured development database.");

        return DevEntityBuilder.table2entity(fw.db, tableName)
            ?? throw new UserException($"Could not read schema metadata for '{tableName}'.");
    }

    private static FwList contextEntities(FW fw, FwDict targetEntity)
    {
        var configFile = fw.config("template") + DevCodeGen.DB_JSON_PATH;
        var configuredEntities = DevEntityBuilder.loadJson<FwList>(configFile);
        var result = new FwList { targetEntity };
        var targetTable = targetEntity["table"].toStr();
        foreach (FwDict entity in configuredEntities)
        {
            if (!string.Equals(entity["table"].toStr(), targetTable, StringComparison.OrdinalIgnoreCase))
                result.Add(entity);
        }
        return result;
    }

    private static ControllerPlan controllerPlan(FW fw, Options options, string modelName)
    {
        var url = options.ControllerUrl.Length > 0 ? options.ControllerUrl : "/Admin/" + modelName;
        if (!isValidControllerUrl(url))
            throw new UserException($"Derived controller URL '{url}' is invalid. Supply --url /Prefix/Controller.");

        var title = options.ControllerTitle.Length > 0 ? options.ControllerTitle : Utils.name2human(modelName);
        var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var className = string.Concat(parts);
        var sourcePath = Path.Combine(fw.config("site_root").toStr(), "App_Code", "controllers", className + ".cs");
        var templatePath = Path.Combine(
            fw.config("template").toStr(),
            parts[0].ToLowerInvariant(),
            parts[1].ToLowerInvariant());
        return new ControllerPlan(url, title, options.ControllerType, className, sourcePath, templatePath, options.ControllerType == "lookup");
    }

    private static void applyControllerPlan(FwDict entity, ControllerPlan plan, bool force)
    {
        entity["controller"] = new FwDict
        {
            ["url"] = plan.Url,
            ["title"] = plan.Title,
            ["type"] = plan.Type,
            ["rwtpl"] = force,
        };
    }

    internal static void ensureModelTargetAvailable(string modelName, string modelPath, bool force)
    {
        var targetExists = File.Exists(modelPath);
        if (targetExists && !force)
            throw new UserException($"Model source already exists: {modelPath}. Use --force to replace it.");

        if (!targetExists && hasCompiledModelNamespaceType(modelName))
            throw new UserException($"A compiled type named '{modelName}' already exists in namespace '{typeof(FwModel).Namespace}'.");
    }

    internal static void ensureControllerTargetsAvailable(ControllerPlan plan, bool force)
    {
        var compiledControllerExists = DevEntityBuilder.listControllers()
            .Any(name => string.Equals(name, plan.ClassName + "Controller", StringComparison.OrdinalIgnoreCase));
        if (compiledControllerExists && plan.IsLookup)
            throw new UserException($"Controller type '{plan.ClassName}Controller' already occupies route '{plan.Url}'; lookup registration would be shadowed.");
        if (compiledControllerExists && !File.Exists(plan.SourcePath))
            throw new UserException($"Controller type '{plan.ClassName}Controller' already exists in another source file.");

        if (plan.IsLookup)
            return;
        if (File.Exists(plan.SourcePath) && !force)
            throw new UserException($"Controller source already exists: {plan.SourcePath}. Use --force to replace it.");
        if (Directory.Exists(plan.TemplatePath) && !force)
            throw new UserException($"Controller template directory already exists: {plan.TemplatePath}. Use --force to replace it.");
    }

    private static void writeControllerResult(TextWriter output, ControllerPlan plan, bool created)
    {
        if (!created && plan.IsLookup)
        {
            output.WriteLine("Updated lookup registration: " + plan.Url);
            return;
        }

        output.WriteLine("Generated controller: " + plan.SourcePath);
        output.WriteLine("Generated templates: " + plan.TemplatePath);
        output.WriteLine("Updated menu item: " + plan.Url);
    }

    private static string modelSourcePath(FW fw, string modelName)
    {
        return Path.Combine(fw.config("site_root").toStr(), "App_Code", "models", modelName + ".cs");
    }

    private static void requireValidDerivedModelName(string modelName)
    {
        if (!isValidModelName(modelName))
            throw new UserException($"Derived model name '{modelName}' is invalid. Supply --model or --name with a PascalCase name.");
    }

    private static bool isValidTableName(string value)
    {
        return value.Length <= 128 && TableNameRegex.IsMatch(value);
    }

    private static bool isValidModelName(string value)
    {
        return value.Length <= 128 && ModelNameRegex.IsMatch(value);
    }

    private static bool isValidControllerUrl(string value)
    {
        return value.Length <= 255 && ControllerUrlRegex.IsMatch(value);
    }

    private static bool isValidReportCode(string value)
    {
        return value.Length <= 128 && ReportCodeRegex.IsMatch(value);
    }

    private static bool hasCompiledModelNamespaceType(string typeName)
    {
        var modelNamespace = typeof(FwModel).Namespace;
        return typeof(FwModel).Assembly.GetTypes()
            .Any(type =>
                !type.IsNested &&
                string.Equals(type.Namespace, modelNamespace, StringComparison.Ordinal) &&
                string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool tryNormalizeControllerType(string value, out string normalized)
    {
        normalized = value.Trim().ToLowerInvariant();
        if (normalized == "dynamic")
            normalized = string.Empty;
        return normalized is "" or "vue" or "lookup" or "api";
    }

    private static bool isHelpOption(string value)
    {
        return value is "-h" or "--help" or "/?";
    }
}
