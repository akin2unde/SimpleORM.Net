namespace SimpleORM.Net.Configuration;


/// <summary>Batch conventions.</summary>
public sealed class BatchOptions
{

    /// <summary>Save batch.</summary>
    public int Save
    {
        get;

        set;

    }
    =100;


    /// <summary>Select batch.</summary>
    public int Select
    {
        get;

        set;

    }
    =100;


}
