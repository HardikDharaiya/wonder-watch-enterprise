using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using WonderWatch.Application.Interfaces;

namespace WonderWatch.Web.Jobs
{
    [DisallowConcurrentExecution]
    public class InventoryAlertJob : IJob
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<InventoryAlertJob> _logger;

        public InventoryAlertJob(IServiceProvider serviceProvider, ILogger<InventoryAlertJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("InventoryAlertJob executing at {Time}", DateTime.UtcNow);

            using var scope = _serviceProvider.CreateScope();
            var adminService = scope.ServiceProvider.GetRequiredService<IAdminService>();
            
            var alerts = await adminService.GetInventoryAlertsAsync();
            if (alerts.Any())
            {
                _logger.LogWarning("INVENTORY ALERT: {Count} watches are running extremely low on stock!", alerts.Count);
                foreach (var watch in alerts)
                {
                    _logger.LogWarning("- {Brand} {Name} (Ref: {Reference}): Only {Stock} left.", watch.Brand, watch.Name, watch.ReferenceNumber, watch.StockQuantity);
                }
            }
            else
            {
                _logger.LogInformation("Inventory levels are healthy.");
            }
        }
    }
}
