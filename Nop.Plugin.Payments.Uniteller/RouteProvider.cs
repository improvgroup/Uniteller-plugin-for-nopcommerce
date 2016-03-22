using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.Uniteller
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            //confirm pay
            routes.MapRoute("Plugin.Payments.Uniteller.ConfirmPay",
                 "Plugins/Uniteller/ConfirmPay",
                 new { controller = "PaymentUniteller", action = "ConfirmPay" },
                 new[] { "Nop.Plugin.Payments.Uniteller.Controllers" }
            );
            //cancel
            routes.MapRoute("Plugin.Payments.Uniteller.CancelOrder",
                 "Plugins/Uniteller/CancelOrder",
                 new { controller = "PaymentUniteller", action = "CancelOrder" },
                 new[] { "Nop.Plugin.Payments.Uniteller.Controllers" }
            );
            //success
            routes.MapRoute("Plugin.Payments.Uniteller.Success",
                 "Plugins/Uniteller/Success",
                 new { controller = "PaymentUniteller", action = "Success" },
                 new[] { "Nop.Plugin.Payments.Uniteller.Controllers" }
            );
        }
        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
