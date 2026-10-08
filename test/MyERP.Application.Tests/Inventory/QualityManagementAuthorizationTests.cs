using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;

namespace MyERP.Inventory;

public class QualityManagementAuthorizationTests
{
    [Fact]
    public void QualityManagementAppService_ClassHasAuthorizeAttribute()
    {
        var classAuth = typeof(QualityManagementAppService).GetCustomAttribute<AuthorizeAttribute>();
        classAuth.ShouldNotBeNull();
    }

    [Fact]
    public void QualityManagementAppService_AllPublicInterfaceMethods_HaveGranularAuthorizeAttributeWithPolicy()
    {
        var appServiceType = typeof(QualityManagementAppService);
        var interfaceType = typeof(IQualityManagementAppService);
        var interfaceMethods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        interfaceMethods.Length.ShouldBeGreaterThan(0);

        foreach (var ifaceMethod in interfaceMethods)
        {
            var implMethod = appServiceType.GetMethod(
                ifaceMethod.Name,
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: ifaceMethod.GetParameters().Select(p => p.ParameterType).ToArray(),
                modifiers: null);

            implMethod.ShouldNotBeNull($"Method {ifaceMethod.Name} should be implemented on QualityManagementAppService");

            var authAttr = implMethod.GetCustomAttribute<AuthorizeAttribute>();
            authAttr.ShouldNotBeNull($"Method {ifaceMethod.Name} on QualityManagementAppService must have [Authorize] attribute");
            authAttr.Policy.ShouldNotBeNullOrWhiteSpace($"Method {ifaceMethod.Name} must have a non-empty Policy on its [Authorize] attribute");
            authAttr.Policy.ShouldStartWith("MyERP.");
        }
    }
}
