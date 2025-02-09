using System;
using System.Collections.Generic;
using DomainTest.Managers;
using DomainTest.Models;
using TKW.Framework.Common.Entity.Exceptions;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain;

namespace DomainTest.Services;

public class DepartmentService : DomainService
{
    private readonly DomainTestDataAccessHelper _DaHelper;
    private readonly DepartmentManager _DepartmentManager;

    public DepartmentService(DomainUser domainUser,
        DomainTestDataAccessHelper daHelper, DepartmentManager departmentManager) : base(domainUser)
    {
        _DepartmentManager = departmentManager.AssertNotNull(nameof(departmentManager));
        _DaHelper = daHelper.AssertNotNull(nameof(daHelper));
    }
    
    public List<Department> ListAllDepartments()
    {
        return _DaHelper.DepartmentRepository.WhereAsync(null).Result;
    }

    public List<Department> ListRootDepartments()
    {
        return _DaHelper.DepartmentRepository.WhereAsync(d => d.IsRoot).Result;
    }

    public List<Department> ListSubDepartments(Guid parentDeptGuid)
    {
        return _DaHelper.DepartmentRepository.WhereAsync(d => d.ParentDeptUid == parentDeptGuid).Result;
    }

    public Department CreateRootDepartment(string name)
    {
        name.EnsureHasValue(nameof(name));
        var department = new Department()
        {
            Name = name,
            Uid = Guid.NewGuid(),
            IsRoot = true,
            ParentDeptUid = Guid.Empty,
        };
        var result = _DaHelper.DepartmentRepository.CreateAsync(department).Result;
        return result;
    }

    public Department CreateSubDepartment(Guid parentDeptUid, string deptName)
    {
        deptName.EnsureHasValue(nameof(deptName));
        var department = new Department()
        {
            Name = deptName,
            Uid = Guid.NewGuid(),
            IsRoot = true,
            ParentDeptUid = parentDeptUid,
        };
        //验证父级部门存在
        if (_DaHelper.DepartmentRepository.CountAsync(d => d.Uid == parentDeptUid).Result == 0)
            throw new EntityNotFoundException($"指定的上级部门不存在：{parentDeptUid}");

        var result = _DaHelper.DepartmentRepository.CreateAsync(department).Result;
        return result;
    }

    public IList<Department> ClearDepartments()
    {
        return _DepartmentManager.ClearDepartments();
    }
}