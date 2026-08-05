using System;
using UnityEngine;

/// <summary>
/// Stores a stable localization entry ID in Unity-serialized data.
/// </summary>
[Serializable]
public struct I18nKey : IEquatable<I18nKey>
{
    [SerializeField]
    private long _id;

    /// <summary>
    /// Initializes a localization key with a positive entry ID or zero for an unassigned key.
    /// </summary>
    /// <param name="id">The stable entry ID, or zero for an unassigned key.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> is negative.</exception>
    public I18nKey(long id)
    {
        if (id < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Localization key ID cannot be negative.");
        }

        _id = id;
    }

    /// <summary>
    /// Gets an unassigned localization key.
    /// </summary>
    public static I18nKey None => default;

    /// <summary>
    /// Gets the stable entry ID, or zero when the key is unassigned.
    /// </summary>
    public long Id => _id;

    /// <summary>
    /// Gets a value indicating whether an entry is assigned.
    /// </summary>
    public bool IsAssigned => _id > 0;

    /// <summary>
    /// Converts a Unity localization key to its stable numeric ID.
    /// </summary>
    public static implicit operator long(I18nKey key)
    {
        return key._id;
    }

    /// <inheritdoc />
    public bool Equals(I18nKey other)
    {
        return _id == other._id;
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
        return obj is I18nKey other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return _id.GetHashCode();
    }

    /// <summary>
    /// Determines whether two localization keys contain the same entry ID.
    /// </summary>
    public static bool operator ==(I18nKey left, I18nKey right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two localization keys contain different entry IDs.
    /// </summary>
    public static bool operator !=(I18nKey left, I18nKey right)
    {
        return !left.Equals(right);
    }
}
