using MediaCred.Models;
using Microsoft.Extensions.Configuration;

namespace MediaCred
{
    public class Helper
    {
        private IConfiguration _configuration;
        public Helper(IConfiguration configuration)
        {
            _configuration= configuration;
        }
        public DateTime GetNextNudge(User u)
        {
            // Calculate the number of days to add to DateTime.Now
            int daysToAdd = Convert.ToInt32(Math.Round((double)u.PoliticalBias / _configuration.GetValue<int>("NudgeStrength") * (_configuration.GetValue<int>("MaxNudgeDays") - _configuration.GetValue<int>("MinNudgeDays"))));

            // Ensure that the number of days to add is within the range of 1 to 100
            daysToAdd = Math.Max(_configuration.GetValue<int>("MinNudgeDays"), Math.Min(daysToAdd, _configuration.GetValue<int>("MaxNudgeDays")));

            // Calculate the nudged date
            DateTime nudgedDate = DateTime.Now.AddDays(daysToAdd);

            return nudgedDate;
        }
    }
}
