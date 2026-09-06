using SimpleORM.Net.Attributes;

using SimpleORM.Net.Configuration;

using SimpleORM.Net.Metadata;

using SimpleORM.Net.Models;

using SimpleORM.Net.Query;

using System.Text.Json;

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


    /// <summary>Concurrency protection is enabled by default.</summary>
    [Fact]
    public void ConcurrencyIsEnabledByDefault()
    {
        var metadata = new DBMetadataProvider(Options())
            .GetMetadata<Customer>();

        Assert.True(metadata.ConcurrencyEnabled);
        Assert.Equal(1, new Customer().Version);
    }

    /// <summary>Models can explicitly opt out of concurrency protection.</summary>
    [Fact]
    public void ModelCanDisableConcurrencyCheck()
    {
        var metadata = new DBMetadataProvider(Options())
            .GetMetadata<NoConcurrencyModel>();

        Assert.False(metadata.ConcurrencyEnabled);
    }

    /// <summary>Global concurrency can be disabled.</summary>
    [Fact]
    public void ConcurrencyCanBeDisabledGlobally()
    {
        var options = Options();
        options.Concurrency.Enabled = false;

        var metadata = new DBMetadataProvider(options)
            .GetMetadata<Customer>();

        Assert.False(metadata.ConcurrencyEnabled);
    }

    /// <summary>Numeric JSON filter values are converted to model enum values.</summary>
    [Fact]
    public void SearchFilterConvertsNumericJsonElementToEnum()
    {
        var metadata = new DBMetadataProvider(Options());
        var normalizer = new SearchParamNormalizer(metadata);
        using var document = JsonDocument.Parse("1");
        var search = new SearchParam
        {
            Filters =
            [
                new SearchFilter
                {
                    Field = nameof(Customer.Status),
                    Operator = SearchOperator.EQ,
                    Value = document.RootElement.Clone()
                }
            ]
        };

        var normalized = normalizer.Normalize<Customer>(search);

        Assert.Equal(CustomerStatus.Active, normalized.Filters[0].Value);
        Assert.IsType<JsonElement>(search.Filters[0].Value);
    }

    /// <summary>JSON arrays used by IN are converted item by item.</summary>
    [Fact]
    public void SearchFilterConvertsJsonArrayToEnumValues()
    {
        var metadata = new DBMetadataProvider(Options());
        var normalizer = new SearchParamNormalizer(metadata);
        using var document = JsonDocument.Parse("[0, 1]");
        var search = new SearchParam
        {
            Filters =
            [
                new SearchFilter
                {
                    Field = nameof(Customer.Status),
                    Operator = SearchOperator.In,
                    Value = document.RootElement.Clone()
                }
            ]
        };

        var normalized = normalizer.Normalize<Customer>(search);
        var values = Assert.IsType<List<object?>>(normalized.Filters[0].Value);

        Assert.Equal(
            new[] { CustomerStatus.Inactive, CustomerStatus.Active },
            values.Cast<CustomerStatus>().ToArray());
    }

    private static SimpleOrmOptions Options()=>new()
    {
        DefaultStringLength=50
    }
    ;

}
