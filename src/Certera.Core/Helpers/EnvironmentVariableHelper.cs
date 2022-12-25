using System;
using System.Collections.Generic;

namespace Certera.Core.Helpers
{
    public static class EnvironmentVariableHelper
    {
        public static IDictionary<string, string> ToKeyValuePair(string envVars)
        {
            var result = new Dictionary<string, string>(1);
            if (!string.IsNullOrWhiteSpace(envVars))
            {
                foreach (var line in envVars.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split("=", 2);
                    if (parts.Length > 0)
                    {
                        var envKey = parts[0];
                        string value = null;
                        if (parts.Length >= 1)
                        {
                            value = parts[1];
                        }

                        result.Add(envKey, value!);
                    }
                }
            }
            return result;
        }
    }
}