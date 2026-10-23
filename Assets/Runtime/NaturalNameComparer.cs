using System;
using System.Collections.Generic;

public sealed class NaturalNameComparer : IComparer<string>
{
    public static readonly NaturalNameComparer Instance = new NaturalNameComparer();

    public int Compare(string left, string right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left == null) return -1;
        if (right == null) return 1;
        int a = 0, b = 0;
        while (a < left.Length && b < right.Length)
        {
            if (IsDigit(left[a]) && IsDigit(right[b]))
            {
                int startA = a, startB = b;
                while (a < left.Length && IsDigit(left[a])) a++;
                while (b < right.Length && IsDigit(right[b])) b++;
                int significantA = startA, significantB = startB;
                while (significantA < a && left[significantA] == '0') significantA++;
                while (significantB < b && right[significantB] == '0') significantB++;
                int comparison = (a - significantA).CompareTo(b - significantB);
                if (comparison != 0) return comparison;
                comparison = string.CompareOrdinal(left, significantA, right, significantB, a - significantA);
                if (comparison != 0) return comparison;
                comparison = (a - startA).CompareTo(b - startB);
                if (comparison != 0) return comparison;
            }
            else
            {
                int comparison = char.ToUpperInvariant(left[a]).CompareTo(char.ToUpperInvariant(right[b]));
                if (comparison != 0) return comparison;
                a++;
                b++;
            }
        }

        int remaining = (left.Length - a).CompareTo(right.Length - b);
        return remaining != 0 ? remaining : StringComparer.Ordinal.Compare(left, right);
    }

    private static bool IsDigit(char value) => value >= '0' && value <= '9';
}
