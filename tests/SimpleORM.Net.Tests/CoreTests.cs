using SimpleORM.Net.Attributes;


using SimpleORM.Net.Configuration;


using SimpleORM.Net.Metadata;


using SimpleORM.Net.Models;


using SimpleORM.Net.Services;


namespace SimpleORM.Net.Tests;


/// <summary>Convention tests.</summary>
public sealed class CoreTests
{

    /// <summary>Metadata conventions.</summary>
    [Fact]
    public void MetadataDefaults()
    {
        var o=Options();

        var m=new DBMetadataProvider(o).GetMetadata<Customer>();

        Assert.Equal("Customer",m.TableName);

        Assert.Equal("CUS",m.CodePrefix);

        Assert.Equal(10,m.CodeLength);

        Assert.True(m.CodeColumn.Unique);

        Assert.True(m.Columns.Single(x=>x.PropertyName==nameof(Customer.Name)).Searchable);

        Assert.False(m.Columns.Single(x=>x.PropertyName==nameof(Customer.Secret)).Searchable);

    }

    /// <summary>Batch hard cap.</summary>
    [Theory][InlineData(100,100,100)][InlineData(1000,100,500)][InlineData(null,250,250)]
    public void BatchResolution(int? request,int configured,int expected)=>Assert.Equal(expected,BatchResolver.Resolve(request,configured));


    /// <summary>Code format.</summary>
    [Fact]
    public void CodeFormat()
    {
        var o=Options();

        var p=new DBMetadataProvider(o);

        var c=new CodeGenerator(p,o).Generate<Customer>();

        Assert.StartsWith("CUS-",c);

        Assert.Equal(14,c.Length);

    }

    private static SimpleOrmOptions Options()=>new()
    {
        DefaultStringLength=50
    }
    ;


}
