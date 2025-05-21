// Virtual Vue Fw Controller class for standard module with list/form screens
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

/*
class FwVirtualController extends FwVueController {
    const int access_level = Users::ACL_SITE_ADMIN;

    public string $template_basedir = '/common/virtual';

    public function __construct(array $fwcontroller) {
        parent::__construct(); #this will not load config.json as base_url is not yet set

        $this->base_url     = $fwcontroller['url'];
        $controller_basedir = strtolower($this->base_url);
        // if /index subdir of such directory exists - use it, otherwise /common/virtual will be used
        // we check for /index subdir because we want to be able to have config.json for configs override without all templates
        if (is_dir($this->fw->config->SITE_TEMPLATES . $controller_basedir . '/index')) {
            $this->template_basedir = $controller_basedir;
        }

        //use cached config or create config on the fly
        $config = json_decode($fwcontroller['config'] ?? '', true);
        if (!$config) {
            $entity = [
                'model_name' => $fwcontroller['model'],
                'controller' => [
                    'url'                   => $fwcontroller['url'],
                    'title'                 => $fwcontroller['iname'],
                    'is_dynamic_index_edit' => Users::i()->isAccessLevel($fwcontroller['access_level_edit']),
                ]
            ];
            $config = FwDev::init($this->fw, $this->fw->db)->updateControllerConfig($entity, []);
            #logger("virtual config:", $config);
        }

        // now merge with hardcoded config.json in templates (if any, file has a higher priority)
        // first check controller basedir, then /common/virtual
        $is_conf_found = false;
        $conf_file     = $this->fw->config->SITE_TEMPLATES . $controller_basedir . '/config.json';
        if (file_exists($conf_file)) {
            $is_conf_found = true;
            $file_config   = json_decode(file_get_contents($conf_file), true);
            if (!$file_config) {
                logger("WARN", "Error decoding config from $conf_file");
            } else {
                logger("TRACE", "merging config from:", $file_config);
                $config = array_replace_recursive($config, $file_config);
            }
        }

        if (!$is_conf_found) {
            # no controller-specific config, then check /common/virtual/config.json
            $conf_file = $this->fw->config->SITE_TEMPLATES . '/common/virtual/config.json';
            if (file_exists($conf_file)) {
                $file_config = json_decode(file_get_contents($conf_file), true);
                if (!$file_config) {
                    logger("WARN", "Error decoding config from $conf_file");
                } else {
                    logger("TRACE", "merging config from:", $file_config);
                    $config = array_replace_recursive($config, $file_config);
                }
            }
        }

        $this->loadControllerConfig($config);
    }

}

 */

//convert from PHP to C# .NET CORE

using osafw;
using System.Collections;
using System.IO;

public class FwVirtualController : FwVueController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    public string template_basedir = "/common/virtual";

    public FwVirtualController(FW fw, Hashtable fwcontroller) : base()
    {
        init(fw);

        //constructor will not load config.json as base_url is not yet set

        this.base_url = (string)fwcontroller["url"];
        string controller_basedir = this.base_url.ToLower();
        string template_root = fw.config("template") + controller_basedir;

        // if /index subdir of such directory exists - use it, otherwise /common/virtual will be used
        // we check for /index subdir because we want to be able to have config.json for configs override without all templates
        if (Directory.Exists(template_root + "/index"))
        {
            this.template_basedir = controller_basedir;
        }

        //use cached config or create config on the fly
        var config = (Hashtable)Utils.jsonDecode(fwcontroller["config"]);
        if (config == null)
        {
            var entity = new Hashtable
            {
                { "model_name", fwcontroller["model"] },
                { "controller", new Hashtable
                    {
                        { "url", fwcontroller["url"] },
                        { "title", fwcontroller["iname"] },
                        { "is_dynamic_index_edit", fw.model<Users>().isAccessLevel((int)fwcontroller["access_level_edit"]) }
                    }
                }
            };
            config = [];
            new DevCodeGen(fw, fw.db).updateControllerConfig(entity, config);
            //logger("virtual config:", config);
        }

        // now merge with hardcoded config.json in templates (if any, file has a higher priority)
        // first check controller basedir, then /common/virtual
        bool is_conf_found = false;
        string conf_file = template_root + "/config.json";
        if (File.Exists(conf_file))
        {
            is_conf_found = true;
            Hashtable file_config = (Hashtable)Utils.jsonDecode(FW.getFileContent(conf_file));
            if (file_config == null)
            {
                logger(LogLevel.WARN, "Error decoding config from " + conf_file);
            }
            else
            {
                logger(LogLevel.TRACE, "merging config from:", file_config);
                Utils.mergeHashDeep(config, file_config);
            }
        }

        if (!is_conf_found)
        {
            // no controller-specific config, then check /common/virtual/config.json
            conf_file = fw.config("template") + "/common/virtual/config.json";
            if (File.Exists(conf_file))
            {
                Hashtable file_config = (Hashtable)Utils.jsonDecode(FW.getFileContent(conf_file));
                if (file_config == null)
                {
                    logger(LogLevel.WARN, "Error decoding config from " + conf_file);
                }
                else
                {
                    logger(LogLevel.TRACE, "merging config from:", file_config);
                    Utils.mergeHashDeep(config, file_config);
                }
            }
        }

        loadControllerConfig(config);
    }

}
