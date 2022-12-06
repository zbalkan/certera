using System.Collections.Generic;
using Certera.Data.Models;
using Microsoft.AspNetCore.Identity;

namespace Certera.Data
{
    public partial class ApplicationUser : IdentityUser<long>
    {
        public NotificationSetting NotificationSetting { get; set; }

        public virtual ICollection<UserConfiguration> UserConfigurations { get; set; } = new List<UserConfiguration>();
    }
}