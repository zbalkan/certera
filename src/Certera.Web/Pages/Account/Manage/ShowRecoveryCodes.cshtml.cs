using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Certera.Web.Pages.Account.Manage
{
    public class ShowRecoveryCodesModel : PageModel
    {
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not
        ///     intended to be used directly from your code. This API may change or be removed in
        ///     future releases.
        /// </summary>
        [TempData]
        public IList<string> RecoveryCodes { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not
        ///     intended to be used directly from your code. This API may change or be removed in
        ///     future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not
        ///     intended to be used directly from your code. This API may change or be removed in
        ///     future releases.
        /// </summary>
        public IActionResult OnGet() => RecoveryCodes == null || RecoveryCodes.Count == 0 ? RedirectToPage("./TwoFactorAuthentication") : Page();
    }
}