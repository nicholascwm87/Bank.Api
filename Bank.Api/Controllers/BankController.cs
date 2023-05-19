using Bank.Data.Entities;
using Bank.Data.Interface;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Bank.Api.Controllers
{
    [ApiController]
    [Route("api/bank")]
    //[Authorize]
    public class BankController : ControllerBase
    {
        readonly ICustomerAccountRepository _customerAccountRepository;
        readonly IHttpContextAccessor _context;

        public BankController(ICustomerAccountRepository customerAccount, IHttpContextAccessor context)
        {
            _customerAccountRepository = customerAccount;
            _context = context;
        }


        /// <summary>
        /// To Get Customer Transaction
        /// </summary>
        /// <param name="pageNo">default to 1</param>
        /// <param name="pageSize">default to 30</param>
        /// <returns></returns>
        [HttpGet, Route("CustomerTransaction")]
        [ProducesResponseType(typeof(CustomerAccount), (int)HttpStatusCode.OK)]
        public IActionResult GetCustomerTransaction([FromQuery] int pageNo = 1, [FromQuery] int pageSize = 30)
        {
            // To extract to unique key
            //var userInfo = Helper.GetUserClaim(_context.HttpContext.User);

            var result = _customerAccountRepository.GetCustomAccountsTestData(1234);

            if (result != null && result.Transactions != null)
                result.Transactions = result.Transactions.Skip((pageNo - 1) * pageSize).Take(pageSize).ToList();
            else
                return NotFound();

            return Ok(result);
        }
    }
}
