﻿using System;
using System.Collections.Generic;
using System.Linq;
using Certera.Core.Extensions;
using Certera.Data.Models;
using Certera.Data.Views;
using Microsoft.EntityFrameworkCore;

namespace Certera.Data
{
    public partial class DataContext
    {
        public DbSet<AcmeAccount> AcmeAccounts { get; set; }
        public DbSet<AcmeCertificate> AcmeCertificates { get; set; }
        public DbSet<AcmeOrder> AcmeOrders { get; set; }
        public DbSet<AcmeRequest> AcmeRequests { get; set; }
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
        public DbSet<Domain> Domains { get; set; }
        public DbSet<DomainScan> DomainScans { get; set; }
        public DbSet<DomainCertificate> DomainCertificates { get; set; }
        public DbSet<DomainCertificateChangeEvent> DomainCertificateChangeEvents { get; set; }
        public DbSet<Key> Keys { get; set; }
        public DbSet<KeyHistory> KeyHistories { get; set; }
        public DbSet<NotificationSetting> NotificationSettings { get; set; }
        public DbSet<Setting> Settings { get; set; }
        public DbSet<UserConfiguration> UserConfigurations { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }

        private static void ConfigureDataModels(ModelBuilder builder)
        {
            builder.Entity<AcmeAccount>()
                .HasOne(x => x.Key)
                .WithMany(x => x.AcmeAccounts)
                .HasForeignKey(x => x.KeyId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<AcmeAccount>()
                .Property(x => x.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Entity<AcmeCertificate>()
                .Property(x => x.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Entity<AcmeCertificate>()
                .HasIndex(x => new { x.Name, x.AcmeAccountId })
                .IsUnique();
            builder.Entity<AcmeCertificate>()
                .HasIndex(x => new { x.Subject, x.AcmeAccountId });
            builder.Entity<AcmeCertificate>()
                .HasOne(x => x.AcmeAccount)
                .WithMany(x => x.AcmeCertificates)
                .HasForeignKey(x => x.AcmeAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<AcmeCertificate>()
                .HasOne(x => x.Key)
                .WithMany(x => x.AcmeCertificates)
                .HasForeignKey(x => x.KeyId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<AcmeCertificate>()
                .HasIndex(x => x.ApiKey1)
                .IsUnique();
            builder.Entity<AcmeCertificate>()
                .HasIndex(x => x.ApiKey2)
                .IsUnique();

            builder.Entity<AcmeRequest>()
                .Property(x => x.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Entity<AcmeRequest>()
                .HasIndex(x => x.Token)
                .IsUnique();

            builder.Entity<AcmeOrder>()
                .Property(x => x.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Entity<Domain>()
                .Property(x => x.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Entity<Domain>()
                .HasIndex(x => x.Uri)
                .IsUnique();
            builder.Entity<Domain>()
                .HasIndex(x => x.DateLastScanned);
            builder.Entity<Domain>()
                .HasMany(x => x.DomainScans)
                .WithOne(x => x.Domain)
                .HasForeignKey(x => x.DomainId);

            builder.Entity<DomainCertificate>()
                .Property(x => x.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Entity<DomainCertificateChangeEvent>()
                .Property(x => x.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Entity<DomainScan>()
                .HasOne(x => x.Domain)
                .WithMany(x => x.DomainScans)
                .HasForeignKey(x => x.DomainId);
            builder.Entity<DomainScan>()
                .Property(x => x.DateScan)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Entity<Key>()
                .Property(x => x.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Entity<Key>()
                .HasIndex(x => x.Name)
                .IsUnique();
            builder.Entity<Key>()
                .Property(x => x.RawData)
                .IsRequired();
            builder.Entity<Key>()
                .HasIndex(x => x.ApiKey1)
                .IsUnique();
            builder.Entity<Key>()
                .HasIndex(x => x.ApiKey2)
                .IsUnique();

            builder.Entity<KeyHistory>()
                .Property(x => x.DateOperation)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Entity<UserNotification>()
                .Property(x => x.DateCreated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Entity<UserNotification>()
                .HasIndex(x => x.NotificationEvent);
        }

        public IEnumerable<TrackedCertificate> GetTrackedCertificates()
        {
            var domainsView = GetDomains()
                .Select(TrackedCertificate.FromDomain);

            var uploadedView = DomainCertificates
                .Where(x => x.CertificateSource == CertificateSource.Uploaded)
                .Select(TrackedCertificate.FromDomainCertificate);

            var domainsAndUploadedCerts = domainsView.Union(uploadedView).ToList();
            if (domainsAndUploadedCerts == null)
            {
                return new List<TrackedCertificate>();
            }
            // Get all ACME certificates that we've issued and attempt to match or add as new
            var acmeCertsView = GetAcmeCertificates()
                .Select(TrackedCertificate.FromAcmeCertificate)
                .Where(x => x != null)
                .ToList();

            var acmeCertDict = acmeCertsView.Where(x => x.Thumbprint != null)
                .GroupBy(x => x.Thumbprint)
                .ToDictionary(x => x.Key, x => x.First());
            var toRemove = new HashSet<string>();

            foreach (var cert in domainsAndUploadedCerts)
            {
                if (string.IsNullOrWhiteSpace(cert.Thumbprint))
                {
                    continue;
                }
                if (acmeCertDict.TryGetValue(cert.Thumbprint, out var value))
                {
                    cert.AcmeCertType = value.AcmeCertType;
                    toRemove.Add(cert.Thumbprint);
                }
            }
            acmeCertsView.RemoveAll(x => toRemove.Contains(x.Thumbprint));
            return domainsAndUploadedCerts.Union(acmeCertsView);
        }

        #region Certs

        public IList<AcmeCertificate> GetAcmeCertificates()
        {
            var certs = AcmeCertQuery().ToList();
            if (!certs.IsNullOrEmpty())
            {
                var orders = AcmeCertOrderQuery()
                    .Where(x => x.LatestValidAcmeOrder != null)
                    .ToDictionary(x => x.LatestValidAcmeOrder.AcmeCertificateId, x => x);
                foreach (var cert in certs)
                {
                    if (orders.TryGetValue(cert.AcmeCertificateId, out var value))
                    {
                        cert.LatestValidAcmeOrder = value.LatestValidAcmeOrder;
                    }
                }
            }

            return certs;
        }

        public AcmeCertificate GetAcmeCertificate(string host, bool? staging = null)
        {
            var query = AcmeCertQuery();

            if (staging != null)
            {
                query = query.Where(x => x.AcmeAccount.IsAcmeStaging == staging.Value);
            }

            var cert = query.FirstOrDefault(x => x.Subject == host);

            if (cert != null)
            {
                var order = AcmeCertOrderQuery()
                    .Where(x => x.LatestValidAcmeOrder.AcmeCertificateId == cert.AcmeCertificateId)
                    .FirstOrDefault();
                cert.LatestValidAcmeOrder = order?.LatestValidAcmeOrder;
            }
            return cert;
        }

        public IQueryable<AcmeCertificate> AcmeCertQuery() => AcmeCertificates
                .Include(x => x.Key)
                .Include(x => x.AcmeAccount)
                    .ThenInclude(x => x.Key);

        public IQueryable<AcmeCertOrderContainer> AcmeCertOrderQuery() => AcmeCertificates
                .Include(x => x.AcmeOrders)
                    .ThenInclude(x => x.DomainCertificate)
                .Select(x => new AcmeCertOrderContainer
                {
                    LatestValidAcmeOrder = x.AcmeOrders
                            .OrderByDescending(y => y.DateCreated)
                            .FirstOrDefault(y => y.Status == AcmeOrderStatus.Completed)
                });

        #endregion Certs

        #region Domains

        public Domain GetDomain(long id)
        {
            var domain = Domains.FirstOrDefault(x => x.DomainId == id);
            if (domain != null)
            {
                var scans = DomainScansQuery(timeAgo: null, id).FirstOrDefault();
                domain.LatestDomainScan = scans?.LatestDomainScan;
                domain.LatestValidDomainScan = scans?.LatestValidDomainScan;
            }

            return domain;
        }

        public IList<Domain> GetDomains(long[] ids = null)
        {
            var domainQuery = Domains.AsQueryable();
            if (ids != null)
            {
                domainQuery = domainQuery.Where(x => ids.Contains(x.DomainId));
            }
            var domains = domainQuery
                .ToList();
            if (!domains.IsNullOrEmpty())
            {
                var domainScans = DomainScansQuery()
                    .Where(x => x.LatestDomainScan != null)
                    .ToDictionary(x => x.LatestDomainScan.DomainId, x => x);
                foreach (var domain in domains)
                {
                    if (domainScans.TryGetValue(domain.DomainId, out var value))
                    {
                        domain.LatestDomainScan = value.LatestDomainScan;
                        domain.LatestValidDomainScan = value.LatestValidDomainScan;
                    }
                }
            }

            return domains;
        }

        public IList<Domain> GetDomainsNeedingScan(DateTime? timeAgo = null)
        {
            var domains = Domains
                .Where(x => x.DateLastScanned == null || x.DateLastScanned < timeAgo)
                .ToList();
            if (!domains.IsNullOrEmpty())
            {
                var domainScans = DomainScansQuery(timeAgo)
                    .Where(x => x.LatestDomainScan != null)
                    .ToDictionary(x => x.LatestDomainScan.DomainId, x => x);
                foreach (var domain in domains)
                {
                    if (domainScans.TryGetValue(domain.DomainId, out var value))
                    {
                        domain.LatestDomainScan = value.LatestDomainScan;
                        domain.LatestValidDomainScan = value.LatestValidDomainScan;
                    }
                }
            }

            return domains;
        }

        private IQueryable<DomainScanContainer> DomainScansQuery(DateTime? timeAgo = null, params long[] ids)
        {
            var query = Domains
                .Include(x => x.DomainScans)
                .ThenInclude(x => x.DomainCertificate)
                .AsQueryable();

            if (!ids.IsNullOrEmpty())
            {
                query = query.Where(x => ids.Contains(x.DomainId));
            }

            if (timeAgo != null)
            {
                query = query.Where(x => x.DateLastScanned == null || x.DateLastScanned < timeAgo);
            }

            return query.Select(x => new DomainScanContainer
            {
                LatestDomainScan = x.DomainScans
                        .OrderByDescending(x => x.DateScan)
                        .FirstOrDefault(),
                LatestValidDomainScan = x.DomainScans
                        .Where(x => x.ScanSuccess)
                        .OrderByDescending(x => x.DateScan)
                        .FirstOrDefault()
            });
        }

        public IList<DomainCertificate> GetUnsentCertChangeNotifications()
        {
            // Get all of the domains that have at least 2 scans and the certificates are different
            // and the latest one's thumbprint isn'converted in the UserNotifications table

            var domainsWithScans = Domains
                .Include(x => x.DomainScans)
                .ThenInclude(x => x.DomainCertificate)
                .Select(x => new {
                    LatestDomainScan = x.DomainScans
                        .OrderByDescending(x => x.DateScan)
                        .Take(1),
                    PreviousDomainScan = x.DomainScans
                        .OrderByDescending(x => x.DateScan)
                        .Skip(1)
                        .Take(1),
                    Notifications = x.DomainScans
                        .Join(UserNotifications,
                            s => s.DomainCertificate.Thumbprint,
                            n => n.DomainCertificate.Thumbprint,
                            (_, notif) => notif)
                })
                .ToList();

            return domainsWithScans
                .ConvertAll(x => x.LatestDomainScan.FirstOrDefault()?.DomainCertificate)
;
        }

        #endregion Domains

        public T GetSetting<T>(Settings setting, T @default)
        {
            var settingRecord = Settings.FirstOrDefault(x => x.Name == setting.ToString());

            if (settingRecord == null)
            {
                Settings.Add(new Setting
                {
                    Name = setting.ToString(),
                    Value = @default?.ToString()
                });

                SaveChanges();

                return @default;
            }
            else
            {
                var converted = Convert.ChangeType(settingRecord?.Value, typeof(T));
                return converted is null ? default : (T)converted;
            }
        }

        public void SetSetting<T>(Settings setting, T value)
        {
            var settingRecord = Settings.FirstOrDefault(x => x.Name == setting.ToString());

            // Add, if null
            if (settingRecord == null)
            {
                Settings.Add(new Setting
                {
                    Name = setting.ToString(),
                    Value = value?.ToString()
                });
            }
            // Update, if exists
            else
            {
                settingRecord.Value = value?.ToString();
            }

            SaveChanges();
        }

        public DnsSettingsContainer GetDnsSettings()
        {
            var settings = Settings.ToDictionary(x => x.Name, x => x.Value);

            settings.TryGetValue(nameof(Data.Settings.Dns01SetEnvironmentVariables), out var envVars);
            settings.TryGetValue(nameof(Data.Settings.Dns01SetScript), out var setScript);
            settings.TryGetValue(nameof(Data.Settings.Dns01CleanupScript), out var cleanupScript);
            settings.TryGetValue(nameof(Data.Settings.Dns01SetScriptArguments), out var setScriptArgs);
            settings.TryGetValue(nameof(Data.Settings.Dns01CleanupScriptArguments), out var cleanupScriptArgs);

            return new DnsSettingsContainer
            {
                DnsEnvironmentVariables = envVars,
                DnsSetupScript = setScript,
                DnsCleanupScript = cleanupScript,
                DnsSetupScriptArguments = setScriptArgs,
                DnsCleanupScriptArguments = cleanupScriptArgs
            };
        }
    }

    public enum Settings
    {
        RenewCertificateDays = 0,
        Dns01SetEnvironmentVariables = 1,
        Dns01SetScript = 2,
        Dns01CleanupScript = 3,
        Dns01SetScriptArguments = 4,
        Dns01CleanupScriptArguments = 5
    }

    public class AcmeCertOrderContainer
    {
        public AcmeOrder LatestValidAcmeOrder { get; set; }
    }

    public class DomainScanContainer
    {
        public DomainScan LatestDomainScan { get; set; }
        public DomainScan LatestValidDomainScan { get; set; }
    }
}