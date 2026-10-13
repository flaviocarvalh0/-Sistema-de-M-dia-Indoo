using System.Globalization;
using Atlas.Infrastructure.Data.History.Modeling;
using Atlas.Infrastructure.Data.History.Reading;

namespace Atlas.Infrastructure.Data.History.Comparison;

/// <summary>
/// Compara versões históricas consecutivas e identifica os campos
/// efetivamente modificados.
/// </summary>
internal sealed class EntityHistorySnapshotComparer
    : IEntityHistorySnapshotComparer
{
    public IReadOnlyCollection<EntityHistoryVersionComparison> Compare(
        EntityHistoryDefinition definition,
        EntityHistorySnapshotBatch batch)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Snapshots.Count == 0)
        {
            return [];
        }

        var snapshots = batch.Snapshots.ToArray();

        var comparableColumns = definition.Columns
            .Where(EntityHistoryColumnPolicy.IsDisplayedAsFieldChange)
            .ToArray();

        var comparisons =
            new List<EntityHistoryVersionComparison>(
                snapshots.Length);

        for (var index = 0; index < snapshots.Length; index++)
        {
            var current = snapshots[index];

            var previous = GetPreviousSnapshot(
                snapshots,
                index,
                batch.ComparisonBaseline);

            var isInitialVersion =
                previous is null;

            var changes = CompareValues(
                comparableColumns,
                current,
                previous);

            comparisons.Add(
                new EntityHistoryVersionComparison
                {
                    HistoryId = current.HistoryId,
                    RecordId = current.RecordId,
                    HistoryCreatedAt = current.HistoryCreatedAt,
                    IsInitialVersion = isInitialVersion,
                    Changes = changes,
                    Values = current.Values
                });
        }

        return comparisons;
    }

    private static EntityHistorySnapshot? GetPreviousSnapshot(
        IReadOnlyList<EntityHistorySnapshot> snapshots,
        int currentIndex,
        EntityHistorySnapshot? comparisonBaseline)
    {
        var nextIndex = currentIndex + 1;

        if (nextIndex < snapshots.Count)
        {
            return snapshots[nextIndex];
        }

        return comparisonBaseline;
    }

    private static IReadOnlyCollection<EntityHistoryFieldChange>
        CompareValues(
            IReadOnlyCollection<EntityHistoryColumnDefinition> columns,
            EntityHistorySnapshot current,
            EntityHistorySnapshot? previous)
    {
        var changes =
            new List<EntityHistoryFieldChange>();

        foreach (var column in columns)
        {
            current.Values.TryGetValue(
                column.ColumnName,
                out var currentValue);

            object? previousValue = null;

            if (previous is not null)
            {
                previous.Values.TryGetValue(
                    column.ColumnName,
                    out previousValue);
            }

            if (ValuesAreEquivalent(
                    previousValue,
                    currentValue))
            {
                continue;
            }

            changes.Add(
                new EntityHistoryFieldChange
                {
                    ColumnName = column.ColumnName,
                    PropertyName = column.PropertyName,
                    PreviousValue = previousValue,
                    CurrentValue = currentValue
                });
        }

        return changes;
    }

    private static bool ValuesAreEquivalent(
        object? left,
        object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left is byte[] leftBytes &&
            right is byte[] rightBytes)
        {
            return leftBytes.AsSpan()
                .SequenceEqual(rightBytes);
        }

        if (left is DateTime leftDateTime &&
            right is DateTime rightDateTime)
        {
            return NormalizeDateTime(leftDateTime) ==
                   NormalizeDateTime(rightDateTime);
        }

        if (left is DateTimeOffset leftDateTimeOffset &&
            right is DateTimeOffset rightDateTimeOffset)
        {
            return leftDateTimeOffset.ToUniversalTime() ==
                   rightDateTimeOffset.ToUniversalTime();
        }

        if (IsNumeric(left) && IsNumeric(right))
        {
            return NumericValuesAreEquivalent(
                left,
                right);
        }

        if (left is Array leftArray &&
            right is Array rightArray)
        {
            return ArraysAreEquivalent(
                leftArray,
                rightArray);
        }

        return Equals(left, right);
    }

    private static DateTime NormalizeDateTime(
        DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,

            DateTimeKind.Local =>
                value.ToUniversalTime(),

            _ => DateTime.SpecifyKind(
                value,
                DateTimeKind.Utc)
        };
    }

    private static bool IsNumeric(
        object value)
    {
        return Type.GetTypeCode(value.GetType()) switch
        {
            TypeCode.Byte or
            TypeCode.SByte or
            TypeCode.UInt16 or
            TypeCode.UInt32 or
            TypeCode.UInt64 or
            TypeCode.Int16 or
            TypeCode.Int32 or
            TypeCode.Int64 or
            TypeCode.Decimal or
            TypeCode.Double or
            TypeCode.Single => true,

            _ => false
        };
    }

    private static bool NumericValuesAreEquivalent(
        object left,
        object right)
    {
        try
        {
            var leftDecimal = Convert.ToDecimal(
                left,
                CultureInfo.InvariantCulture);

            var rightDecimal = Convert.ToDecimal(
                right,
                CultureInfo.InvariantCulture);

            return leftDecimal == rightDecimal;
        }
        catch (OverflowException)
        {
            var leftDouble = Convert.ToDouble(
                left,
                CultureInfo.InvariantCulture);

            var rightDouble = Convert.ToDouble(
                right,
                CultureInfo.InvariantCulture);

            return leftDouble.Equals(rightDouble);
        }
    }

    private static bool ArraysAreEquivalent(
        Array left,
        Array right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        var leftEnumerator = left.GetEnumerator();
        var rightEnumerator = right.GetEnumerator();

        while (leftEnumerator.MoveNext() &&
               rightEnumerator.MoveNext())
        {
            if (!ValuesAreEquivalent(
                    leftEnumerator.Current,
                    rightEnumerator.Current))
            {
                return false;
            }
        }

        return true;
    }
}
