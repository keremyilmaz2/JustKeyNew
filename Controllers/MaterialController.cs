using JustKeyNew.DataAccess.Data;
using JustKeyNew.DataAccess.Repository.IRepository;
using JustKeyNew.Models;
using JustKeyNew.Models.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace JustKeyNew.Controllers
{
    public class MaterialController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ApplicationDbContext _context;

        public MaterialController(IUnitOfWork unitOfWork,ApplicationDbContext context)
        {
            _unitOfWork = unitOfWork;  
            _context = context;
        }
        public IActionResult Index()
        {
            var materials = _context.CategoryMaterials
            .Include(m => m.Category)
            .GroupBy(m => m.MaterialName)
            .Select(g => new MaterialVM
            {
                MaterialName = g.Key,
                Categories = g.Select(m => m.Category.Name).ToList()
            })
            .ToList();

            return View(materials);
        }
        public IActionResult Create()
        {
            CategoryMaterialVM categoryMaterialVM = new()
            {
                CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                }),
                MaterialNameList = _unitOfWork.CategoryMaterial.GetAll().Select(u => new SelectListItem
                {
                    Text = u.MaterialName,
                    Value = u.Id.ToString()
                }),
                CategoryMaterial = new CategoryMaterial()
            };
            return View(categoryMaterialVM);
        }
        [HttpPost]
        public IActionResult Create(CategoryMaterialVM categoryMaterialVM)
        {
            
            if (ModelState.IsValid) 
            {
                categoryMaterialVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                });

                for (int i = 0; i < categoryMaterialVM.CategoryList.Count(); i++)
                {
                    if (categoryMaterialVM.IsCategorySelected[i])
                    {
                        
                        var selectedCategoryId = int.Parse(categoryMaterialVM.CategoryList.ElementAt(i).Value);
                        CategoryMaterial categoryMaterial = new()
                        {
                            MaterialName = categoryMaterialVM.CategoryMaterial.MaterialName,
                            CategoryId = selectedCategoryId
                        };
                        _unitOfWork.CategoryMaterial.Add(categoryMaterial);
                        _unitOfWork.Save();
                    }
                }

     
                IEnumerable<Stock> objStockList = _unitOfWork.Stock.GetAll().ToList();
                var existingItem = objStockList.FirstOrDefault(item => item.Name == categoryMaterialVM.CategoryMaterial.MaterialName);
                if (existingItem == null)
                {
                    Stock stock = new()
                    {
                        Name = categoryMaterialVM.CategoryMaterial.MaterialName,
                        TotalAmount = 0,
                    };

                    _unitOfWork.Stock.Add(stock);
                    _unitOfWork.Save();
                }
                

                TempData["success"] = "Material created succesfully.";
                return RedirectToAction("Index");
            }
            
            else
            {
                categoryMaterialVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                }); 
                categoryMaterialVM.MaterialNameList = _unitOfWork.CategoryMaterial.GetAll().Select(u => new SelectListItem
                {
                    Text = u.MaterialName,
                    Value = u.Id.ToString()
                });
                return View(categoryMaterialVM);
            }



        }

        public IActionResult Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var material = _unitOfWork.CategoryMaterial.GetFirstOrDefault(m => m.MaterialName == id, includeProperties: "Category");
            if (material == null)
            {
                return NotFound();
            }

            var selectedCategories = _unitOfWork.CategoryMaterial.GetAll()
                .Where(cm => cm.MaterialName == id)
                .Select(cm => cm.CategoryId)
                .ToList();

            CategoryMaterialVM categoryMaterialVM = new()
            {
                CategoryMaterial = material,
                CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                }),
                MaterialNameList = _unitOfWork.CategoryMaterial.GetAll().Select(u => new SelectListItem
                {
                    Text = u.MaterialName,
                    Value = u.Id.ToString()
                }),
                IsCategorySelected = _unitOfWork.Category.GetAll().Select(u => selectedCategories.Contains(u.Id)).ToList()
            };

            return View(categoryMaterialVM);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(CategoryMaterialVM categoryMaterialVM)
        {
            if (ModelState.IsValid)
            {
                categoryMaterialVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                });

                var material = _unitOfWork.CategoryMaterial.GetFirstOrDefault(m => m.MaterialName == categoryMaterialVM.CategoryMaterial.MaterialName, includeProperties: "Category");
                if (material == null)
                {
                    return NotFound();
                }

                for (int i = 0; i < categoryMaterialVM.CategoryList.Count(); i++)
                {
                    var category = categoryMaterialVM.CategoryList.ElementAt(i);
                    var selectedCategoryId = int.Parse(category.Value);
                    var existingMaterial = _unitOfWork.CategoryMaterial.GetFirstOrDefault(m => m.MaterialName == categoryMaterialVM.CategoryMaterial.MaterialName && m.CategoryId == selectedCategoryId);

                    if (categoryMaterialVM.IsCategorySelected[i])
                    {
                        if (existingMaterial == null)
                        {
                            CategoryMaterial newMaterial = new()
                            {
                                MaterialName = categoryMaterialVM.CategoryMaterial.MaterialName,
                                CategoryId = selectedCategoryId
                            };
                            _unitOfWork.CategoryMaterial.Add(newMaterial);
                        }
                    }
                    else
                    {
                        if (existingMaterial != null)
                        {
                            _unitOfWork.CategoryMaterial.Remove(existingMaterial);
                        }
                    }
                }

                _unitOfWork.Save();
                return RedirectToAction("Index");
            }

            categoryMaterialVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
            {
                Text = u.Name,
                Value = u.Id.ToString()
            });

            categoryMaterialVM.MaterialNameList = _unitOfWork.CategoryMaterial.GetAll().Select(u => new SelectListItem
            {
                Text = u.MaterialName,
                Value = u.Id.ToString()
            });

            return View(categoryMaterialVM);
        }




    }
}
