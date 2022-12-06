using System.Collections.Generic;
using System.Threading.Tasks;
using Certera.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Certera.Web.Pages.Keys
{
    public class IndexModel : PageModel
    {
        private readonly Data.DataContext _context;

        public IndexModel(Data.DataContext context)
        {
            _context = context;
        }

        public IList<Key> Key { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task OnGetAsync() => Key = await _context.Keys.ToListAsync();
    }
}