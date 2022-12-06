using System.Collections.Generic;
using System.Threading.Tasks;
using Certera.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Certera.Web.Pages.Acme.Accounts
{
    public class IndexModel : PageModel
    {
        private readonly Data.DataContext _context;

        public IndexModel(Data.DataContext context)
        {
            _context = context;
        }

        public IList<AcmeAccount> AcmeAccount { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task OnGetAsync() => AcmeAccount = await _context.AcmeAccounts
                .Include(a => a.ApplicationUser)
                .Include(a => a.Key).ToListAsync();
    }
}