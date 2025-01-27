using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionApp
{
    public class ShortRunningPeriodicJob
    {
        private readonly ILogger _logger;

        public ShortRunningPeriodicJob(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ShortRunningPeriodicJob>();
        }

        [Function("ShortRunningPeriodicJob")]
        public void Run([TimerTrigger("*/30 * * * * *")] TimerInfo myTimer)  // "0 */5 * * * *" - every 5 hours
        {
            _logger.LogInformation($"{nameof(ShortRunningPeriodicJob)} function executed at: {DateTime.Now} XXXXX");

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
