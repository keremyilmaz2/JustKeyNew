using JustKeyNew.DataAccess.Repository.IRepository;
using JustKeyNew.Utility;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace JustKeyNew.ViewComponents
{
    public class ShoppingCartViewComponent : ViewComponent
    {
        private readonly IUnitOfWork _unitOfWork;
        public ShoppingCartViewComponent(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var sessionId = HttpContext.Session.Id;

            if (sessionId != null)
            {
                if (HttpContext.Session.GetInt32(SD.SessionCart)==null)
                {
                    HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart.GetAll(u => u.SessionId == sessionId).Count());
                }
                return View(HttpContext.Session.GetInt32(SD.SessionCart));
            }
            else
            {
                HttpContext.Session.Clear();
                return View(0);
            }
        }

    }
}
