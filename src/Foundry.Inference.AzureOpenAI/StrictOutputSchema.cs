// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Foundry.Inference.AzureOpenAI;

/// <summary>
/// A deliberately bounded JSON Schema subset for provider generation and
/// independent response validation. Unsupported keywords fail closed instead
/// of being sent to the provider and silently ignored by the engine.
/// </summary>
internal sealed class StrictOutputSchema : IDisposable
{
    internal const int MaximumSchemaBytes = 256 * 1024;
    internal const int MaximumProviderNestingLevels = 5;
    internal const int MaximumProviderObjectProperties = 100;

    // Conservative cross-deployment compatibility ceilings. Azure support can
    // lag the broader OpenAI service, so every registered schema must fit these
    // bounds locally before the provider acquires a token or creates a request.
    internal const int MaximumProviderEnumValues = 500;
    internal const int MaximumProviderStringCharacters = 15_000;
    internal const int LargeStringEnumValueThreshold = 250;
    internal const int MaximumLargeStringEnumCharacters = 7_500;
    private const int MaximumNumericLiteralCharacters = 4_096;
    private const int MaximumResponseDepth = 16;

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = MaximumResponseDepth,
    };

    private static readonly HashSet<string> AnnotationKeywords = new(StringComparer.Ordinal)
    {
        "description",
    };

    private static readonly HashSet<string> LocalValidationKeywords = new(StringComparer.Ordinal)
    {
        "minItems", "maxItems", "minLength", "maxLength",
        "minimum", "maximum", "minProperties", "maxProperties",
    };

    private readonly JsonDocument _document;

    private StrictOutputSchema(JsonDocument document)
    {
        _document = document;
    }

    public JsonElement Root => _document.RootElement;

    public static bool TryCreate(string? schemaJson, out StrictOutputSchema schema)
    {
        schema = null!;
        if (string.IsNullOrWhiteSpace(schemaJson)
            || Encoding.UTF8.GetByteCount(schemaJson) > MaximumSchemaBytes)
        {
            return false;
        }

        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(schemaJson, JsonOptions);
            var propertyCount = 0;
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !HasValidDecodedStrings(document.RootElement)
                || HasDuplicatePropertyNames(document.RootElement)
                || !IsWithinProviderBudgets(document.RootElement)
                || !IsSupportedSchema(document.RootElement, nestingLevel: 1, ref propertyCount)
                || document.RootElement.GetProperty("type").GetString() != "object")
            {
                document.Dispose();
                return false;
            }

            schema = new StrictOutputSchema(document);
            return true;
        }
        catch (JsonException)
        {
            document?.Dispose();
            return false;
        }
        catch (InvalidOperationException)
        {
            // System.Text.Json defers decoding some escaped strings until they
            // are read or written. A malformed surrogate must remain a local
            // capability refusal, never reach token acquisition or body
            // serialization.
            document?.Dispose();
            return false;
        }
        catch (ArgumentException)
        {
            // Parsing a .NET string that already contains an unpaired UTF-16
            // surrogate fails during UTF-8 transcoding. Registry content is
            // untrusted capability data, so keep that failure local too.
            document?.Dispose();
            return false;
        }
    }

    public bool Matches(string structuredJson)
    {
        if (string.IsNullOrWhiteSpace(structuredJson))
        {
            return false;
        }

        try
        {
            using var instance = JsonDocument.Parse(structuredJson, JsonOptions);
            return !HasDuplicatePropertyNames(instance.RootElement)
                && Matches(instance.RootElement, Root, depth: 0);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public void Dispose() => _document.Dispose();

    /// <summary>
    /// Writes the Azure-supported generation schema. The registry document may
    /// carry deterministic local bounds (for example minItems); those are
    /// deliberately omitted from the provider request and still enforced by
    /// <see cref="Matches(string)"/> after the response returns.
    /// </summary>
    public void WriteProviderSchema(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        WriteProviderSchema(writer, Root);
    }

    private static void WriteProviderSchema(Utf8JsonWriter writer, JsonElement schema)
    {
        writer.WriteStartObject();
        foreach (var keyword in schema.EnumerateObject())
        {
            if (LocalValidationKeywords.Contains(keyword.Name))
            {
                continue;
            }

            writer.WritePropertyName(keyword.Name);
            if (keyword.NameEquals("properties"))
            {
                writer.WriteStartObject();
                foreach (var property in keyword.Value.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteProviderSchema(writer, property.Value);
                }

                writer.WriteEndObject();
            }
            else if (keyword.NameEquals("items"))
            {
                WriteProviderSchema(writer, keyword.Value);
            }
            else
            {
                keyword.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static bool IsSupportedSchema(
        JsonElement schema,
        int nestingLevel,
        ref int propertyCount)
    {
        if (nestingLevel > MaximumProviderNestingLevels
            || schema.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("type", out var typeElement)
            || typeElement.ValueKind != JsonValueKind.String
            || typeElement.GetString() is not { } type
            || type is not ("object" or "array" or "string" or "integer" or "number" or "boolean"))
        {
            return false;
        }

        foreach (var property in schema.EnumerateObject())
        {
            if (!IsAllowedKeyword(type, property.Name)
                || property.NameEquals("description")
                    && property.Value.ValueKind != JsonValueKind.String)
            {
                return false;
            }
        }

        if (!ValidateEnum(schema, type))
        {
            return false;
        }

        return type switch
        {
            "object" => IsSupportedObjectSchema(schema, nestingLevel, ref propertyCount),
            "array" => IsSupportedArraySchema(schema, nestingLevel, ref propertyCount),
            "string" => HasOrderedNonNegativeBounds(schema, "minLength", "maxLength"),
            "integer" or "number" => HasOrderedNumericBounds(schema),
            "boolean" => true,
            _ => false,
        };
    }

    private static bool IsAllowedKeyword(string type, string keyword)
    {
        if (keyword == "type" || keyword == "enum"
            || AnnotationKeywords.Contains(keyword))
        {
            return true;
        }

        return type switch
        {
            "object" => keyword is "properties" or "required" or "additionalProperties"
                or "minProperties" or "maxProperties",
            "array" => keyword is "items" or "minItems" or "maxItems",
            "string" => keyword is "minLength" or "maxLength",
            "integer" or "number" => keyword is "minimum" or "maximum",
            _ => false,
        };
    }

    private static bool IsSupportedObjectSchema(
        JsonElement schema,
        int nestingLevel,
        ref int propertyCount)
    {
        if (!schema.TryGetProperty("properties", out var properties)
            || properties.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("required", out var required)
            || required.ValueKind != JsonValueKind.Array
            || !schema.TryGetProperty("additionalProperties", out var additional)
            || additional.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || additional.GetBoolean()
            || !HasOrderedNonNegativeBounds(schema, "minProperties", "maxProperties"))
        {
            return false;
        }

        var propertyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in properties.EnumerateObject())
        {
            propertyCount++;
            if (!propertyNames.Add(property.Name)
                || propertyCount > MaximumProviderObjectProperties
                || !IsSupportedSchema(property.Value, nestingLevel + 1, ref propertyCount))
            {
                return false;
            }
        }

        var requiredNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in required.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String
                || item.GetString() is not { } name
                || !propertyNames.Contains(name)
                || !requiredNames.Add(name))
            {
                return false;
            }
        }

        // Azure strict structured output requires every declared property to be
        // required. Preserve that rule locally instead of advertising a schema
        // the provider may reinterpret.
        return requiredNames.SetEquals(propertyNames);
    }

    private static bool IsSupportedArraySchema(
        JsonElement schema,
        int nestingLevel,
        ref int propertyCount)
    {
        return schema.TryGetProperty("items", out var items)
            && IsSupportedSchema(items, nestingLevel + 1, ref propertyCount)
            && HasOrderedNonNegativeBounds(schema, "minItems", "maxItems");
    }

    private static bool ValidateEnum(JsonElement schema, string type)
    {
        if (schema.TryGetProperty("enum", out var values))
        {
            if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() == 0)
            {
                return false;
            }

            var seen = new List<JsonElement>();
            foreach (var value in values.EnumerateArray())
            {
                if (!MatchesDeclaredType(value, type)
                    || seen.Any(previous => JsonSemanticallyEquals(previous, value)))
                {
                    return false;
                }

                seen.Add(value);
            }
        }

        return true;
    }

    private static bool IsWithinProviderBudgets(JsonElement root)
    {
        var enumValueCount = 0;
        var aggregateStringCharacters = 0;
        return AccumulateProviderBudgets(
            root,
            ref enumValueCount,
            ref aggregateStringCharacters);
    }

    private static bool AccumulateProviderBudgets(
        JsonElement element,
        ref int enumValueCount,
        ref int aggregateStringCharacters)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (!AccumulateProviderBudgets(
                        item,
                        ref enumValueCount,
                        ref aggregateStringCharacters))
                {
                    return false;
                }
            }

            return true;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if ((property.NameEquals("properties")
                    || property.NameEquals("$defs")
                    || property.NameEquals("definitions"))
                && property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var namedSchema in property.Value.EnumerateObject())
                {
                    if (!TryAddProviderCharacters(
                            namedSchema.Name.Length,
                            ref aggregateStringCharacters))
                    {
                        return false;
                    }
                }
            }

            if (property.NameEquals("enum") && property.Value.ValueKind == JsonValueKind.Array)
            {
                var stringEnumValueCount = 0;
                var stringEnumCharacters = 0;
                foreach (var value in property.Value.EnumerateArray())
                {
                    enumValueCount++;
                    if (enumValueCount > MaximumProviderEnumValues)
                    {
                        return false;
                    }

                    if (value.ValueKind == JsonValueKind.String)
                    {
                        stringEnumValueCount++;
                        // UTF-16 code units conservatively overcount supplementary
                        // characters rather than permitting a provider-limit overrun.
                        var characterCount = value.GetString()!.Length;
                        stringEnumCharacters += characterCount;
                        if (!TryAddProviderCharacters(
                                characterCount,
                                ref aggregateStringCharacters))
                        {
                            return false;
                        }
                    }
                }

                if (stringEnumValueCount > LargeStringEnumValueThreshold
                    && stringEnumCharacters > MaximumLargeStringEnumCharacters)
                {
                    return false;
                }
            }
            else if (property.NameEquals("const")
                && property.Value.ValueKind == JsonValueKind.String
                && !TryAddProviderCharacters(
                    property.Value.GetString()!.Length,
                    ref aggregateStringCharacters))
            {
                return false;
            }

            if (!AccumulateProviderBudgets(
                    property.Value,
                    ref enumValueCount,
                    ref aggregateStringCharacters))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryAddProviderCharacters(
        int characters,
        ref int aggregateStringCharacters)
    {
        aggregateStringCharacters += characters;
        return aggregateStringCharacters <= MaximumProviderStringCharacters;
    }

    private static bool HasOrderedNonNegativeBounds(JsonElement schema, string minimumName, string maximumName)
    {
        if (!TryReadNonNegativeInteger(schema, minimumName, out var minimum)
            || !TryReadNonNegativeInteger(schema, maximumName, out var maximum))
        {
            return false;
        }

        return minimum is null || maximum is null || minimum <= maximum;
    }

    private static bool TryReadNonNegativeInteger(JsonElement schema, string name, out int? value)
    {
        value = null;
        if (!schema.TryGetProperty(name, out var element))
        {
            return true;
        }

        if (!ExactJsonNumber.TryParse(element, out var exact)
            || !exact.TryGetInt32(out var parsed)
            || parsed < 0)
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool HasOrderedNumericBounds(JsonElement schema)
    {
        if (!TryReadDecimal(schema, "minimum", out var minimum)
            || !TryReadDecimal(schema, "maximum", out var maximum))
        {
            return false;
        }

        return minimum is null
            || maximum is null
            || minimum.Value.CompareTo(maximum.Value) <= 0;
    }

    private static bool TryReadDecimal(JsonElement schema, string name, out ExactJsonNumber? value)
    {
        value = null;
        if (!schema.TryGetProperty(name, out var element))
        {
            return true;
        }

        if (!ExactJsonNumber.TryParse(element, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool Matches(JsonElement instance, JsonElement schema, int depth)
    {
        if (depth > MaximumResponseDepth
            || schema.GetProperty("type").GetString() is not { } type
            || !MatchesDeclaredType(instance, type))
        {
            return false;
        }

        if (schema.TryGetProperty("enum", out var allowed)
            && !allowed.EnumerateArray().Any(value => MatchesEnumValue(value, instance)))
        {
            return false;
        }

        return type switch
        {
            "object" => MatchesObject(instance, schema, depth),
            "array" => MatchesArray(instance, schema, depth),
            "string" => MatchesString(instance, schema),
            "integer" or "number" => MatchesNumber(instance, schema),
            "boolean" => true,
            _ => false,
        };
    }

    private static bool MatchesObject(JsonElement instance, JsonElement schema, int depth)
    {
        var properties = schema.GetProperty("properties");
        var count = 0;
        foreach (var property in instance.EnumerateObject())
        {
            count++;
            if (!properties.TryGetProperty(property.Name, out var propertySchema)
                || !Matches(property.Value, propertySchema, depth + 1))
            {
                return false;
            }
        }

        foreach (var required in schema.GetProperty("required").EnumerateArray())
        {
            if (!instance.TryGetProperty(required.GetString()!, out _))
            {
                return false;
            }
        }

        return MatchesIntegerBounds(count, schema, "minProperties", "maxProperties");
    }

    private static bool MatchesArray(JsonElement instance, JsonElement schema, int depth)
    {
        if (!MatchesIntegerBounds(instance.GetArrayLength(), schema, "minItems", "maxItems"))
        {
            return false;
        }

        var itemSchema = schema.GetProperty("items");
        return instance.EnumerateArray().All(item => Matches(item, itemSchema, depth + 1));
    }

    private static bool MatchesString(JsonElement instance, JsonElement schema)
    {
        var length = instance.GetString()!.EnumerateRunes().Count();
        return MatchesIntegerBounds(length, schema, "minLength", "maxLength");
    }

    private static bool MatchesNumber(JsonElement instance, JsonElement schema)
    {
        if (!ExactJsonNumber.TryParse(instance, out var value))
        {
            return false;
        }

        return (!schema.TryGetProperty("minimum", out var minimum)
                || ExactJsonNumber.TryParse(minimum, out var minimumValue)
                    && value.CompareTo(minimumValue) >= 0)
            && (!schema.TryGetProperty("maximum", out var maximum)
                || ExactJsonNumber.TryParse(maximum, out var maximumValue)
                    && value.CompareTo(maximumValue) <= 0);
    }

    private static bool MatchesIntegerBounds(
        int value,
        JsonElement schema,
        string minimumName,
        string maximumName)
        => (!schema.TryGetProperty(minimumName, out var minimum)
                || ExactJsonNumber.TryParse(minimum, out var minimumValue)
                    && minimumValue.TryGetInt32(out var minimumInteger)
                    && value >= minimumInteger)
            && (!schema.TryGetProperty(maximumName, out var maximum)
                || ExactJsonNumber.TryParse(maximum, out var maximumValue)
                    && maximumValue.TryGetInt32(out var maximumInteger)
                    && value <= maximumInteger);

    private static bool MatchesDeclaredType(JsonElement value, string type)
        => type switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "integer" => IsJsonInteger(value),
            "number" => ExactJsonNumber.TryParse(value, out _),
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => false,
        };

    private static bool IsJsonInteger(JsonElement value)
        => ExactJsonNumber.TryParse(value, out var number) && number.IsInteger;

    private static bool MatchesEnumValue(JsonElement allowed, JsonElement instance)
    {
        if (allowed.ValueKind == JsonValueKind.Number
            && instance.ValueKind == JsonValueKind.Number
            && ExactJsonNumber.TryParse(allowed, out var allowedNumber)
            && ExactJsonNumber.TryParse(instance, out var instanceNumber))
        {
            return allowedNumber.Equals(instanceNumber);
        }

        return JsonElement.DeepEquals(allowed, instance);
    }

    private static bool JsonSemanticallyEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    var leftProperties = left.EnumerateObject().ToArray();
                    var rightProperties = right.EnumerateObject().ToArray();
                    if (leftProperties.Length != rightProperties.Length)
                    {
                        return false;
                    }

                    foreach (var property in leftProperties)
                    {
                        if (!right.TryGetProperty(property.Name, out var rightValue)
                            || !JsonSemanticallyEquals(property.Value, rightValue))
                        {
                            return false;
                        }
                    }

                    return true;
                }

            case JsonValueKind.Array:
                {
                    var leftItems = left.EnumerateArray().ToArray();
                    var rightItems = right.EnumerateArray().ToArray();
                    return leftItems.Length == rightItems.Length
                        && leftItems.Zip(rightItems).All(pair =>
                            JsonSemanticallyEquals(pair.First, pair.Second));
                }

            case JsonValueKind.String:
                return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Number:
                return ExactJsonNumber.TryParse(left, out var leftNumber)
                    && ExactJsonNumber.TryParse(right, out var rightNumber)
                    && leftNumber.Equals(rightNumber);
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;
            default:
                return false;
        }
    }

    private readonly record struct ExactJsonNumber(BigInteger Significand, BigInteger Exponent)
        : IComparable<ExactJsonNumber>
    {
        public bool IsInteger => Significand.IsZero || Exponent.Sign >= 0;

        public string CanonicalIdentity => $"{Significand.ToString(CultureInfo.InvariantCulture)}e{Exponent.ToString(CultureInfo.InvariantCulture)}";

        public static bool TryParse(JsonElement element, out ExactJsonNumber number)
        {
            number = default;
            if (element.ValueKind != JsonValueKind.Number)
            {
                return false;
            }

            var raw = element.GetRawText();
            if (raw.Length is 0 or > MaximumNumericLiteralCharacters)
            {
                return false;
            }

            var negative = raw[0] == '-';
            var numberStart = negative ? 1 : 0;
            var exponentMarker = raw.IndexOfAny(['e', 'E'], numberStart);
            var mantissaEnd = exponentMarker >= 0 ? exponentMarker : raw.Length;
            var decimalPoint = raw.IndexOf('.', numberStart, mantissaEnd - numberStart);
            var fractionDigits = decimalPoint >= 0 ? mantissaEnd - decimalPoint - 1 : 0;
            var digits = decimalPoint >= 0
                ? string.Concat(raw.AsSpan(numberStart, decimalPoint - numberStart), raw.AsSpan(decimalPoint + 1, fractionDigits))
                : raw[numberStart..mantissaEnd];
            digits = digits.TrimStart('0');
            if (digits.Length == 0)
            {
                number = new(BigInteger.Zero, BigInteger.Zero);
                return true;
            }

            var trailingZeros = 0;
            while (trailingZeros < digits.Length && digits[^(trailingZeros + 1)] == '0')
            {
                trailingZeros++;
            }

            if (trailingZeros > 0)
            {
                digits = digits[..^trailingZeros];
            }

            if (!BigInteger.TryParse(
                    digits,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var significand))
            {
                return false;
            }

            BigInteger explicitExponent = BigInteger.Zero;
            if (exponentMarker >= 0
                && !BigInteger.TryParse(
                    raw[(exponentMarker + 1)..],
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out explicitExponent))
            {
                return false;
            }

            if (negative)
            {
                significand = BigInteger.Negate(significand);
            }

            number = new(
                significand,
                explicitExponent - fractionDigits + trailingZeros);
            return true;
        }

        public int CompareTo(ExactJsonNumber other)
        {
            var signComparison = Significand.Sign.CompareTo(other.Significand.Sign);
            if (signComparison != 0)
            {
                return signComparison;
            }

            if (Significand.IsZero)
            {
                return 0;
            }

            var left = BigInteger.Abs(Significand);
            var right = BigInteger.Abs(other.Significand);
            var leftDigits = left.ToString(CultureInfo.InvariantCulture).Length;
            var rightDigits = right.ToString(CultureInfo.InvariantCulture).Length;
            var leftMagnitude = Exponent + leftDigits;
            var rightMagnitude = other.Exponent + rightDigits;
            var magnitudeComparison = leftMagnitude.CompareTo(rightMagnitude);
            if (magnitudeComparison != 0)
            {
                return Significand.Sign > 0 ? magnitudeComparison : -magnitudeComparison;
            }

            var exponentDifference = Exponent - other.Exponent;
            var absoluteComparison = exponentDifference.Sign switch
            {
                > 0 => (left * BigInteger.Pow(10, checked((int)exponentDifference))).CompareTo(right),
                < 0 => left.CompareTo(right * BigInteger.Pow(10, checked((int)BigInteger.Negate(exponentDifference)))),
                _ => left.CompareTo(right),
            };
            return Significand.Sign > 0 ? absoluteComparison : -absoluteComparison;
        }

        public bool TryGetInt32(out int value)
        {
            value = default;
            if (!IsInteger || Exponent > 10)
            {
                return false;
            }

            var integer = Significand * BigInteger.Pow(10, (int)Exponent);
            if (integer.CompareTo(int.MinValue) < 0 || integer.CompareTo(int.MaxValue) > 0)
            {
                return false;
            }

            value = (int)integer;
            return true;
        }
    }

    private static bool HasDuplicatePropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name) || HasDuplicatePropertyNames(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (HasDuplicatePropertyNames(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasValidDecodedStrings(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!IsWellFormedUtf16(property.Name)
                    || !HasValidDecodedStrings(property.Value))
                {
                    return false;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (!HasValidDecodedStrings(item))
                {
                    return false;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.String
            && !IsWellFormedUtf16(element.GetString()))
        {
            return false;
        }

        return true;
    }

    private static bool IsWellFormedUtf16(string? value)
    {
        if (value is null)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsHighSurrogate(character))
            {
                if (++index >= value.Length || !char.IsLowSurrogate(value[index]))
                {
                    return false;
                }
            }
            else if (char.IsLowSurrogate(character))
            {
                return false;
            }
        }

        return true;
    }
}
