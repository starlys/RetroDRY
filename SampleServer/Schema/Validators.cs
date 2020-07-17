using RetroDRY;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#pragma warning disable IDE0060

namespace SampleServer.Schema
{
    public static class Validators
    {
        /// <summary>
        /// Sample validation function (registered in Startup)
        /// </summary>
        public static Task<IEnumerable<string>> ValidateCustomer(Daton daton, ViewonKey _, IUser user)
        {
            var cust = daton as Customer;
            var errors = new List<string>();

            //silly rule to demonstrate custom validation:
            if (cust.Company.StartsWith("The", StringComparison.InvariantCultureIgnoreCase))
                errors.Add("Companies cannot start with 'the' "); 
            //note that you can use multi language strings here by checking user.LangCode

            //This longer return expression is used when we are not doing async validation. For async validation,
            //make the validation method async and return errors directly.
            return Task.FromResult(errors as IEnumerable<string>); //sync return
            //return errors; //async return
        }

    }
}
