using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionApp
{
    public class ThirdJob
    {
        private readonly ILogger _logger;

        public ThirdJob(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ShortRunningPeriodicJob>();
        }

        [Function("ThirdJob")]
        public void Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)  // "0 */5 * * * *" - every 5 hours
        {
            _logger.LogInformation($"{nameof(ShortRunningPeriodicJob)} function executed at: {DateTime.Now}");

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}