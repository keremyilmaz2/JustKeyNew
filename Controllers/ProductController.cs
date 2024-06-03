using JustKeyNew.DataAccess.Data;
using JustKeyNew.DataAccess.Repository.IRepository;
using JustKeyNew.Models;
using JustKeyNew.Models.ViewModels;
using JustKeyNew.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;

namespace JustKeyNew.Controllers
{

    public class ProductController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
        }
        public IActionResult Index(string status)
        {
            ProductVM productVM = new ProductVM
            {
                CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                }).ToList()
            };

            if (!string.IsNullOrEmpty(status))
            {
                if (status == "All")
                {
                    var products = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();
                    productVM.ProductList = products;
                }
                else
                {
                    var products = _unitOfWork.Product.GetAll(includeProperties: "Category").Where(u => u.Category.Name == status).ToList();
                    productVM.ProductList = products;
                }
                
            }
            else
            {
                var products = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();
                productVM.ProductList = products;
            }

            return View(productVM);
        }


        public IActionResult Upsert(int? id)
        {
            ProductVM productVM = new()
            {
                CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                }),
                CategoryMaterials = _unitOfWork.CategoryMaterial.GetAll().Select(u => new SelectListItem
                {
                    Text = u.MaterialName,
                    Value = u.CategoryId.ToString()
                }),
                CategoryExtras = _unitOfWork.CategoryExtra.GetAll().Select(u => new SelectListItem
                {
                    Text = u.ExtraName,
                    Value = u.CategoryId.ToString()
                }),
                Product = new Product()
            };
            if (id == null || id == 0)
            {
                return View(productVM);
            }
            else
            {
                productVM.Product = _unitOfWork.Product.Get(u => u.Id == id);
                return View(productVM);
            }
        }


        [HttpPost]
        public IActionResult Upsert(ProductVM productVM, IFormFile? file)
        {
            if (productVM.Product.Id != 0)
            {
                var existingProduct = _unitOfWork.Product.Get(u => u.Id == productVM.Product.Id);
                if (existingProduct != null)
                {
                    productVM.Product.ProductImageUrl = existingProduct.ProductImageUrl;
                }
            }

            if (ModelState.IsValid)
            {
                string wwwRootPath = _webHostEnvironment.WebRootPath;
                if (file != null)
                {
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    string productPath = Path.Combine(wwwRootPath, "images", "product");

                    if (!Directory.Exists(productPath))
                    {
                        Directory.CreateDirectory(productPath);
                    }

                    if (!string.IsNullOrEmpty(productVM.Product.ProductImageUrl))
                    {
                        string oldImagePath = Path.Combine(wwwRootPath, productVM.Product.ProductImageUrl.TrimStart('\\'));

                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    string filePath = Path.Combine(productPath, fileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        file.CopyTo(fileStream);
                    }

                    productVM.Product.ProductImageUrl = @"\images\product\" + fileName;
                }

                if (productVM.Product.Id == 0)
                {
                    _unitOfWork.Product.Add(productVM.Product);
                }
                else
                {
                    _unitOfWork.Product.Update(productVM.Product);
                }

                _unitOfWork.Save();

                TempData["success"] = "Product created/updated successfully.";
                return RedirectToAction(nameof(Content), new { id = productVM.Product.Id });
            }
            else
            {
                productVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                });
                return View(productVM);
            }
        }


        public IActionResult Content(int? id)
        {
            ProductVM productVM = new()
            {
                CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                }),
                CategoryMaterials = _unitOfWork.CategoryMaterial.GetAll().Select(u => new SelectListItem
                {
                    Text = u.MaterialName,
                    Value = u.CategoryId.ToString()
                }),
                Product = _unitOfWork.Product.Get(u => u.Id == id, includeProperties: "ProductMaterials")
            };

            productVM.Product.ProductMaterials = new List<ProductMaterial>();
            IEnumerable<CategoryMaterial> categoryMaterials = _unitOfWork.CategoryMaterial.GetAll(u => u.CategoryId == productVM.Product.CategoryId).ToList();

            foreach (CategoryMaterial categoryMaterial in categoryMaterials)
            {
                productVM.Product.ProductMaterials.Add(new ProductMaterial
                {
                    MaterialName = categoryMaterial.MaterialName,
                    Amount = 0,
                    ProductId = productVM.Product.Id,
                });
            }

            return View(productVM);

        }

        

        [HttpPost]
        public IActionResult Content(ProductVM productVM)
        {
            var product = _unitOfWork.Product.Get(u => u.Id == productVM.Product.Id);
            var materials = productVM.Product.ProductMaterials;
            if (product != null)
            {
                _unitOfWork.ProductMaterial.RemoveRange(_unitOfWork.ProductMaterial.GetAll(u => u.ProductId == product.Id));
                _unitOfWork.Save();
                if (materials != null)
                {

                    foreach (var material in materials)
                    {
                        ProductMaterial productMaterial = new()
                        {
                            MaterialName = material.MaterialName,
                            Amount = material.Amount,
                            ProductId = product.Id
                        };
                        _unitOfWork.ProductMaterial.Add(productMaterial);
                        _unitOfWork.Save();

                        if (productVM.Product.ProductMaterials != null)
                        {
                            productVM.Product.ProductMaterials = new List<ProductMaterial>();
                        }
                        productVM.Product.ProductMaterials.Add(productMaterial);


                    }
                    Product saveproduct = new()
                    {
                        Id = product.Id,
                        Title = product.Title,
                        Description = product.Description,
                        Price = product.Price,
                        CategoryId = product.CategoryId,
                        ProductImageUrl = product.ProductImageUrl,
                        ProductMaterials = productVM.Product.ProductMaterials,

                    };
                    _unitOfWork.Product.Update(saveproduct);
                    _unitOfWork.Save();
                }

            }

            return RedirectToAction(nameof(ExtraSelect), new { id = productVM.Product.Id });

        }

        public IActionResult ExtraSelect(int? id)
        {
            var product = _unitOfWork.Product.Get(u => u.Id == id, includeProperties: "ProductExtras");

            if (product == null)
            {
                return NotFound();
            }

            var categoryExtras = _unitOfWork.CategoryExtra.GetAll(u => u.CategoryId == product.CategoryId).ToList();

            var productVM = new ProductVM
            {
                CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                }).ToList(),
                CategoryExtras = categoryExtras.Select(u => new SelectListItem
                {
                    Text = u.ExtraName,
                    Value = u.Id.ToString()
                }).ToList(),
                Product = product
            };

            productVM.Product.ProductExtras = new List<ProductExtra>();
            foreach (var categoryExtra in categoryExtras)
            {
                productVM.Product.ProductExtras.Add(new ProductExtra
                {
                    ExtraName = categoryExtra.ExtraName,
                    Price = categoryExtra.Price,
                    ProductId = product.Id
                });
            }

            return View(productVM);
        }

        [HttpPost]
        public IActionResult ExtraSelect(ProductVM productVM)
        {
            var product = _unitOfWork.Product.Get(u => u.Id == productVM.Product.Id, includeProperties: "ProductExtras");

            if (product == null)
            {
                return NotFound();
            }

            if (product != null)
            {
                _unitOfWork.ProductExtra.RemoveRange(_unitOfWork.ProductExtra.GetAll(u => u.ProductId == product.Id));
                _unitOfWork.Save();

                var categoryExtras = _unitOfWork.CategoryExtra.GetAll(u => u.CategoryId == product.CategoryId).ToList();

                for (int i = 0; i < categoryExtras.Count(); i++)
                {
                    if (productVM.IsExtraSelected[i])
                    {
                        var selectedExtra = categoryExtras[i];
                        var productExtra = new ProductExtra
                        {
                            ExtraName = selectedExtra.ExtraName,
                            Price = selectedExtra.Price,
                            ProductId = product.Id
                        };
                        _unitOfWork.ProductExtra.Add(productExtra);
                        _unitOfWork.Save();

                        if (productVM.Product.ProductExtras == null)
                        {
                            productVM.Product.ProductExtras = new List<ProductExtra>();
                        }
                        productVM.Product.ProductExtras.Add(productExtra);
                    }
                }

                product.ProductExtras = productVM.Product.ProductExtras;
                _unitOfWork.Product.Update(product);
                _unitOfWork.Save();

                TempData["success"] = "Material created successfully.";
                return RedirectToAction("Index");
            }
            else
            {
                productVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                }).ToList();
                productVM.CategoryExtras = _unitOfWork.CategoryExtra.GetAll(u => u.CategoryId == product.CategoryId).Select(u => new SelectListItem
                {
                    Text = u.ExtraName,
                    Value = u.Id.ToString()
                }).ToList();
                productVM.Product = _unitOfWork.Product.Get(u => u.Id == productVM.Product.Id, includeProperties: "ProductExtras");

                return View(productVM);
            }
        }


        public IActionResult Delete(int? id)
        {
            var productToBeDelete = _unitOfWork.Product.Get(u => u.Id == id);
            if (productToBeDelete == null)
            {
                return Json(new { success = false, message = "Error while deleting" });
            }

            string productPath = @"images\products\product-" + id;
            string finalPath = Path.Combine(_webHostEnvironment.WebRootPath, productPath);

            if (Directory.Exists(finalPath))
            {
                string[] filepaths = Directory.GetFiles(finalPath);
                foreach (var filepath in filepaths)
                {
                    System.IO.File.Delete(filepath);
                }
                Directory.Delete(finalPath);
            }

            _unitOfWork.Product.Remove(productToBeDelete);
            _unitOfWork.Save();

            return RedirectToAction("Index");
        }


        #region API CALLS
        [HttpGet]
        public IActionResult GetAll(string status)
        {
            IEnumerable<Product> objProductList = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();
            List<Category> objCategoryList = _unitOfWork.Category.GetAll().ToList();
            if (status == null) { status = "All"; }
            foreach (var objCategory in objCategoryList) 
            {
                if (objCategory.Name.ToLower() == status.ToLower())
                {
                    objProductList = objProductList.Where(u => u.CategoryId == objCategory.Id).ToList();
                    break;
                }
                if (status == "All")
                {
                    objProductList = objProductList;
                }

            }
            return Json(new { data = objProductList });
        }

        //[HttpDelete]
        //public IActionResult Delete(int? id)
        //{
        //    var productToBeDelete = _unitOfWork.Product.Get(u => u.Id == id);
        //    if (productToBeDelete == null)
        //    {
        //        return Json(new { success = false, message = "Error while deleting" });
        //    }

        //    string productPath = @"images\products\product-" + id;
        //    string finalPath = Path.Combine(_webHostEnvironment.WebRootPath, productPath);

        //    if (Directory.Exists(finalPath))
        //    {
        //        string[] filepaths = Directory.GetFiles(finalPath);
        //        foreach (var filepath in filepaths)
        //        {
        //            System.IO.File.Delete(filepath);
        //        }
        //        Directory.Delete(finalPath);
        //    }

        //    _unitOfWork.Product.Remove(productToBeDelete);
        //    _unitOfWork.Save();

        //    return Json(new { success = true, message = "Delete successful" });
        //}
        #endregion
    }
}