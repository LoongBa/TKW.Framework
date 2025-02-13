using System;
using System.Collections.Generic;
using DomainTest.Managers;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interception.Filters;

namespace DomainTest.Services.Contracts
{
    [AuthorityActionFilter]
    public interface IUserControllerContract : IDomainControllerContract
    {
        User GetUserByUid(Guid uid);
        //[EntityHistoryActionFilter]
        User GetUserByUsername(string userName);
        List<User> SearchUsers(string nameKeyword);
        List<User> ListAllUsers1();
        [AllowAnonymous]
        List<User> ListAllUsers2();
        [AllowAnonymous]
        List<User> ListAllUsers3();
        void DelUserByUid(Guid uid);
        void DelUserByUsername(string username);
    }
}