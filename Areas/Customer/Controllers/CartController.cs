using JustKeyNew.DataAccess.Repository.IRepository;
using JustKeyNew.Models;
using JustKeyNew.Models.ViewModels;
using JustKeyNew.Utility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System.Drawing;
using System;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Security.Claims;
using System.Drawing.Imaging;
using JustKeyNew.Services;
using System.Drawing.Printing;
using Microsoft.Extensions.Options;
using PrinterSettings = JustKeyNew.Utility.PrinterSettings;

namespace JustKeyNew.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class CartController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IPrintNodeService _printNodeService;
        private readonly Utility.PrinterSettings _printerSettings;

        [BindProperty]
        public ShoppingCartVM ShoppingCartVM { get; set; }

        public CartController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment, IPrintNodeService printNodeService, IOptions<PrinterSettings> printerSettings)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
            _printNodeService = printNodeService;
            _printerSettings = printerSettings.Value;
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
                    SuccessUrl = domain + $"customer/cart/OrderConfirmation?id={ShoppingCartVM.OrderHeader.Id}&paymentMethod={paymentMethod}",
                    CancelUrl = domain + "customer/cart/index",
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

            string orderheaderid = orderHeader.Id.ToString();
            string wwwRootPath = _webHostEnvironment.WebRootPath;
            string textFilesPath = Path.Combine(wwwRootPath, "textfiles", "order");

            if (!Directory.Exists(textFilesPath))
            {
                Directory.CreateDirectory(textFilesPath);
            }

            string outputFilePath = Path.Combine(textFilesPath, $"{orderheaderid}.txt");
            string text = $"{orderHeader.TableNo}\n---------------------------------\n";
            int orderHeaderId = orderHeader.Id;
            var orderDetails = _unitOfWork.OrderDetail.GetAll(u => u.OrderHeaderId == orderHeaderId, includeProperties: "Product,DetailExtras").ToList();

            foreach (var orderDetail in orderDetails)
            {
                text += $"{orderDetail.Product.Title.ToUpper()} X{orderDetail.Count} \n";
                foreach (var extras in orderDetail.DetailExtras)
                {
                    text += $"  * {extras.ExtraName}\n";
                }
            }

            // Sipariş metninin altına boşluk eklemek
            for (int i = 0; i < 4; i++)
            {
                text += "\n";
            }

            System.IO.File.WriteAllText(outputFilePath, text);

            _printNodeService.PrintFileAsync(_printerSettings.PrinterId, outputFilePath).Wait();

            var shoppingCarts = _unitOfWork.ShoppingCart.GetAll(includeProperties: "SelectedExtra").ToList();
            _unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
            _unitOfWork.Save();
            var sessionId = HttpContext.Session.Id;
            HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart.GetAll(u => u.SessionId == sessionId).Count());
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
            var sessionId = HttpContext.Session.Id;
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId, tracked: true, includeProperties: "SelectedExtra");
            if (cartFromDb.Count == 1)
            {
                _unitOfWork.ShoppingCart.Remove(cartFromDb);
                HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart.GetAll(u => u.SessionId == sessionId).Count() - 1 );
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
            var sessionId = HttpContext.Session.Id;
            var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId, tracked: true, includeProperties: "SelectedExtra");
            HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart.GetAll(u => u.SessionId == sessionId).Count() - 1);
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
