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
        public IActionResult Index()
        {
            //List<Product> objProductList = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();
            ProductVM productVM = new()
            {
                CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                }),
                Product = new Product()
            };


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
                Product = _unitOfWork.Product.Get(u => u.Id == id)
            };
            
            return View(productVM);
            
        }

        [HttpPost]
        public IActionResult Upsert(ProductVM productVM, IFormFile file)
        {

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

            TempData["success"] = "Product created/updated succesfully.";
            return RedirectToAction(nameof(Content), new { id = productVM.Product.Id});
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

        [HttpDelete]
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

            return Json(new { success = true, message = "Delete successful" });
        }
        #endregion
    }
}