using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace MyERP.Data;

/* This is used if database provider does't define
 * IMyERPDbSchemaMigrator implementation.
 */
public class NullMyERPDbSchemaMigrator : IMyERPDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
