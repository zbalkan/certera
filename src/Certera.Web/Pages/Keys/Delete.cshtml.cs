using System.Threading.Tasks;
using Certera.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Certera.Web.Pages.Keys
{
    public class DeleteModel : PageModel
    {
        private readonly Data.DataContext _context;

        public DeleteModel(Data.DataContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Key Key { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Key = await _context.Keys.FirstOrDefaultAsync(m => m.KeyId == id);

            return Key == null ? NotFound() : Page();
        }

        public async Task<IActionResult> OnPostAsync(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Key = await _context.Keys.FindAsync(id);

            if (Key != null)
            {
                _context.Keys.Remove(Key);
                try
                {
                    await _context.SaveChangesAsync();
                    StatusMessage = "Key deleted";
                }
                catch (DbUpdateException)
                {
                    StatusMessage = "Unable to delete key in use";
                    return Page();
                }
            }

            return RedirectToPage("./Index");
        }
    }
}