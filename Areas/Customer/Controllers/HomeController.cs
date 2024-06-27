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
using JustKeyNew.Utility;
using Microsoft.Extensions.Options;

namespace JustKeyNew.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class HomeController : Controller
    {
        
        private readonly IUnitOfWork _unitOfWork;
        

        public HomeController( IUnitOfWork unitOfWork)
        {
            
            _unitOfWork = unitOfWork;
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

            
            var cart = new ShoppingCart
            {
                Product = product,
                Count = 1,
                ProductId = productId,
                SessionId = HttpContext.Session.Id
            };

            
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
                }
            }
            else
            {
                AddNewCartWithExtras(cartVM, productExtras, sessionId);
            }

            _unitOfWork.Save();

            return RedirectToAction(nameof(Index));
        }


        private void AddNewCartWithExtras(CartVM cartVM, List<ProductExtra> productExtras, string sessionId)
        {
            var productfromdb = _unitOfWork.Product.Get(u => u.Id == cartVM.ShoppingCart.Product.Id,includeProperties:"Category");

            
            if (cartVM.ShoppingCart.Count <= productfromdb.AvailableProducts || productfromdb.Category.ProductCount == false)
            {
                productfromdb.AvailableProducts -= cartVM.ShoppingCart.Count;
                _unitOfWork.Product.Update(productfromdb);

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
                _unitOfWork.Save();
                HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart.GetAll(u => u.SessionId == sessionId).Count());
                TempData["success"] = "Product added to cart successfully.";
            }
            else
            {
                TempData["error"] = "Not enough products in stock.";
                
                RedirectToAction(nameof(CategoriesToProducts), new { categoryId = productfromdb.CategoryId });
            }
        }


        public IActionResult AddProductCount(int id)
        {
            var product = _unitOfWork.Product.Get(u => u.Id == id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        [HttpPost]
        public IActionResult AddProductCount(Product product)
        {
            var productFromDb = _unitOfWork.Product.Get(u => u.Id == product.Id, includeProperties: "Category,ProductExtras,ProductMaterials");
            if (productFromDb != null)
            {
                productFromDb.AvailableProducts = product.AvailableProducts;
             
                _unitOfWork.Product.Update(productFromDb);
                _unitOfWork.Save();

                
                var updatedProduct = _unitOfWork.Product.Get(u => u.Id == productFromDb.Id);
                if (updatedProduct.AvailableProducts == product.AvailableProducts)
                {
                    TempData["success"] = "Product count updated successfully.";
                }
                else
                {
                    TempData["error"] = "Product count update failed.";
                }

                return RedirectToAction(nameof(CategoriesToProducts), new { categoryId = productFromDb.CategoryId });
            }

            return View(product);
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
