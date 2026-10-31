using System;
using System.Text;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Developer;

public sealed class EncodingToolkitService : IEncodingToolkitService
{
    public EncodingResult Base64Encode(string input)
    {
        if (input == null) return new EncodingResult { Success = false, ErrorMessage = "Input cannot be null." };
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            string base64 = Convert.ToBase64String(bytes);
            return new EncodingResult { Success = true, OutputText = base64 };
        }
        catch (Exception ex)
        {
            return new EncodingResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public EncodingResult Base64Decode(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new EncodingResult { Success = false, ErrorMessage = "Input cannot be empty." };
        try
        {
            string clean = input.Trim();
            byte[] bytes = Convert.FromBase64String(clean);
            string decoded = Encoding.UTF8.GetString(bytes);
            return new EncodingResult { Success = true, OutputText = decoded };
        }
        catch (FormatException ex)
        {
            return new EncodingResult { Success = false, ErrorMessage = $"Invalid Base64 format: {ex.Message}" };
        }
        catch (Exception ex)
        {
            return new EncodingResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public EncodingResult UrlEncode(string input)
    {
        if (input == null) return new EncodingResult { Success = false, ErrorMessage = "Input cannot be null." };
        try
        {
            string encoded = Uri.EscapeDataString(input);
            return new EncodingResult { Success = true, OutputText = encoded };
        }
        catch (Exception ex)
        {
            return new EncodingResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public EncodingResult UrlDecode(string input)
    {
        if (input == null) return new EncodingResult { Success = false, ErrorMessage = "Input cannot be null." };
        try
        {
            string decoded = Uri.UnescapeDataString(input);
            return new EncodingResult { Success = true, OutputText = decoded };
        }
        catch (Exception ex)
        {
            return new EncodingResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public EncodingResult ConvertEncoding(string input, string fromEncoding, string toEncoding)
    {
        if (input == null) return new EncodingResult { Success = false, ErrorMessage = "Input cannot be null." };

        try
        {
            Encoding src = GetEncodingByName(fromEncoding);
            Encoding dst = GetEncodingByName(toEncoding);

            byte[] srcBytes = src.GetBytes(input);
            byte[] dstBytes = Encoding.Convert(src, dst, srcBytes);
            string result = dst.GetString(dstBytes);

            return new EncodingResult { Success = true, OutputText = result };
        }
        catch (Exception ex)
        {
            return new EncodingResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static Encoding GetEncodingByName(string name) => name.ToUpperInvariant() switch
    {
        "UTF-8" or "UTF8" => Encoding.UTF8,
        "UTF-16" or "UTF16" or "UNICODE" => Encoding.Unicode,
        "ASCII" => Encoding.ASCII,
        "LATIN1" or "ISO-8859-1" => Encoding.Latin1,
        _ => Encoding.UTF8
    };
}

