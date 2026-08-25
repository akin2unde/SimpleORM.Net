using SimpleORM.Net.Configuration;

namespace SimpleORM.Net.Attributes;

/// <summary>
/// Overrides the convention-based database metadata for a model property.
/// Properties are optional: when an option is not specified, SimpleORM.Net uses
/// the corresponding convention or injection-level setting.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DBColumnAttribute : Attribute
{

    private int _size;

    private bool _nullable;

    private EnumStorage _enumStorage;

    /// <summary>
    /// Gets or sets the physical database column name. When omitted, the CLR
    /// property name is used.
    /// </summary>
    public string? Name
    {
        get;

        set;

    }

    /// <summary>
    /// Gets or sets the string column size. A value of -1 means provider-specific
    /// unbounded text. When omitted, the injection-level default string size is used.
    /// </summary>
    public int Size

    {

        get => _size;

        set

        {

            _size = value;

            HasSize = true;

        }

    }

    /// <summary>
    /// Gets or sets whether the column should be nullable. When omitted, nullability
    /// is inferred from the CLR property type.
    /// </summary>
    public bool Nullable

    {

        get => _nullable;

        set

        {

            _nullable = value;

            HasNullable = true;

        }

    }

    /// <summary>
    /// Gets or sets how an enum property is persisted. When omitted, the global
    /// enum-storage setting is used.
    /// </summary>
    public EnumStorage EnumStorage

    {

        get => _enumStorage;

        set

        {

            _enumStorage = value;

            HasEnumStorage = true;

        }

    }

    /// <summary>Indicates whether <see cref="Size"/> was explicitly supplied.</summary>
    internal bool HasSize
    {
        get;

        private set;

    }

    /// <summary>Indicates whether <see cref="Nullable"/> was explicitly supplied.</summary>
    internal bool HasNullable
    {
        get;

        private set;

    }

    /// <summary>Indicates whether <see cref="EnumStorage"/> was explicitly supplied.</summary>
    internal bool HasEnumStorage
    {
        get;

        private set;

    }

}
