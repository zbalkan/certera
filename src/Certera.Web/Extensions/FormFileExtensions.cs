﻿using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Certera.Web.Extensions
{
    public static class FormFileExtensions
    {
        public static bool IsNullOrEmpty(this IFormFile file) => file == null || file.Length == 0;

        public static async Task<string> ReadAsStringAsync(this IFormFile file)
        {
            if (file.IsNullOrEmpty())
            {
                return null;
            }
            var result = new StringBuilder();
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                while (reader.Peek() >= 0)
                {
                    result.AppendLine(await reader.ReadLineAsync());
                }
            }
            return result.ToString();
        }

        public static async Task<byte[]> ReadAsBytesAsync(this IFormFile file)
        {
            if (file.IsNullOrEmpty())
            {
                return null;
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return ms.ToArray();
        }
    }
}