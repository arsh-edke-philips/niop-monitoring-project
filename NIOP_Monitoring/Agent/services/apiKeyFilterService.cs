using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.VisualBasic;


namespace NIOP_Monitoring.Agent
{
    public class ApiKeyFilter(IConfiguration config, ILogger<ApiKeyFilter> logger) : IAuthorizationFilter
    {
        public const string HeaderName = "X-Agent-Key";
        
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var expected = config["Agent:ApiKey"];
            if (string.IsNullOrWhiteSpace(expected))
            {
                logger.LogError("Agent:ApiKey is not configured. All /api/agent requests are refused.");
                context.Result = new ObjectResult(new { error = "Agent API is not configured." }) { StatusCode = 503 };
                return;
            }

            var supplied = context.HttpContext.Request.Headers[HeaderName].ToString();
            if (!KeysMatch(supplied, expected))
            {
                logger.LogWarning("Rejected /api/agent call from {Ip}: missing or wrong key.",
                    context.HttpContext.Connection.RemoteIpAddress);
                context.Result = new UnauthorizedObjectResult(new { error = "Missing or invalid X-Agent-Key." });
            }
        }

        private static bool KeysMatch(string supplied, string expected)
        {
            var a = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
            var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
    }
}