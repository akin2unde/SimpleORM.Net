namespace SimpleORM.Net.Configuration;

/// <summary>Code generation conventions.</summary>
public sealed class CodeGenerationOptions
{

    /// <summary>Default random suffix length.</summary>
    public int Length
    {
        get;

        set;

    }
    =10;

    /// <summary>Inferred prefix length.</summary>
    public int PrefixLength
    {
        get;

        set;

    }
    =3;

    /// <summary>Separator.</summary>
    public string Separator
    {
        get;

        set;

    }
    ="-";

}
