using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace TKW.Framework.Domain.WebApi
{
    public class DynamicWebApiControllerFeatureProvider(ISelectController selectController) : ControllerFeatureProvider
    {
        protected override bool IsController(TypeInfo typeInfo)
        {
            return selectController.IsController(typeInfo);
        }
    }
}