using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Certera.Data.Migrations
{
    public partial class SetApiKeyValues : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            using var ctx = new DataContext();
            foreach (var cert in ctx.AcmeCertificates.Where(x => x.ApiKey1 == null).ToList())
            {
                cert.ApiKey1 = ApiKeyGenerator.CreateApiKey();
                cert.ApiKey2 = ApiKeyGenerator.CreateApiKey();
            }

            foreach (var key in ctx.Keys.Where(x => x.ApiKey1 == null).ToList())
            {
                key.ApiKey1 = ApiKeyGenerator.CreateApiKey();
                key.ApiKey2 = ApiKeyGenerator.CreateApiKey();
            }
            ctx.SaveChanges();
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}