﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Certera.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Certera.Web.Pages.Certificates
{
    public class IndexModel : PageModel
    {
        private readonly Data.DataContext _context;

        public IndexModel(Data.DataContext context)
        {
            _context = context;
        }

        public IList<AcmeCertificate> AcmeCertificate { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task OnGetAsync() => AcmeCertificate = await _context.AcmeCertificates
                .Include(a => a.AcmeAccount)
                .Include(a => a.Key).ToListAsync();
    }
}