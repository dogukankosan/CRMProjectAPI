using Microsoft.Data.SqlClient;
using System.Data;

namespace CRMProjectAPI.Data
{
    public class DapperContext
    {
        private readonly string _connectionString;
        public DapperContext(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string bulunamadı!");
        }
        public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
    }
}