using Microsoft.AspNetCore.Mvc.Filters;

using NLog;

using System.Net;

namespace Bank.Api.Core
{
    public class ExceptionHandlingFilter : ExceptionFilterAttribute
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static DateTime? LastLogged502 = null;
        private static DateTime? LastLogged503 = null;
        private static DateTime? LastLogged504 = null;
        static int LastLoggedIntervalTime = 5;

        public override void OnException(ExceptionContext actionExecutedContext)
        {
            //base.OnException(actionExecutedContext);
            bool requireLog = true;
            DateTime now = DateTime.UtcNow;


            int responseStatusCode = actionExecutedContext.HttpContext.Response.StatusCode;

            if (responseStatusCode == (int)HttpStatusCode.BadGateway || responseStatusCode == (int)HttpStatusCode.ServiceUnavailable || responseStatusCode == (int)HttpStatusCode.GatewayTimeout)
            {
                switch (responseStatusCode)
                {
                    case (int)HttpStatusCode.BadGateway:
                        if (LastLogged502.HasValue && now < LastLogged502.Value.AddMinutes(LastLoggedIntervalTime)) requireLog = false;
                        else LastLogged502 = now;
                        break;
                    case (int)HttpStatusCode.ServiceUnavailable:
                        if (LastLogged503.HasValue && now < LastLogged503.Value.AddMinutes(LastLoggedIntervalTime)) requireLog = false;
                        else LastLogged503 = now;
                        break;
                    case (int)HttpStatusCode.GatewayTimeout:
                        if (LastLogged504.HasValue && now < LastLogged504.Value.AddMinutes(LastLoggedIntervalTime)) requireLog = false;
                        else LastLogged504 = now;
                        break;
                }
            }

            //to handle unexpected exception logging to error file
            if (requireLog)
                Logger.Error(actionExecutedContext.Exception);
        }
    }
}
