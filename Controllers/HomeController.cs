using JustKeyNew.DataAccess.Data;
using JustKeyNew.DataAccess.Repository.IRepository;
using JustKeyNew.Models;
using JustKeyNew.Models.ViewModels;
using JustKeyNew.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Diagnostics;

namespace JustKeyNew.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public HomeController(ILogger<HomeController> logger, IUnitOfWork unitOfWork, DbContextOptions<ApplicationDbContext> options)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _options = options;
        }

        public IActionResult Index()
        {
            var categoryList = _unitOfWork.Category.GetAll();
            return View(categoryList);
        }

        public IActionResult CategoriesToProducts(int categoryId)
        {
            var productList = _unitOfWork.Product.GetAll(u => u.CategoryId == categoryId, includeProperties: "Category");
            return View(productList);
        }

        public IActionResult Details(int productId)
        {
            var product = _unitOfWork.Product.Get(u => u.Id == productId, includeProperties: "Category,ProductExtras,ProductMaterials");

            // Sepet verisini oturumda sakla
            var cart = new ShoppingCart
            {
                Product = product,
                Count = 1,
                ProductId = productId,
                SessionId = HttpContext.Session.Id
            };

            // Oturumda sepeti sakla
            HttpContext.Session.SetObjectAsJson("Cart", cart);

            var cartVM = new CartVM
            {
                ShoppingCart = cart,
            };

            return View(cartVM);
        }

        [HttpPost]
        public IActionResult Details(CartVM cartVM)
        {
            var productExtras = _unitOfWork.ProductExtra.GetAll(u => u.ProductId == cartVM.ShoppingCart.Product.Id).ToList();

            // Oturum ID'sini al
            var sessionId = HttpContext.Session.Id;

            var cartFromDb = _unitOfWork.ShoppingCart.GetFirstOrDefault(
                u => u.ProductId == cartVM.ShoppingCart.Product.Id && u.SessionId == sessionId, includeProperties: "SelectedExtra");

            if (cartFromDb != null)
            {
                bool isSameExtras = false;

                if (cartFromDb.SelectedExtra != null && cartVM.ShoppingCart.SelectedExtra != null)
                {
                    isSameExtras = cartFromDb.SelectedExtra.Select(e => e.Id).OrderBy(id => id)
                                          .SequenceEqual(cartVM.ShoppingCart.SelectedExtra.Select(e => e.Id).OrderBy(id => id));
                }

                if (isSameExtras)
                {
                    cartFromDb.Count += cartVM.ShoppingCart.Count;
                    _unitOfWork.ShoppingCart.Update(cartFromDb);
                    TempData["success"] = "Cart updated successfully.";
                }
                else
                {
                    AddNewCartWithExtras(cartVM, productExtras, sessionId);
                    TempData["success"] = "Cart created successfully.";
                }
            }
            else
            {
                AddNewCartWithExtras(cartVM, productExtras, sessionId);
                TempData["success"] = "Cart created successfully.";
            }

            _unitOfWork.Save();

            return RedirectToAction(nameof(Index));
        }

        private void AddNewCartWithExtras(CartVM cartVM, List<ProductExtra> productExtras, string sessionId)
        {
            var newCart = new ShoppingCart
            {
                ProductId = cartVM.ShoppingCart.Product.Id,
                Count = cartVM.ShoppingCart.Count,
                SessionId = sessionId
            };
            _unitOfWork.ShoppingCart.Add(newCart);
            _unitOfWork.Save();

            cartVM.ShoppingCart.SelectedExtra = new List<ShoppingCartExtra>();

            for (int i = 0; i < productExtras.Count; i++)
            {
                if (cartVM.IsExtraSelected[i])
                {
                    var selectedExtra = productExtras[i];

                    var shoppingCartExtra = new ShoppingCartExtra
                    {
                        ExtraName = selectedExtra.ExtraName,
                        Price = selectedExtra.Price,
                        ShoppingCartId = newCart.Id,
                    };
                    cartVM.ShoppingCart.SelectedExtra.Add(shoppingCartExtra);
                }
            }

            newCart.SelectedExtra = cartVM.ShoppingCart.SelectedExtra;
            _unitOfWork.ShoppingCart.Update(newCart);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
