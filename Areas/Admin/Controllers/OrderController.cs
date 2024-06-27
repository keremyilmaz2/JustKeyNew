using JustKeyNew.DataAccess.Repository;
using JustKeyNew.DataAccess.Repository.IRepository;
using JustKeyNew.Models;
using JustKeyNew.Models.ViewModels;
using JustKeyNew.Utility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Collections.Generic;
using JustKeyNew.DataAccess.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Stripe;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace JustKeyNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class OrderController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderDetailRepository _orderDetailRepository;
        private readonly DbContextOptions<ApplicationDbContext> _options;
        [BindProperty]
        public OrderVM OrderVM { get; set; }

        public OrderController(IUnitOfWork unitOfWork, IOrderDetailRepository orderDetailRepository, DbContextOptions<ApplicationDbContext> options)
        {
            _unitOfWork = unitOfWork;
            _orderDetailRepository = orderDetailRepository;
            _options = options;
        }

        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Details(int orderId)
        {
            OrderVM = new()
            {
                OrderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderId),
                OrderDetail = _unitOfWork.OrderDetail.GetAll(u => u.OrderHeaderId == orderId, includeProperties: "Product,DetailExtras")

            };
            return View(OrderVM);
        }

        public IActionResult EndOfDay()
        {
            DateTime dateTime = DateTime.Today;
            var objOrderHeaderList = _unitOfWork.OrderHeader.GetAll(u => u.OrderDate.Date == dateTime).ToList();
            var orderVmList = new List<OrderVM>();

            var productCountDict = new Dictionary<string, int>();

            foreach (var orderHeader in objOrderHeaderList)
            {
                int orderHeaderId = orderHeader.Id;
                var orderDetails = _unitOfWork.OrderDetail.GetAll(u => u.OrderHeaderId == orderHeaderId, includeProperties: "Product,DetailExtras").ToList();

                foreach (var detail in orderDetails)
                {
                    var productName = detail.Product.Title;
                    if (productCountDict.ContainsKey(productName))
                    {
                        productCountDict[productName] += detail.Count;
                    }
                    else
                    {
                        productCountDict[productName] = detail.Count;
                    }
                }

                OrderVM orderVM = new()
                {
                    OrderHeader = orderHeader,
                    OrderDetail = orderDetails,
                    ProductList = new Dictionary<string, int>(productCountDict)
                };

                orderVmList.Add(orderVM);
            }

            // To pass the product count dictionary separately to the view, add it to the ViewBag
            ViewBag.ProductCountDict = productCountDict;

            return View(orderVmList);
        }



        [HttpPost]
        public IActionResult UpdateOrderDetail()
        {
            var orderHeaderFromDb = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);
            orderHeaderFromDb.Name = OrderVM.OrderHeader.Name;
            orderHeaderFromDb.PhoneNumber = OrderVM.OrderHeader.PhoneNumber;
            orderHeaderFromDb.TableNo = OrderVM.OrderHeader.TableNo;
            
            _unitOfWork.OrderHeader.Update(orderHeaderFromDb);
            _unitOfWork.Save();
            TempData["Success"] = "Order Details Updated Successfully";

            return RedirectToAction(nameof(Details), new { orderId = orderHeaderFromDb.Id });
        }
        public IActionResult PendingToCash(int id)
        {
            OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == id);
            orderHeader.OrderStatus = SD.StatusCash;
            orderHeader.PaymentStatus = SD.PaymentStatusApproved;
            _unitOfWork.OrderHeader.Update(orderHeader);
            _unitOfWork.Save();
            return RedirectToAction("Index");
        }

        public IActionResult OrderDelivered(int id)
        {
            OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == id);
            orderHeader.OrderDelivered = SD.StatusShipped;
            _unitOfWork.OrderHeader.Update(orderHeader);

            var orderDetails = _orderDetailRepository.GetAllIncludingProductMaterials(
               u => u.OrderHeaderId == id
           ).ToList();

            foreach (OrderDetail detail in orderDetails)
            {
                ReduceStock(detail);
            }

            _unitOfWork.Save();
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult CancelOrder()
        {
            var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);

            if (orderHeader.PaymentStatus == SD.PaymentStatusApproved && orderHeader.OrderStatus == SD.StatusCreditCart)
            {
                var options = new RefundCreateOptions
                {
                    Reason = RefundReasons.RequestedByCustomer,
                    PaymentIntent = orderHeader.PaymentIntentId,
                };

                var service = new RefundService();
                Refund refund = service.Create(options);

                _unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusRefunded);
            }
            else
            {
                _unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusCancelled);
            }
            _unitOfWork.Save();
            TempData["Success"] = "Order Cancelled Successfully.";


            return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
        }

        #region API CALLS
        [HttpGet]
        public IActionResult GetAll(string status)
        {
            IEnumerable<OrderHeader> objOrderHeaders;

            objOrderHeaders = _unitOfWork.OrderHeader.GetAll();

            switch (status)
            {
                case "pending":
                    objOrderHeaders = objOrderHeaders.Where(u => u.PaymentStatus == SD.PaymentStatusDelayedPayment);
                    break;
                case "cash":
                    objOrderHeaders = objOrderHeaders.Where(u => u.OrderStatus == SD.StatusCash);
                    break;
                case "completed":
                    objOrderHeaders = objOrderHeaders.Where(u => u.OrderDelivered == SD.StatusShipped);
                    break;
                case "creditCart":
                    objOrderHeaders = objOrderHeaders.Where(u => u.OrderStatus == SD.StatusCreditCart);
                    break;
                default:
                    break;
            }

            return Json(new { data = objOrderHeaders });
        }
        #endregion

        private void ReduceStock(OrderDetail orderDetail)
        {
            var productMaterials = orderDetail.Product.ProductMaterials;
            foreach (var material in productMaterials)
            {
                using (var newContext = new ApplicationDbContext(_options))
                {
                    var stock = newContext.Stocks.FirstOrDefault(u => u.Name == material.MaterialName);
                    if (stock != null)
                    {
                        stock.TotalAmount -= material.Amount;
                        newContext.Stocks.Update(stock);
                        newContext.SaveChanges();
                    }
                }
            }
        }
    }
}