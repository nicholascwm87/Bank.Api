using Bank.Data.Context;
using Bank.Data.Entities;
using Bank.Data.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Bank.Data.Repositories
{
    public class CustomerAccountRepository : ICustomerAccountRepository
    {
        // Due to no db exist so this chunk of code need to be comment to run
        //BankDbContext _context;

        //public CustomerAccountRepository(BankDbContext context)
        //{
        //    _context = context;
        //}

        //public IQueryable<CustomerAccount> GetCustomerAccounts(int userId)
        //{
        //    var customerAccount = _context.CustomerAccount.Where(c => c.UserId == userId).Include(p => p.Transactions).AsNoTracking();

        //    return customerAccount;
        //}

        public CustomerAccount GetCustomAccountsTestData(int userId)
        {
            CustomerAccount result = new()
            {
                UserId = userId,
                CreatedById = 0,
                CreatedOn = DateTime.UtcNow,
                ModifiedById = 0,
                ModifiedOn = DateTime.UtcNow
            };

            List<Transaction> trans = new();
            string[] currency = { "USD", "MYR", "AUD", "JPN", "NZD" };
            string[] countryCode = { "US", "MY", "AU", "JP", "NZ" };

            for (int i = 0; i < 50; i++)
            {
                int number = RandomIntFromRange(0, 4);

                trans.Add(new Transaction
                {
                    Account = $"Account - {countryCode[number]}{RandomIntFromRange(0, 99).ToString().PadLeft(2, '0')}-{RandomIntFromRange(0, 9999).ToString().PadLeft(4, '0')}-{RandomIntFromRange(0, 9999).ToString().PadLeft(4, '0')}-{RandomIntFromRange(0, 9999).ToString().PadLeft(4, '0')}-{RandomIntFromRange(0, 9)}",
                    Amount = $"{currency[number]} {RandomDoubleFromRange(100.00, 10000.00)}",
                    Description = "",
                    TransactionDate = DateTime.UtcNow.AddDays(-1),
                    TransactionId = Guid.NewGuid(),
                    UserId = userId
                });
            }

            result.Transactions = trans.OrderByDescending(o => o.TransactionDate).ToList();

            return result;
        }

        private double RandomDoubleFromRange(double min, double max)
        {
            Random random = new Random();
            return (max - min) + min * random.NextDouble();
        }

        private int RandomIntFromRange(int min, int max)
        {
            Random random = new Random();
            return random.Next(min, max);

        }

    }
}
