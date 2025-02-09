using System.Collections.Generic;
using DomainTest.Models;
using TKW.Framework.Domain;

namespace DomainTest.Managers
{
    public class DepartmentManager : AbstractDomainManager<DomainTestDataAccessHelper>
    {
        public DepartmentManager(DomainTestDataAccessHelper dbDataAccessHelper) : base(dbDataAccessHelper)
        {
        }

        public List<Department> ClearDepartments()
        {
            return DaHelper.DepartmentRepository.RemoveAsync(null).Result;
        }
    }
}
