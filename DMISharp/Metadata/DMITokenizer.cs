using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Toolkit.HighPerformance.Enumerables;
using Microsoft.Toolkit.HighPerformance;

namespace DMISharp.Metadata;

/// <summary>
/// Ref struct for tokenizing a DMI's metadata text into key and value pairs using ReadOnlySpans
/// </summary>
public ref struct DMITokenizer
{
    private ReadOnlySpanTokenizer<char> _tokenizer;

    /// <summary>
    /// Instantiate a new DMITokenizer to tokenize a provided ReadOnlySpan
    /// </summary>
    /// <param name="data">The span to tokenize into key, value pairs</param>
    public DMITokenizer(ReadOnlySpan<char> data)
    {
        _tokenizer = data.Tokenize('\n');
        CurrentKey = ReadOnlySpan<char>.Empty;
        CurrentValue = ReadOnlySpan<char>.Empty;
    }

    /// <summary>
    /// Attempts to get the next key, value pair.
    /// </summary>
    /// <returns>True if an additional pair is found, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        while (_tokenizer.MoveNext())
        {
            var line = _tokenizer.Current;

            // Skip comments
            if (line.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                continue;

            // Strip any whitespace
            var trimmedLine = line.Trim("\n\t ");

            // Skip any lines without assignment
            var equalsIndex = trimmedLine.IndexOf("=", StringComparison.OrdinalIgnoreCase);
            if (equalsIndex == -1)
                continue;

            // Extract key and value
            CurrentKey = trimmedLine[..(equalsIndex - 1)].TrimEnd(" ");
            CurrentValue = trimmedLine[(equalsIndex + 1)..].Trim(" ");

            // Trip any wrapping quotes if value is a string
            CurrentValue = CurrentValue.Trim("\"");

            return true;
        }

        return false;
    }

    /// <summary>
    /// The current token key
    /// </summary>
    private ReadOnlySpan<char> CurrentKey
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        set;
    }

    /// <summary>
    /// The current token value
    /// </summary>
    public ReadOnlySpan<char> CurrentValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        private set;
    }

    /// <summary>
    /// Determines if the current key token is equal to some value
    /// </summary>
    /// <param name="value">The value to compare against</param>
    /// <returns>If the two values are equal</returns>
    public readonly bool KeyEquals(ReadOnlySpan<char> value) => CurrentKey.Equals(value, StringComparison.OrdinalIgnoreCase);


    /// <summary>
    /// Attempts to return the current value token as an integer.
    /// </summary>
    /// <returns>The current value token as an integer</returns>
    public readonly int ValueAsInt()
    {
        var currentValue = CurrentValue;

        try
        {
            return int.Parse(currentValue, provider: CultureInfo.InvariantCulture);
        }
        catch (FormatException e)
        {
            throw new FormatException(
                $"Failed to parse int {CurrentKey.ToString()} from line: \"{CurrentValue.ToString()}\"", e);
        }
    }

    /// <summary>
    /// Attempts to return the current value token as a double.
    /// </summary>
    /// <returns>The current value token as a double</returns>
    public readonly double ValueAsDouble()
    {
        var currentValue = CurrentValue;

        try
        {
            return double.Parse(currentValue, provider: CultureInfo.InvariantCulture);
        }
        catch (FormatException e)
        {
            throw new FormatException(
                $"Failed to parse double {CurrentKey.ToString()} from line: \"{CurrentValue.ToString()}\"", e);
        }
    }

    /// <summary>
    /// Attempts to return the current value token as a bool.
    /// </summary>
    /// <returns>The current value token as a bool</returns>
    public readonly bool ValueAsBool() => ValueAsInt() == 1;

    /// <summary>
    /// Attempts to return the current value token as an array of doubles.
    /// </summary>
    /// <returns>The current value token as an array of doubles</returns>
    public readonly double[] ValueAsDoubleArray()
    {
        try
        {
            var toReturn = new double[CurrentValue.Count(',') + 1];
            var span = new Span<double>(toReturn);
            var currIdx = 0;
            foreach (var token in CurrentValue.Tokenize(','))
            {
                span[currIdx++] = double.Parse(token, provider: CultureInfo.InvariantCulture);
            }

            return toReturn;
        }
        catch (FormatException e)
        {
            throw new FormatException(
                $"Failed to parse double array {CurrentKey.ToString()} from line: \"{CurrentValue.ToString()}\"",
                e);
        }
    }

    /// <summary>
    /// Attempts to return the current value token as an array of ints.
    /// </summary>
    /// <returns>The current value token as an array of ints</returns>
    public readonly int[] ValueAsIntArray()
    {
        try
        {
            var toReturn = new int[CurrentValue.Count(',') + 1];
            var span = new Span<int>(toReturn);
            var currIdx = 0;
            foreach (var token in CurrentValue.Tokenize(','))
            {
                span[currIdx++] = int.Parse(token, provider: CultureInfo.InvariantCulture);
            }

            return toReturn;
        }
        catch (FormatException e)
        {
            throw new FormatException(
                $"Failed to parse int array {CurrentKey.ToString()} from line: \"{CurrentValue.ToString()}\"", e);
        }
    }
}