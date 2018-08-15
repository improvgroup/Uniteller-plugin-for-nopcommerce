using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.Uniteller.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        /// <summary>
        /// The Uniteller Point ID
        /// </summary>
        [NopResourceDisplayName("Plugins.Payments.Uniteller.Fields.ShopIdp")]
        public string ShopIdp { get; set; }
        public bool ShopIdpOverrideForStore { get; set; }

        /// <summary>
        /// Login
        /// </summary>
        [NopResourceDisplayName("Plugins.Payments.Uniteller.Fields.Login")]
        public string Login { get; set; }
        public bool LoginOverrideForStore { get; set; }

        /// <summary>
        /// Password
        /// </summary>
        [NopResourceDisplayName("Plugins.Payments.Uniteller.Fields.Password")]
        public string Password { get; set; }
        public bool PasswordOverrideForStore { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value
        /// </summary>
        [NopResourceDisplayName("Plugins.Payments.Uniteller.Fields.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }
        public bool AdditionalFeePercentageOverrideForStore { get; set; }

        /// <summary>
        /// Additional fee
        /// </summary>
        [NopResourceDisplayName("Plugins.Payments.Uniteller.Fields.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
        public bool AdditionalFeeOverrideForStore { get; set; }
    }
}