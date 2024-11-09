using System.Runtime.CompilerServices;
using System.Text;

namespace io.github.azukimochi;

internal static class StringExt
{
    public static bool Contains(this string[] strings, string value, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
    {
        foreach (string s in strings)
        {
            if (s.Equals(value, comparisonType))
                return true;
        }
        return false;
    }

    public static int IndexOf(this string[] strings, string value, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
    {
        for (int i = 0; i < strings.Length; i++)
        {
            string s = strings[i];

            if (s.Equals(value, comparisonType))
                return i;
        }
        return -1;
    }

    public static int SplitAny(this ReadOnlySpan<char> span, Span<Range> ranges, ReadOnlySpan<char> values)
    {
        int start = 0;
        int count = 0;
        int idx;
        while ((idx = span[start..].IndexOfAny(values)) != -1)
        {
            if (ranges.Length + 1 < count)
                break;
            ranges[count++] = start..(start + idx);
            start += idx + 1;
        }
        ranges[count++] = start..;
        return count;
    }

    public static string Create(StringBuilder sb, [InterpolatedStringHandlerArgument("sb")] ref StringBuilderInterpolatedStringHandler handler)
    {
        return handler.ToStringAndClear();
    }

    public static string ToKebabCase(this string str)
    {
        static char ToLower(char c) => (char)(c | 0x20);
        static bool IsUpper(char c) => (c & 0x20) == 0;

        if (str is null)
            return null;
        if (str.Length == 0)
            return str;

        StringBuilder sb = new();
        sb.Append(ToLower(str[0]));

        foreach(var x in str.AsSpan(1))
        {
            if (IsUpper(x))
            {
                sb.Append("-");
            }
            sb.Append(ToLower(x));
        }
        return sb.ToString();
    }
}
