using System;
using System.Collections.Generic;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Developer;

public sealed class UuidToolkitService : IUuidToolkitService
{
    public IReadOnlyList<string> Generate(UuidOptions options)
    {
        int count = Math.Clamp(options.Count, 1, 100);
        var list = new List<string>(count);

        for (int i = 0; i < count; i++)
        {
            var guid = Guid.NewGuid();
            string formatted;

            if (options.Braces)
            {
                formatted = guid.ToString("B"); // {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}
            }
            else if (!options.Hyphens)
            {
                formatted = guid.ToString("N"); // xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
            }
            else
            {
                formatted = guid.ToString("D"); // xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
            }

            if (options.Uppercase)
            {
                formatted = formatted.ToUpperInvariant();
            }
            else
            {
                formatted = formatted.ToLowerInvariant();
            }

            list.Add(formatted);
        }

        return list;
    }
}
