using System;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Uniteller.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.Uniteller.Controllers
{
    public class PaymentUnitellerController : BasePaymentController
    {
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;

        public PaymentUnitellerController(ILocalizationService localizationService,
            ILogger logger,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPaymentService paymentService,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext,
            IWebHelper webHelper)
        {
            this._localizationService = localizationService;
            this._logger = logger;
            this._orderProcessingService = orderProcessingService;
            this._orderService = orderService;
            this._paymentService = paymentService;
            this._permissionService = permissionService;
            this._settingService = settingService;
            this._storeContext = storeContext;
            this._webHelper = webHelper;
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
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

            return View("~/Plugins/Payments.Uniteller/Views/Configure.cshtml", model);
        }
        
        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
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
            _settingService.SaveSettingOverridablePerStore(unitellerPaymentSettings, x => x.ShopIdp, model.ShopIdpOverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(unitellerPaymentSettings, x => x.Login, model.LoginOverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(unitellerPaymentSettings, x => x.Password, model.PasswordOverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(unitellerPaymentSettings, x => x.AdditionalFee, model.AdditionalFeeOverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(unitellerPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentageOverrideForStore, storeScope, false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }
        
        private ContentResult GetResponse(string textToResponse, bool success = false)
        {
            var msg = success ? "SUCCESS" : "FAIL";
            if (!success)
                _logger.Error($"Uniteller. {textToResponse}");
           
            return Content($"{msg}\r\nnopCommerce. {textToResponse}", "text/plain", Encoding.UTF8);
        }

        private string GetValue(string key, IFormCollection form)
        {
            return (form.Keys.Contains(key) ? form[key].ToString() : _webHelper.QueryString<string>(key)) ?? string.Empty;
        }

        private IActionResult UpdateOrderStatus(Order order, string status)
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

        public IActionResult ConfirmPay(IpnModel model)
        {
            var form = model.Form;
            var processor =
               _paymentService.LoadPaymentMethodBySystemName("Payments.Uniteller") as UnitellerPaymentProcessor;
            if (processor == null ||
                !_paymentService.IsPaymentMethodActive(processor) || !processor.PluginDescriptor.Installed)
                throw new NopException("Uniteller module cannot be loaded");

            const string orderIdKey = "Order_ID";
            const string signatureKey = "Signature";
            const string statuskey = "Status";

            var orderId = GetValue(orderIdKey, form);
            var signature = GetValue(signatureKey, form);
            var status = GetValue(statuskey, form);

            Order order = null;

            if (Guid.TryParse(orderId, out Guid orderGuid))
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

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var setting = _settingService.LoadSetting<UnitellerPaymentSettings>(storeScope);

            var checkDataString = UnitellerPaymentProcessor.GetMD5(orderId + status + setting.Password).ToUpper();

            return checkDataString != signature ? GetResponse("Invalid order data") : UpdateOrderStatus(order, status);
        }

        public IActionResult Success()
        {
            var orderId = _webHelper.QueryString<string>("Order_ID");
            Order order = null;

            if (Guid.TryParse(orderId, out Guid orderGuid))
            {
                order = _orderService.GetOrderByGuid(orderGuid);
            }

            if (order == null)
                return RedirectToAction("Index", "Home", new { area = string.Empty });

            //update payment status if need
            if (order.PaymentStatus != PaymentStatus.Paid)
            {
                var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Uniteller") as UnitellerPaymentProcessor;
                if (processor == null || !_paymentService.IsPaymentMethodActive(processor) || !processor.PluginDescriptor.Installed)
                    throw new NopException("Uniteller module cannot be loaded");

                var statuses = processor.GetPaymentStatus(orderId);

                foreach (var status in statuses)
                {
                    UpdateOrderStatus(order, status);
                }
            }

            return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
        }

        public IActionResult CancelOrder()
        {
            var orderId = _webHelper.QueryString<string>("Order_ID");
            Order order = null;

            if (Guid.TryParse(orderId, out Guid orderGuid))
            {
                order = _orderService.GetOrderByGuid(orderGuid);
            }

            if (order == null)
                return RedirectToAction("Index", "Home", new {area = string.Empty});

            return RedirectToRoute("OrderDetails", new { orderId = order.Id });
        }
    }
}