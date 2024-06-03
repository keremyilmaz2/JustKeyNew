using JustKeyNew.DataAccess.Repository.IRepository;
using JustKeyNew.Models;
using JustKeyNew.Models.ViewModels;
using JustKeyNew.Utility;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace JustKeyNew.Controllers
{
    public class CartController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        [BindProperty]
        public ShoppingCartVM ShoppingCartVM { get; set; }

        public CartController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            ShoppingCartVM = new ShoppingCartVM
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(includeProperties: "Product,SelectedExtra"),
                OrderHeader = new OrderHeader()
            };

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartVM);
        }

        public IActionResult Summary()
        {
            ShoppingCartVM = new ShoppingCartVM
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(includeProperties: "Product,SelectedExtra"),
                OrderHeader = new OrderHeader()
            };

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartVM);
        }

        [HttpPost]
        [ActionName("Summary")]
        public IActionResult SummaryPOST(string paymentMethod)
        {
            ShoppingCartVM.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(includeProperties: "Product,SelectedExtra");
            ShoppingCartVM.OrderHeader.OrderDate = System.DateTime.Now;

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }

            ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
            ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;

            _unitOfWork.OrderHeader.Add(ShoppingCartVM.OrderHeader);
            _unitOfWork.Save();

            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                var orderDetail = new OrderDetail
                {
                    ProductId = cart.ProductId,
                    OrderHeaderId = ShoppingCartVM.OrderHeader.Id,
                    Price = cart.Price,
                    Count = cart.Count,
                };
                _unitOfWork.OrderDetail.Add(orderDetail);
                _unitOfWork.Save();

                var orderDetailExtras = cart.SelectedExtra.Select(extra => new OrderDetailExtra
                {
                    ExtraName = extra.ExtraName,
                    Price = extra.Price,
                    OrderDetailId = orderDetail.Id
                }).ToList();

                orderDetail.DetailExtras = orderDetailExtras;
                _unitOfWork.OrderDetail.Update(orderDetail);
                _unitOfWork.Save();
            }

            if (paymentMethod == "CreditCard")
            {
                var domain = Request.Scheme + "://" + Request.Host.Value + "/";
                var options = new Stripe.Checkout.SessionCreateOptions
                {
                    SuccessUrl = domain + $"cart/OrderConfirmation?id={ShoppingCartVM.OrderHeader.Id}&paymentMethod={paymentMethod}",
                    CancelUrl = domain + "cart/index",
                    LineItems = new List<Stripe.Checkout.SessionLineItemOptions>(),
                    Mode = "payment",
                };

                foreach (var item in ShoppingCartVM.ShoppingCartList)
                {
                    var sessionLineItem = new Stripe.Checkout.SessionLineItemOptions
                    {
                        PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(item.Price * 100),
                            Currency = "usd",
                            ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                            {
                                Name = item.Product.Title
                            }
                        },
                        Quantity = item.Count
                    };
                    options.LineItems.Add(sessionLineItem);
                }

                var service = new Stripe.Checkout.SessionService();
                var session = service.Create(options);
                _unitOfWork.OrderHeader.UpdateStripePaymentID(ShoppingCartVM.OrderHeader.Id, session.Id, session.PaymentIntentId);
                _unitOfWork.Save();
                Response.Headers.Add("Location", session.Url);
                return new StatusCodeResult(303);
            }
            else if (paymentMethod == "Cash")
            {
                ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
                ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
                _unitOfWork.OrderHeader.Update(ShoppingCartVM.OrderHeader);
                _unitOfWork.Save();

                return RedirectToAction(nameof(OrderConfirmation), new { id = ShoppingCartVM.OrderHeader.Id, paymentMethod = paymentMethod });
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult OrderConfirmation(int id, string paymentMethod)
        {
            var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == id);

            if (orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
            {
                var service = new Stripe.Checkout.SessionService();
                var session = service.Get(orderHeader.SessionId);

                if (session.PaymentStatus.ToLower() == "paid")
                {
                    _unitOfWork.OrderHeader.UpdateStripePaymentID(id, session.Id, session.PaymentIntentId);
                    _unitOfWork.OrderHeader.UpdateStatus(id, SD.StatusCreditCart, SD.PaymentStatusApproved);
                    _unitOfWork.Save();
                }
            }

            var shoppingCarts = _unitOfWork.ShoppingCart.GetAll(includeProperties: "SelectedExtra").ToList();
            _unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
            _unitOfWork.Save();

            ViewBag.PaymentMethod = paymentMethod;

            return View(id);
        }

        public IActionResult Plus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId);
            cartFromDb.Count += 1;
            _unitOfWork.ShoppingCart.Update(cartFromDb);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Minus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId, tracked: true, includeProperties: "SelectedExtra");
            if (cartFromDb.Count == 1)
            {
                _unitOfWork.ShoppingCart.Remove(cartFromDb);
            }
            else
            {
                cartFromDb.Count -= 1;
                _unitOfWork.ShoppingCart.Update(cartFromDb);
            }

            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Remove(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId, tracked: true, includeProperties: "SelectedExtra");
            _unitOfWork.ShoppingCart.Remove(cartFromDb);
            _unitOfWork.Save();

            return RedirectToAction(nameof(Index));
        }

        private double GetPriceBasedOnQuantity(ShoppingCart shoppingCart)
        {
            double extrasPrice = shoppingCart.SelectedExtra.Sum(extra => extra.Price);
            return shoppingCart.Product.Price + extrasPrice;
        }
    }
}
