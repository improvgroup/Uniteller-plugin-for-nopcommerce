using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Uniteller
{
    public class UnitellerPaymentSettings : ISettings
    {
        /// <summary>
        /// The Uniteller Point ID
        /// </summary>
        public string ShopIdp { get; set; }

        /// <summary>
        /// Login
        /// </summary>
        public string Login { get; set; }

        /// <summary>
        /// Password
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }

        /// <summary>
        /// Additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }
    }
}
