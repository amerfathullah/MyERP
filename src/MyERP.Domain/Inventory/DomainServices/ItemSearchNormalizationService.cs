using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Normalizes and folds text for Item search indexing and candidate query matching.
/// Per ERPNext PR #65020 (commits 77c5fd1722, 88fabb805e, 459174fa30) and PR #65117 (commit 6ad4805b44):
/// Folds what MariaDB utf8mb4_unicode_ci and SQLite/PostgreSQL search treat as equivalent:
/// accents, kana voicing marks, character width, small and full-size kana, katakana with hiragana,
/// and digits of any script.
/// </summary>
public class ItemSearchNormalizationService : DomainService
{
    private static readonly Dictionary<char, char> SmallKanaToFullSize = new()
    {
        ['ぁ'] = 'あ', ['ぃ'] = 'い', ['ぅ'] = 'う', ['ぇ'] = 'え', ['ぉ'] = 'お',
        ['っ'] = 'つ', ['ゃ'] = 'や', ['ゅ'] = 'ゆ', ['ょ'] = 'よ', ['ゎ'] = 'わ',
        ['ゕ'] = 'か', ['ゖ'] = 'け', ['ㇰ'] = 'く', ['ㇱ'] = 'し', ['ㇲ'] = 'す',
        ['ㇳ'] = 'と', ['ㇴ'] = 'ぬ', ['ㇵ'] = 'は', ['ㇶ'] = 'ひ', ['ㇷ'] = 'ふ',
        ['ㇸ'] = 'へ', ['ㇹ'] = 'ほ', ['ㇺ'] = 'む', ['ㇻ'] = 'ら', ['ㇼ'] = 'り',
        ['ㇽ'] = 'る', ['ㇾ'] = 'れ', ['ㇿ'] = 'ろ'
    };

    /// <summary>
    /// Folds accents, kana voicing marks, character width, small and full-size kana, katakana with hiragana,
    /// and digits of any script.
    /// Per ERPNext PR #65020 / commit 77c5fd1722 and PR #65117 / commit 6ad4805b44 (fold_text).
    /// </summary>
    public static string FoldText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // 1) NFKD normalization decomposes accents and folds character widths (full-width -> standard, half-width kana -> kana)
        var normalized = text.Normalize(NormalizationForm.FormKD);

        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            // 2) Skip combining marks (accents, voicing marks)
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            var ch = c;

            // 3) Katakana to Hiragana (0x30A1 'ァ' through 0x30F6 'ヶ' maps to ch - 0x60)
            if (ch >= 0x30A1 && ch <= 0x30F6)
            {
                ch = (char)(ch - 0x60);
            }

            // 4) Small kana to full-size kana
            if (SmallKanaToFullSize.TryGetValue(ch, out var fullSize))
            {
                ch = fullSize;
            }

            // 5) Fold digits of any script (PR #65117 / commit 6ad4805b44)
            ch = FoldDigit(ch);

            sb.Append(ch);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Folds digits of any script into standard ASCII '0'..'9'.
    /// Per ERPNext PR #65117 (commit 6ad4805b44 / fold_digit).
    /// </summary>
    public static char FoldDigit(char c)
    {
        var digit = CharUnicodeInfo.GetDigitValue(c);
        if (digit >= 0 && digit <= 9)
        {
            return (char)('0' + digit);
        }

        var dec = CharUnicodeInfo.GetDecimalDigitValue(c);
        if (dec >= 0 && dec <= 9)
        {
            return (char)('0' + dec);
        }

        return c;
    }
}
