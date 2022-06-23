// Self Test controller - only available for Site Admins
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

namespace osafw;

public class DevSelfTestController : FwController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    protected FwSelfTest Test;

    public override void init(FW fw)
    {
        base.init(fw);

        // initialization
        base_url = "/Dev/SelfTest";
        Test = new FwSelfTest(fw);
    }

    public void IndexAction()
    {
        Test.echo_start();
        Test.all();
        // either inherit FwSelfTest and override all/some test
        // or add here tests specific for the site
        Test.echo_totals();
    }

    // just have this stub here, so we don't call IndexAction and stuck in a recursion 
    public FwSelfTest.Result SelfTest(FwSelfTest t)
    {
        return FwSelfTest.Result.OK;
    }
}