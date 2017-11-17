using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Uniteller.Components
{
    [ViewComponent(Name = "PaymentUniteller")]
    public class PaymentUnitellerViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.Uniteller/Views/PaymentInfo.cshtml");
        }
    }
}
