using System.Text.Json;

using SimpleORM.Net.Attributes;

using SimpleORM.Net.Configuration;

using SimpleORM.Net.Metadata;

using SimpleORM.Net.Models;

using SimpleORM.Net.Query;

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


    /// <summary>Global models do not persist or require the inherited tenant column.</summary>
    [Fact]
    public void GlobalModelDisablesTenantScope()
    {
        var metadata = new DBMetadataProvider(Options())
            .GetMetadata<GlobalModel>();

        Assert.False(metadata.TenantScoped);
        Assert.Null(metadata.TenantColumn);
        Assert.True(metadata.Columns.Single(
            column => column.PropertyName == nameof(DBModel.Tenant)).Ignore);
    }

    /// <summary>Ignore removes custom properties from persisted metadata.</summary>
    [Fact]
    public void IgnoreRemovesPropertyFromPersistence()
    {
        var metadata = new DBMetadataProvider(Options())
            .GetMetadata<Customer>();

        Assert.DoesNotContain(
            metadata.PersistedColumns,
            column => column.PropertyName == nameof(Customer.TemporaryValue));
    }

    /// <summary>Auto-delete metadata is discovered from the model attribute.</summary>
    [Fact]
    public void AutoDeleteMetadataIsDiscovered()
    {
        var metadata = new DBMetadataProvider(Options())
            .GetMetadata<AutoDeleteModel>();

        Assert.Equal(60, metadata.AutoDeleteAfterDays);
        Assert.Equal("0 0 1 * *", metadata.AutoDeleteCron);
    }

    /// <summary>Search comparison operators use their compact public names.</summary>
    [Fact]
    public void SearchOperatorsUseCompactNames()
    {
        Assert.Equal("EQ", SearchOperator.EQ.ToString());
        Assert.Equal("NEQ", SearchOperator.NEQ.ToString());
        Assert.Equal("GT", SearchOperator.GT.ToString());
        Assert.Equal("GTE", SearchOperator.GTE.ToString());
        Assert.Equal("LT", SearchOperator.LT.ToString());
        Assert.Equal("LTE", SearchOperator.LTE.ToString());

        var json = JsonSerializer.Serialize(SearchOperator.GT);
        var value = JsonSerializer.Deserialize<SearchOperator>("\"GTE\"");

        Assert.Equal("\"GT\"", json);
        Assert.Equal(SearchOperator.GTE, value);
    }

    /// <summary>Query collection properties can be replaced during request binding.</summary>
    [Fact]
    public void SearchCollectionsAreMutableAndSettable()
    {
        var search = new SearchParam
        {
            Fields = new List<string> { nameof(Customer.Code) },
            Filters = new List<SearchFilter>
            {
                new()
                {
                    Field = nameof(Customer.Name),
                    Operator = SearchOperator.EQ,
                    Value = "Ada"
                }
            },
            Joins = new List<SearchJoin>
            {
                new()
                {
                    Model = typeof(Customer),
                    Fields = new List<string> { nameof(Customer.Name) }
                }
            }
        };

        Assert.Single(search.Fields);
        Assert.Single(search.Filters);
        Assert.Single(search.Joins[0].Fields);
    }

    private static SimpleOrmOptions Options()=>new()
    {
        DefaultStringLength=50
    }
    ;

}
