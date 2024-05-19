using JustKeyNew.DataAccess.Repository.IRepository;
using JustKeyNew.Models;
using JustKeyNew.Models.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace JustKeyNew.Controllers
{
    public class MaterialController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public MaterialController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;  
        }
        public IActionResult Index()
        {
            List<CategoryMaterial> objCategoryMaterialList = _unitOfWork.CategoryMaterial.GetAll(includeProperties: "Category").ToList();
            
            return View(objCategoryMaterialList);
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
                
                _unitOfWork.CategoryMaterial.Add(categoryMaterialVM.CategoryMaterial);
                _unitOfWork.Save();
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
        
        public IActionResult Delete(int? id)
        {
            CategoryMaterial? obj = _unitOfWork.CategoryMaterial.Get(u => u.Id == id);
            if (obj == null)
            {
                return NotFound();
            }
            _unitOfWork.CategoryMaterial.Remove(obj);
            _unitOfWork.Save();
            TempData["success"] = "CategoryMaterial deleted succesfully.";
            return RedirectToAction("Index");
        }
        

    }
}
