using System.Threading.Tasks;

namespace MyERP.Data;

public interface IMyERPDbSchemaMigrator
{
    Task MigrateAsync();
}
