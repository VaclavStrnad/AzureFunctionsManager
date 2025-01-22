using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionApp
{
    public class AnotherJob
    {
        private readonly ILogger _logger;

        public AnotherJob(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ShortRunningPeriodicJob>();
        }

        [Function("AnotherJob")]
        public void Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)  // "0 */5 * * * *" - every 5 hours
        {
            _logger.LogInformation($"{nameof(ShortRunningPeriodicJob)} function executed at: {DateTime.Now}");

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
