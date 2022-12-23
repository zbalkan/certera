using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Certera.Data;
using Certera.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Certera.Web.Pages.Settings
{
    public class IndexModel : PageModel
    {
        private readonly DataContext _dataContext;
        private readonly NotificationService _notificationService;

        public IndexModel(DataContext dataContext, NotificationService notificationService)
        {
            _dataContext = dataContext;
            _notificationService = notificationService;
        }

        [Range(10, 45, ErrorMessage = "Must be between 10 and 45 days")]
        [BindProperty]
        public int RenewCertificateDays { get; set; }

        [BindProperty]
        public string DnsScriptEnvironmentVariables { get; set; }

        [BindProperty]
        public string SetScript { get; set; }

        [BindProperty]
        public string SetScriptArguments { get; set; }

        [BindProperty]
        public string CleanupScript { get; set; }

        [BindProperty]
        public string CleanupScriptArguments { get; set; }

        [BindProperty]
        public string Recipients { get; set; }

        public string StatusMessage { get; set; }

        public IActionResult OnGet()
        {
            RenewCertificateDays = _dataContext.GetSetting(Data.Settings.RenewCertificateDays, 30);
            DnsScriptEnvironmentVariables = _dataContext.GetSetting<string>(Data.Settings.Dns01SetEnvironmentVariables, null);
            SetScript = _dataContext.GetSetting<string>(Data.Settings.Dns01SetScript, null);
            CleanupScript = _dataContext.GetSetting<string>(Data.Settings.Dns01CleanupScript, null);
            SetScriptArguments = _dataContext.GetSetting<string>(Data.Settings.Dns01SetScriptArguments, null);
            CleanupScriptArguments = _dataContext.GetSetting<string>(Data.Settings.Dns01CleanupScriptArguments, null);

            return Page();
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _dataContext.SetSetting(Data.Settings.RenewCertificateDays, RenewCertificateDays);
            _dataContext.SetSetting(Data.Settings.Dns01SetEnvironmentVariables, DnsScriptEnvironmentVariables);
            _dataContext.SetSetting(Data.Settings.Dns01SetScript, SetScript);
            _dataContext.SetSetting(Data.Settings.Dns01CleanupScript, CleanupScript);
            _dataContext.SetSetting(Data.Settings.Dns01SetScriptArguments, SetScriptArguments);
            _dataContext.SetSetting(Data.Settings.Dns01CleanupScriptArguments, CleanupScriptArguments);

            StatusMessage = "Settings saved";

            return Page();
        }

        public async Task<IActionResult> OnPostSendTestEmailAsync()
        {
            var recipients = new List<string>();
            if (!string.IsNullOrWhiteSpace(Recipients))
            {
                recipients.AddRange(Recipients
                    .Split(',', ';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()));

                await _notificationService.SendTestNotificationAsync(recipients).ConfigureAwait(false);

                StatusMessage = "Test email sent";
            }
            else
            {
                StatusMessage = "No recipient specified";
            }

            return Page();
        }
    }
}