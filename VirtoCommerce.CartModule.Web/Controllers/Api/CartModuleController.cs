﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.Description;
using VirtoCommerce.CartModule.Data.Services;
using VirtoCommerce.CartModule.Web.Model;
using VirtoCommerce.Domain.Cart.Model;
using VirtoCommerce.Domain.Cart.Services;
using VirtoCommerce.Domain.Shipping.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Web.Security;

namespace VirtoCommerce.CartModule.Web.Controllers.Api
{
    [RoutePrefix("api/carts")]
    [CheckPermission(Permission = PredefinedPermissions.Query)]
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class CartModuleController : ApiController
    {
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IShoppingCartSearchService _searchService;
        private readonly IShoppingCartBuilder _cartBuilder;

        public CartModuleController(IShoppingCartService shoppingCartService, IShoppingCartSearchService searchService, IShoppingCartBuilder cartBuilder)
        {
            _shoppingCartService = shoppingCartService;
            _searchService = searchService;
            _cartBuilder = cartBuilder;
        }

        [HttpGet]
        [Route("{storeId}/{customerId}/{cartName}/{currency}/{cultureName}/current")]
        [ResponseType(typeof(ShoppingCart))]
        public async Task<IHttpActionResult> GetCart(string storeId, string customerId, string cartName, string currency, string cultureName)
        {
            using (await AsyncLock.GetLockByKey(GetAsyncLockCartKey(string.Join(":", storeId, customerId, cartName, currency))).LockAsync())
            {
                _cartBuilder.GetOrCreateCart(storeId, customerId, cartName, currency, cultureName);
            }
            return Ok(_cartBuilder.Cart);
        }

        [HttpGet]
        [Route("{cartId}/itemscount")]
        [ResponseType(typeof(int))]
        public IHttpActionResult GetCartItemsCount(string cartId)
        {
            var cart = _shoppingCartService.GetByIds(new[] { cartId }).FirstOrDefault();
            return Ok(cart?.Items?.Count ?? 0);
        }

        [HttpPost]
        [Route("{cartId}/items")]
        [ResponseType(typeof(void))]
        public async Task<IHttpActionResult> AddItemToCart(string cartId, [FromBody] LineItem lineItem)
        {
            using (await AsyncLock.GetLockByKey(GetAsyncLockCartKey(cartId)).LockAsync())
            {
                var cart = _shoppingCartService.GetByIds(new[] { cartId }).FirstOrDefault();
                _cartBuilder.TakeCart(cart).AddItem(lineItem).Save();
            }
            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPut]
        [Route("{cartId}/items")]
        [ResponseType(typeof(void))]
        public async Task<IHttpActionResult> ChangeCartItem(string cartId, string lineItemId, int quantity)
        {
            using (await AsyncLock.GetLockByKey(GetAsyncLockCartKey(cartId)).LockAsync())
            {
                var cart = _shoppingCartService.GetByIds(new[] { cartId }).FirstOrDefault();
                _cartBuilder.TakeCart(cart);
                var lineItem = _cartBuilder.Cart.Items.FirstOrDefault(i => i.Id == lineItemId);
                if (lineItem != null)
                {
                    _cartBuilder.ChangeItemQuantity(lineItemId, quantity).Save();
                }
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpDelete]
        [Route("{cartId}/items/{lineItemId}")]
        [ResponseType(typeof(int))]
        public async Task<IHttpActionResult> RemoveCartItem(string cartId, string lineItemId)
        {
            using (await AsyncLock.GetLockByKey(GetAsyncLockCartKey(cartId)).LockAsync())
            {
                var cart = _shoppingCartService.GetByIds(new[] { cartId }).FirstOrDefault();
                _cartBuilder.TakeCart(cart).RemoveItem(lineItemId).Save();
            }
            return Ok(_cartBuilder.Cart.Items.Count);
        }

        [HttpDelete]
        [Route("{cartId}/items")]
        [ResponseType(typeof(void))]
        public async Task<IHttpActionResult> ClearCart(string cartId)
        {
            using (await AsyncLock.GetLockByKey(GetAsyncLockCartKey(cartId)).LockAsync())
            {
                var cart = _shoppingCartService.GetByIds(new[] { cartId }).FirstOrDefault();
                _cartBuilder.TakeCart(cart).Clear().Save();
            }
            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPatch]
        [Route("{cartId}")]
        [ResponseType(typeof(void))]
        public async Task<IHttpActionResult> MergeWithCart(string cartId, [FromBody]ShoppingCart otherCart)
        {
            using (await AsyncLock.GetLockByKey(GetAsyncLockCartKey(cartId)).LockAsync())
            {
                var cart = _shoppingCartService.GetByIds(new[] { cartId }).FirstOrDefault();
                _cartBuilder.TakeCart(cart).MergeWithCart(otherCart).Save();
            }
            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpGet]
        [Route("{cartId}/availshippingrates")]
        [ResponseType(typeof(ICollection<ShippingRate>))]
        public IHttpActionResult GetAvailableShippingRates(string cartId)
        {
            var cart = _shoppingCartService.GetByIds(new[] { cartId }).FirstOrDefault();
            var shippingRates = _cartBuilder.TakeCart(cart).GetAvailableShippingRates();
            return Ok(shippingRates);
        }

        [HttpGet]
        [Route("{cartId}/availpaymentmethods")]
        [ResponseType(typeof(ICollection<Domain.Payment.Model.PaymentMethod>))]
        public IHttpActionResult GetAvailablePaymentMethods(string cartId)
        {
            var cart = _shoppingCartService.GetByIds(new[] { cartId }).FirstOrDefault();
            var paymentMethods = _cartBuilder.TakeCart(cart).GetAvailablePaymentMethods();
            return Ok(paymentMethods);
        }

        [HttpPost]
        [Route("{cartId}/coupons/{couponCode}")]
        [ResponseType(typeof(void))]
        public async Task<IHttpActionResult> AddCartCoupon(string cartId, string couponCode)
        {
            using (await AsyncLock.GetLockByKey(GetAsyncLockCartKey(cartId)).LockAsync())
            {
                var cart = _shoppingCartService.GetByIds(new[] { cartId }).FirstOrDefault();
                _cartBuilder.TakeCart(cart).AddCoupon(couponCode).Save();
            }
            return Ok();
        }

        [HttpDelete]
        [Route("{cartId}/coupons/{couponCode}")]
        [ResponseType(typeof(void))]
        public async Task<IHttpActionResult> RemoveCartCoupon(string cartId, string couponCode)
        {
            using (await AsyncLock.GetLockByKey(GetAsyncLockCartKey(cartId)).LockAsync())
            {
                var cart = _shoppingCartService.GetByIds(new[] { cartId }).FirstOrDefault();
                _cartBuilder.TakeCart(cart).RemoveCoupon().Save();
            }
            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [Route("{cartId}/shipments")]
        [ResponseType(typeof(void))]
        public async Task<IHttpActionResult> AddOrUpdateCartShipment(string cartId, [FromBody] Shipment shipment)
        {
            using (await AsyncLock.GetLockByKey(GetAsyncLockCartKey(cartId)).LockAsync())
            {
                var cart = _shoppingCartService.GetByIds(new[] { cartId }).FirstOrDefault();
                _cartBuilder.TakeCart(cart).AddOrUpdateShipment(shipment).Save();
            }
            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [Route("{cartId}/payments")]
        [ResponseType(typeof(void))]
        public async Task<IHttpActionResult> AddOrUpdateCartPayment(string cartId, [FromBody] Payment payment)
        {
            using (await AsyncLock.GetLockByKey(GetAsyncLockCartKey(cartId)).LockAsync())
            {
                var cart = _shoppingCartService.GetByIds(new[] { cartId }).FirstOrDefault();
                _cartBuilder.TakeCart(cart).AddOrUpdatePayment(payment).Save();
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Get shopping cart by id
        /// </summary>
        /// <param name="cartId">Shopping cart id</param>
        [HttpGet]
        [Route("{cartId}")]
        [ResponseType(typeof(ShoppingCart))]
        public IHttpActionResult GetCartById(string cartId)
        {
            var retval = _shoppingCartService.GetByIds(new[] { cartId }).FirstOrDefault();
            return Ok(retval);
        }

        /// <summary>
        /// Search shopping carts by given criteria
        /// </summary>
        /// <param name="criteria">Shopping cart search criteria</param>
        [HttpPost]
        [Route("search")]
        [ResponseType(typeof(ShoppingCartSearchResult))]
        [CheckPermission(Permission = PredefinedPermissions.Query)]
        public IHttpActionResult Search(ShoppingCartSearchCriteria criteria)
        {
            var result = _searchService.Search(criteria);
            var retVal = new ShoppingCartSearchResult
            {
                Results = result.Results.ToList(),
                TotalCount = result.TotalCount
            };
            return Ok(retVal);
        }

        /// <summary>
        /// Create shopping cart
        /// </summary>
        /// <param name="cart">Shopping cart model</param>
        [HttpPost]
        [Route("")]
        [ResponseType(typeof(ShoppingCart))]
        [CheckPermission(Permission = PredefinedPermissions.Create)]
        public IHttpActionResult Create(ShoppingCart cart)
        {
            _shoppingCartService.SaveChanges(new[] { cart });
            return Ok(cart);
        }

        /// <summary>
        /// Update shopping cart
        /// </summary>
        /// <param name="cart">Shopping cart model</param>
        [HttpPut]
        [Route("")]
        [ResponseType(typeof(ShoppingCart))]
        [CheckPermission(Permission = PredefinedPermissions.Update)]
        public async Task<IHttpActionResult> Update(ShoppingCart cart)
        {
            using (await AsyncLock.GetLockByKey(GetAsyncLockCartKey(cart.Id)).LockAsync())
            {
                _shoppingCartService.SaveChanges(new[] { cart });
            }
            return Ok(cart);
        }


        /// <summary>
        /// Delete shopping carts by ids
        /// </summary>
        /// <param name="ids">Array of shopping cart ids</param>
        [HttpDelete]
        [Route("")]
        [ResponseType(typeof(void))]
        [CheckPermission(Permission = PredefinedPermissions.Delete)]
        public IHttpActionResult DeleteCarts([FromUri] string[] ids)
        {
            _shoppingCartService.Delete(ids);
            return StatusCode(HttpStatusCode.NoContent);
        }

        private static string GetAsyncLockCartKey(string cartId)
        {
            return "Cart:" + cartId;
        }

    }
}
