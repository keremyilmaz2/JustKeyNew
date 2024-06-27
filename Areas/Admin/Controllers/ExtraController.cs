using JustKeyNew.DataAccess.Data;
using JustKeyNew.DataAccess.Repository.IRepository;
using JustKeyNew.Models;
using JustKeyNew.Models.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace JustKeyNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ExtraController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ApplicationDbContext _context;

        public ExtraController(IUnitOfWork unitOfWork, ApplicationDbContext context)
        {
            _unitOfWork = unitOfWork;
            _context = context;
        }
        public IActionResult Index()
        {
            var extras = _context.CategoryExtras
            .Include(m => m.Category)
            .GroupBy(m => m.ExtraName)
            .Select(g => new ExtraVM
            {
                ExtraName = g.Key,
                Categories = g.Select(m => m.Category.Name).ToList()
            })
            .ToList();

            return View(extras);
        }
        public IActionResult Create()
        {
            CategoryExtraVM categoryExtraVM = new()
            {
                CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                }),
                ExtraNameList = _unitOfWork.CategoryExtra.GetAll().Select(u => new SelectListItem
                {
                    Text = u.ExtraName,
                    Value = u.Id.ToString()
                }),
                CategoryExtra = new CategoryExtra()
            };
            return View(categoryExtraVM);
        }
        [HttpPost]
        public IActionResult Create(CategoryExtraVM categoryExtraVM)
        {

            if (ModelState.IsValid)
            {
                categoryExtraVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                });

                for (int i = 0; i < categoryExtraVM.CategoryList.Count(); i++)
                {
                    if (categoryExtraVM.IsCategorySelected[i])
                    {

                        var selectedCategoryId = int.Parse(categoryExtraVM.CategoryList.ElementAt(i).Value);
                        CategoryExtra categoryExtra = new()
                        {
                            ExtraName = categoryExtraVM.CategoryExtra.ExtraName,
                            Price = categoryExtraVM.CategoryExtra.Price,
                            CategoryId = selectedCategoryId
                        };
                        _unitOfWork.CategoryExtra.Add(categoryExtra);
                        _unitOfWork.Save();
                    }
                }

                TempData["success"] = "Material created succesfully.";
                return RedirectToAction("Index");
            }

            else
            {
                categoryExtraVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                });
                categoryExtraVM.ExtraNameList = _unitOfWork.CategoryExtra.GetAll().Select(u => new SelectListItem
                {
                    Text = u.ExtraName,
                    Value = u.Id.ToString()
                });
                return View(categoryExtraVM);
            }



        }

        public IActionResult Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var extra = _unitOfWork.CategoryExtra.GetFirstOrDefault(m => m.ExtraName == id, includeProperties: "Category");
            if (extra == null)
            {
                return NotFound();
            }

            var selectedCategories = _unitOfWork.CategoryExtra.GetAll()
                .Where(cm => cm.ExtraName == id)
                .Select(cm => cm.CategoryId)
                .ToList();

            CategoryExtraVM categoryExtraVM = new()
            {
                CategoryExtra = extra,
                CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                }),
                ExtraNameList = _unitOfWork.CategoryExtra.GetAll().Select(u => new SelectListItem
                {
                    Text = u.ExtraName,
                    Value = u.Id.ToString()
                }),
                IsCategorySelected = _unitOfWork.Category.GetAll().Select(u => selectedCategories.Contains(u.Id)).ToList()
            };

            return View(categoryExtraVM);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(CategoryExtraVM categoryExtraVM)
        {
            if (ModelState.IsValid)
            {
                categoryExtraVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                });

                var extra = _unitOfWork.CategoryExtra.GetFirstOrDefault(m => m.ExtraName == categoryExtraVM.CategoryExtra.ExtraName, includeProperties: "Category");
                if (extra == null)
                {
                    return NotFound();
                }

                for (int i = 0; i < categoryExtraVM.CategoryList.Count(); i++)
                {
                    var category = categoryExtraVM.CategoryList.ElementAt(i);
                    var selectedCategoryId = int.Parse(category.Value);
                    var existingExtra = _unitOfWork.CategoryExtra.GetFirstOrDefault(m => m.ExtraName == categoryExtraVM.CategoryExtra.ExtraName && m.CategoryId == selectedCategoryId);

                    if (categoryExtraVM.IsCategorySelected[i])
                    {
                        if (existingExtra == null)
                        {
                            CategoryExtra newExtra = new()
                            {
                                ExtraName = categoryExtraVM.CategoryExtra.ExtraName,
                                Price = categoryExtraVM.CategoryExtra.Price,
                                CategoryId = selectedCategoryId
                            };
                            _unitOfWork.CategoryExtra.Add(newExtra);
                        }
                    }
                    else
                    {
                        if (existingExtra != null)
                        {
                            _unitOfWork.CategoryExtra.Remove(existingExtra);
                        }
                    }
                }

                _unitOfWork.Save();
                return RedirectToAction("Index");
            }

            categoryExtraVM.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
            {
                Text = u.Name,
                Value = u.Id.ToString()
            });

            categoryExtraVM.ExtraNameList = _unitOfWork.CategoryExtra.GetAll().Select(u => new SelectListItem
            {
                Text = u.ExtraName,
                Value = u.Id.ToString()
            });

            return View(categoryExtraVM);
        }




    }
}