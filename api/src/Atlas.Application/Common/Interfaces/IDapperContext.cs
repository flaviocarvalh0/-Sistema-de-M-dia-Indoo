using System.Data;

namespace Atlas.Application.Common.Interfaces;

public interface IDapperContext
{
    IDbConnection CreateConnection();
}
