using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Certera.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Certera.Web.Pages.Notifications
{
    public class IndexModel : PageModel
    {
        private readonly Data.DataContext _context;

        public IndexModel(Data.DataContext context)
        {
            _context = context;
        }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public NotificationSetting NotificationSetting { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            NotificationSetting = await _context.NotificationSettings
                .Include(x => x.ApplicationUser)
                .FirstOrDefaultAsync(m => m.ApplicationUserId == userId);

            if (NotificationSetting == null)
            {
                NotificationSetting = new NotificationSetting
                {
                    ApplicationUserId = userId
                };
                _context.NotificationSettings.Add(NotificationSetting);
                await _context.SaveChangesAsync();
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(NotificationSetting).State = EntityState.Modified;

            NotificationSetting.ApplicationUserId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            try
            {
                await _context.SaveChangesAsync();
                StatusMessage = "Notification settings updated";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!NotificationSettingExists(NotificationSetting.NotificationSettingId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool NotificationSettingExists(long id) => _context.NotificationSettings.Any(e => e.NotificationSettingId == id);
    }
}