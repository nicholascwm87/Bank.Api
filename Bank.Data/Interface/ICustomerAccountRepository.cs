using Bank.Data.Entities;

namespace Bank.Data.Interface
{
    public interface ICustomerAccountRepository
    {
        //IQueryable<CustomerAccount> GetCustomerAccounts(int userId);

        CustomerAccount GetCustomAccountsTestData(int userId);
    }
}
