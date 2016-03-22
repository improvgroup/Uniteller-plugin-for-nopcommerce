using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Uniteller.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.Uniteller.Controllers
{
    public class PaymentUnitellerController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly ILogger _logger;
        private readonly PaymentSettings _paymentSettings;
        private readonly ILocalizationService _localizationService;
        private readonly IWebHelper _webHelper;


        public PaymentUnitellerController(IWorkContext workContext,
            IStoreService storeService, 
            ISettingService settingService, 
            IPaymentService paymentService, 
            IOrderService orderService, 
            IOrderProcessingService orderProcessingService, 
            ILogger logger,
            PaymentSettings paymentSettings, 
            ILocalizationService localizationService, IWebHelper webHelper)
        {
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._logger = logger;
            this._paymentSettings = paymentSettings;
            this._localizationService = localizationService;
            this._webHelper = webHelper;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var unitellerPaymentSettings = _settingService.LoadSetting<UnitellerPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                ShopIdp = unitellerPaymentSettings.ShopIdp,
                Login = unitellerPaymentSettings.Login,
                Password = unitellerPaymentSettings.Password,
                AdditionalFee = unitellerPaymentSettings.AdditionalFee,
                AdditionalFeePercentage = unitellerPaymentSettings.AdditionalFeePercentage,
                ActiveStoreScopeConfiguration = storeScope
            };

            if (storeScope > 0)
            {
                model.ShopIdpOverrideForStore = _settingService.SettingExists(unitellerPaymentSettings, x => x.ShopIdp, storeScope);
                model.LoginOverrideForStore = _settingService.SettingExists(unitellerPaymentSettings, x => x.Login, storeScope);
                model.PasswordOverrideForStore = _settingService.SettingExists(unitellerPaymentSettings, x => x.Password, storeScope);
                model.AdditionalFeeOverrideForStore = _settingService.SettingExists(unitellerPaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentageOverrideForStore = _settingService.SettingExists(unitellerPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            }

            return View("~/Plugins/Payments.Uniteller/Views/PaymentUniteller/Configure.cshtml", model);
        }

        private void UpdateSetting<TPropType>(int storeScope, bool overrideForStore, UnitellerPaymentSettings settings, Expression<Func<UnitellerPaymentSettings, TPropType>> keySelector)
        {
            if (overrideForStore || storeScope == 0)
                _settingService.SaveSetting(settings, keySelector, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(settings, keySelector, storeScope);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var unitellerPaymentSettings = _settingService.LoadSetting<UnitellerPaymentSettings>(storeScope);

            //save settings
            unitellerPaymentSettings.ShopIdp = model.ShopIdp;
            unitellerPaymentSettings.Login = model.Login;
            unitellerPaymentSettings.Password = model.Password;
            unitellerPaymentSettings.AdditionalFee = model.AdditionalFee;
            unitellerPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            UpdateSetting(storeScope, model.ShopIdpOverrideForStore, unitellerPaymentSettings, x => x.ShopIdp);
            UpdateSetting(storeScope, model.LoginOverrideForStore, unitellerPaymentSettings, x => x.Login);
            UpdateSetting(storeScope, model.PasswordOverrideForStore, unitellerPaymentSettings, x => x.Password);
            UpdateSetting(storeScope, model.AdditionalFeeOverrideForStore, unitellerPaymentSettings, x => x.AdditionalFee);
            UpdateSetting(storeScope, model.AdditionalFeePercentageOverrideForStore, unitellerPaymentSettings, x => x.AdditionalFeePercentage);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            return View("~/Plugins/Payments.Uniteller/Views/PaymentUniteller/PaymentInfo.cshtml");
        }
        
        private ContentResult GetResponse(string textToResponse, bool success = false)
        {
            var msg = success ? "SUCCESS" : "FAIL";
            if(!success)
                _logger.Error(String.Format("Uniteller. {0}", textToResponse));
           
            return Content(String.Format("{0}\r\nnopCommerce. {1}", msg, textToResponse), "text/plain", Encoding.UTF8);
        }

        private string GetValue(string key, FormCollection form)
        {
            return (form.AllKeys.Contains(key) ? form[key] : _webHelper.QueryString<string>(key)) ?? String.Empty;
        }

        private ActionResult UpdateOrderStatus(Order order, string status)
        {
            status = status.ToUpper();
            var textToResponse = "Your order has been paid";

            switch (status)
            {
                case "CANCELED":
                {
                    //mark order as canceled
                    if ((order.PaymentStatus == PaymentStatus.Paid || order.PaymentStatus == PaymentStatus.Authorized) &&
                        _orderProcessingService.CanCancelOrder(order))
                        _orderProcessingService.CancelOrder(order, true);

                    textToResponse = "Your order has been canceled";
                }
                    break;
                case "AUTHORIZED":
                {
                    //mark order as authorized
                    if (_orderProcessingService.CanMarkOrderAsAuthorized(order))
                        _orderProcessingService.MarkAsAuthorized(order);
                    textToResponse = "Your order has been authorized";
                }
                    break;
                case "PAID":
                {
                    //mark order as paid
                    if (_orderProcessingService.CanMarkOrderAsPaid(order) && status.ToUpper() == "PAID")
                        _orderProcessingService.MarkOrderAsPaid(order);
                }
                    break;
                default:
                {
                    return GetResponse("Unsupported status");
                }
            }

            return GetResponse(textToResponse, true);
        }

        public ActionResult ConfirmPay(FormCollection form)
        {
            var processor =
               _paymentService.LoadPaymentMethodBySystemName("Payments.Uniteller") as UnitellerPaymentProcessor;
            if (processor == null ||
                !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                throw new NopException("Uniteller module cannot be loaded");

            const string orderIdKey = "Order_ID";
            const string signatureKey = "Signature";
            const string statuskey = "Status";

            var orderId = GetValue(orderIdKey, form);
            var signature = GetValue(signatureKey, form);
            var status = GetValue(statuskey, form);

            Order order = null;

            Guid orderGuid;
            if (Guid.TryParse(orderId, out orderGuid))
            {
                order = _orderService.GetOrderByGuid(orderGuid);
            }

            if (order == null)
                return GetResponse("Order cannot be loaded");

            var sb = new StringBuilder();
            sb.AppendLine("Uniteller:");
            sb.AppendLine(orderIdKey + ": " + orderId);
            sb.AppendLine(signatureKey + ": " + signature);
            sb.AppendLine(statuskey + ": " + status);

            //order note
            order.OrderNotes.Add(new OrderNote
            {
                Note = sb.ToString(),
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });
            _orderService.UpdateOrder(order);

            //load settings
            var setting = _settingService.LoadSetting<UnitellerPaymentSettings>();

            var checkDataString = UnitellerPaymentProcessor.GetMD5(orderId + status + setting.Password).ToUpper();

            return checkDataString != signature ? GetResponse("Invalid order data") : UpdateOrderStatus(order, status);
        }

        public ActionResult Success(FormCollection form)
        {
            var orderId = _webHelper.QueryString<string>("Order_ID");
            Order order = null;

            Guid orderGuid;
            if (Guid.TryParse(orderId, out orderGuid))
            {
                order = _orderService.GetOrderByGuid(orderGuid);
            }

            if (order == null)
                return RedirectToAction("Index", "Home", new {area = ""});

            //update payment status if need
            if (order.PaymentStatus != PaymentStatus.Paid)
            {
                var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Uniteller") as UnitellerPaymentProcessor;
                if (processor == null || !processor.IsPaymentMethodActive(_paymentSettings) || !processor.PluginDescriptor.Installed)
                    throw new NopException("Uniteller module cannot be loaded");

                var statuses = processor.GetPaimentStatus(orderId);

                foreach (var status in statuses)
                {
                    UpdateOrderStatus(order, status);
                }
            }

            return  RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
        }

        public ActionResult CancelOrder(FormCollection form)
        {
            var orderId = _webHelper.QueryString<string>("Order_ID");
            Order order = null;

            Guid orderGuid;
            if (Guid.TryParse(orderId, out orderGuid))
            {
                order = _orderService.GetOrderByGuid(orderGuid);
            }

            return order==null ? RedirectToAction("Index", "Home", new { area = "" }) : RedirectToRoute("OrderDetails", new {orderId = order.Id});
        }

        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            return new List<string>();
        }

        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            return new ProcessPaymentRequest();
        }
    }
}