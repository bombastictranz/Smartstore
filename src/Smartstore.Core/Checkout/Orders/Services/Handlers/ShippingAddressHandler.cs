﻿using Microsoft.AspNetCore.Http;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Data;

namespace Smartstore.Core.Checkout.Orders.Handlers
{
    public class ShippingAddressHandler : CheckoutHandlerBase
    {
        private static readonly string[] _actionNames = [ "ShippingAddress", "SelectShippingAddress" ];

        private readonly SmartDbContext _db;
        private readonly ICheckoutStateAccessor _checkoutStateAccessor;
        private readonly ShoppingCartSettings _shoppingCartSettings;

        public ShippingAddressHandler(
            SmartDbContext db,
            ICheckoutStateAccessor checkoutStateAccessor,
            IHttpContextAccessor httpContextAccessor,
            ShoppingCartSettings shoppingCartSettings)
            : base(httpContextAccessor)
        {
            _db = db;
            _checkoutStateAccessor = checkoutStateAccessor;
            _shoppingCartSettings = shoppingCartSettings;
        }

        protected override string ActionName => "ShippingAddress";

        public override int Order => 20;

        public override bool IsHandlerFor(string action, string controller)
            => _actionNames.Contains(action) && controller.EqualsNoCase(ControllerName);

        public override async Task<CheckoutHandlerResult> ProcessAsync(ShoppingCart cart, object model = null)
        {
            var customer = cart.Customer;
            var ga = customer.GenericAttributes;

            if (model != null 
                && model is int addressId 
                && IsSameRoute(HttpMethods.Post, "SelectShippingAddress"))
            {
                var address = customer.Addresses.FirstOrDefault(x => x.Id == addressId);
                if (address == null)
                {
                    return new(false);
                }

                customer.ShippingAddress = address;

                if (_shoppingCartSettings.QuickCheckoutEnabled)
                {
                    ga.DefaultShippingAddressId = customer.ShippingAddress.Id;
                }

                await _db.SaveChangesAsync();

                return new(true);
            }

            if (!cart.IsShippingRequired())
            {
                if (customer.ShippingAddressId.GetValueOrDefault() != 0)
                {
                    customer.ShippingAddress = null;
                    await _db.SaveChangesAsync();
                }

                return new(true, null, true);
            }

            var state = _checkoutStateAccessor.CheckoutState;
            state.CustomProperties.TryGetValueAs("SkipShippingAddress", out bool skip);
            state.CustomProperties.Remove("SkipShippingAddress");

            if (!skip && _shoppingCartSettings.QuickCheckoutEnabled)
            {
                var defaultAddress = customer.Addresses.FirstOrDefault(x => x.Id == ga.DefaultShippingAddressId);
                if (defaultAddress != null)
                {
                    if (customer.ShippingAddressId != defaultAddress.Id)
                    {
                        customer.ShippingAddress = defaultAddress;
                        await _db.SaveChangesAsync();
                    }

                    return new(true);
                }
            }

            if (customer.ShippingAddressId == null)
            {
                return new(false, null, skip);
            }

            await _db.LoadReferenceAsync(customer, x => x.ShippingAddress);

            return new(customer.ShippingAddress != null, null, skip);
        }
    }
}
