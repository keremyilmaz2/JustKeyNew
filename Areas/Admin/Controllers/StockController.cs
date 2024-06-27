
using JustKeyNew.DataAccess.Data;
using JustKeyNew.DataAccess.Repository.IRepository;
using JustKeyNew.Models;
using JustKeyNew.Utility;
using JustKeyNew.DataAccess.Repository.IRepository;
using JustKeyNew.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace JustKeyNew.Areas.Admin.Controllers
{

    [Area("Admin")]
    public class StockController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        
        public StockController(IUnitOfWork unitOfWork )
        {
            _unitOfWork = unitOfWork;
            
        }
        public IActionResult Index()
        {
            List<Stock> objStockList = _unitOfWork.Stock.GetAll().ToList();
            return View(objStockList);
        }
        
        public IActionResult Edit(int? id)
        {
            if (id == null || id == 0)
            {
                return NotFound();
            }
            Stock stockFromDb = _unitOfWork.Stock.Get(u => u.Id == id);
            if (stockFromDb == null)
            {
                return NotFound();
            }
            return View(stockFromDb);
        }
        [HttpPost]
        public IActionResult Edit(Stock obj)
        {
            if (ModelState.IsValid)
            {
              
                // Update Category
                _unitOfWork.Stock.Update(obj);
                _unitOfWork.Save();
                TempData["success"] = "Stock updated successfully.";
                return RedirectToAction("Index");
            }

            // If ModelState is not valid, return to the edit view with errors
            return View(obj);
        }
        
        
    }
}
