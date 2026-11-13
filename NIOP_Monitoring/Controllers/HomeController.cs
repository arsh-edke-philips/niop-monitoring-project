using Microsoft.AspNetCore.Mvc;
using NIOP_Monitoring.Models;
using NIOP_Monitoring.Services;
using System.Diagnostics;
using System.Text.Json;

namespace NIOP_Monitoring.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly OrderSummaryService _orderSummaryService;
        private readonly OrderService _orderService;
        private readonly DistributionCenterDeviceService _distributionCenterDeviceService;
        private readonly DeviceService _deviceService;
        private readonly DeviceLineageService _deviceLineageService;
        private readonly OrderLineageService _orderLineageService;
        private readonly EnrollmentLineageService _enrollmentLineageService;
        private readonly ReportLineageService _reportLineageService;
        private readonly PreBuiltKitService _preBuiltKitService;
        private readonly TransferLineageService _transferLineageService;

        public HomeController(ILogger<HomeController> logger, 
            OrderSummaryService service, 
            OrderService orderService, 
            DistributionCenterDeviceService distributionCenterDeviceService, 
            DeviceService deviceService,
            DeviceLineageService deviceLineageService,
            OrderLineageService orderLineageService,
            EnrollmentLineageService enrollmentLineageService,
            ReportLineageService reportLineageService,
            PreBuiltKitService preBuiltKitService,
            TransferLineageService transferLineageService)
        {
            
            _orderSummaryService = service;
            _orderService = orderService;
            _reportLineageService = reportLineageService;
            _transferLineageService = transferLineageService;
            _preBuiltKitService = preBuiltKitService;
            _distributionCenterDeviceService = distributionCenterDeviceService;
            _logger = logger;
            _deviceService = deviceService;
            _orderLineageService = orderLineageService;
            _deviceLineageService = deviceLineageService;
            _enrollmentLineageService = enrollmentLineageService;
        }

        public IActionResult Index(CancellationToken ct)
        {
            var model = new OperationsDashboardViewModel
            {
                Summary = _orderSummaryService.GetOrderSummary().Result,
                Orders = _orderService.GetOrderService().Result,
                DeviceCounts = _distributionCenterDeviceService.GetDeviceCountAsync().Result,
                LocationDeviceCounts = _distributionCenterDeviceService.GetLocationDeviceCountPerCenterAsync().Result,
                Devices = _deviceService.GetDeviceMetrics().Result,
                DevicesList = _deviceLineageService.GetDeviceLineage().Result,
                OrdersList = _orderLineageService.GetOrderLineagePath().Result,
                EnrollmentsList = _enrollmentLineageService.GetEnrollmentLineage(ct).Result,
                ReportsList = _reportLineageService.GetReportLineage().Result,
                PreBuiltKitPaths = _preBuiltKitService.GetPreBuiltKitsLineage().Result,
                TransferPaths = _transferLineageService.GetTransferLineage().Result
            };
            return View(model);
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
