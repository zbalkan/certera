﻿using System;
using System.Linq;
using Certera.Data;
using Certera.Data.Models;
using Certera.Web.AcmeProviders;
using Certes;

namespace Certera.Web.Services
{
    public class KeyGenerator
    {
        private readonly DataContext _dataContext;
        private readonly CertesAcmeProvider _certesAcmeProvider;

        public KeyGenerator(DataContext dataContext, CertesAcmeProvider certesAcmeProvider)
        {
            _dataContext = dataContext;
            _certesAcmeProvider = certesAcmeProvider;
        }

        public Key Generate(string name, KeyAlgorithm keyAlgorithm = KeyAlgorithm.RS256,
            string description = null, string keyContents = null)
        {
            if (_dataContext.Keys.Any(x => x.Name == name))
            {
                name = $"{name}-{DateTime.Now:yyyyMMddHHmmss}";
            }

            keyContents ??= _certesAcmeProvider.NewKey(keyAlgorithm);

            var key = new Key
            {
                DateCreated = DateTime.UtcNow,
                Name = name,
                Description = description,
                RawData = keyContents,
                ApiKey1 = ApiKeyGenerator.CreateApiKey(),
                ApiKey2 = ApiKeyGenerator.CreateApiKey()
            };

            _dataContext.Keys.Add(key);
            _dataContext.SaveChanges();

            return key;
        }
    }
}