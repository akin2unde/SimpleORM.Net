using System.Linq.Expressions;


using System.Security.Cryptography;


using SimpleORM.Net.Abstractions;


using SimpleORM.Net.Configuration;


using SimpleORM.Net.Metadata;


using SimpleORM.Net.Models;


using SimpleORM.Net.Query;


namespace SimpleORM.Net.Services;


/// <summary>Base DataService implementation.</summary>
public class DataService<T>(IDatabaseProvider provider,IDBMetadataProvider metadata,ICodeGenerator codes,IDBTransactionManager tx,ITenantProvider tenant,IUserProvider user,IExtensionService extensions,IAuditService audit,SimpleOrmOptions options):IDataService<T> where T:DBModel
{

    /// <inheritdoc />
    public async Task<PagedResult<T>> Select(SearchParam? search=null,int skip=0,int limit=100,int? batch=null,CancellationToken ct=default)
    {

        if(skip<0||limit<0)throw new ArgumentOutOfRangeException();

        var p=search?.Clone()??new();

        var total=await provider.Count<T>(p,ct);

        var available=Math.Max(0,total-skip);

        var target=limit==0?available:Math.Min(limit,available);

        var bs=BatchResolver.Resolve(batch,options.Batch.Select);

        var data=new List<T>();

        var pos=skip;

        long remain=target;


        while(remain>0)
        {
            var take=(int)Math.Min(bs,remain);

            var chunk=await provider.Select<T>(p,pos,take,ct);

            if(chunk.Count==0)break;

            data.AddRange(chunk);

            pos+=chunk.Count;

            remain-=chunk.Count;

            if(chunk.Count<take)break;

        }

        await extensions.Load(data,ct);

        ApplyDefaults(data);

        return new PagedResult<T>
        {
            Data=data,TotalRecords=total,Skipped=skip,Limit=limit
        }
        ;


    }

    /// <inheritdoc />
    public Task<PagedResult<T>> Select(Expression<Func<T,bool>> e,SearchParam? s=null,int skip=0,int limit=100,int? batch=null,CancellationToken ct=default)=>Select(ExpressionTranslator.Translate(e,s),skip,limit,batch,ct);


    /// <inheritdoc />
    public async Task<T?> SelectSingle(SearchParam? s=null,CancellationToken ct=default)
    {
        var x=await provider.SelectSingle<T>(s?.Clone()??new(),ct);

        if(x is null)return null;

        await extensions.Load(new[]
        {
            x
        }
        ,ct);

        ApplyDefaults(new[]
        {
            x
        }
        );

        return x;

    }

    /// <inheritdoc />
    public Task<T?> SelectSingle(Expression<Func<T,bool>> e,SearchParam? s=null,CancellationToken ct=default)=>SelectSingle(ExpressionTranslator.Translate(e,s),ct);


    /// <inheritdoc />
    public async Task<T?> GetByCode(string code,CancellationToken ct=default)
    {
        var x=await provider.GetByCode<T>(code,ct);

        if(x is null)return null;

        await extensions.Load(new[]
        {
            x
        }
        ,ct);

        ApplyDefaults(new[]
        {
            x
        }
        );

        return x;

    }

    /// <inheritdoc />
    public Task<PagedResult<T>> Search(string text,SearchParam? s=null,int skip=0,int limit=100,int? batch=null,CancellationToken ct=default)
    {
        var p=s?.Clone()??new();

        p.Search=text;

        return Select(p,skip,limit,batch,ct);

    }

    /// <inheritdoc />
    public Task<long> Count(SearchParam? s=null,CancellationToken ct=default)=>provider.Count<T>(s?.Clone()??new(),ct);


    /// <inheritdoc />
    public Task<long> Count(Expression<Func<T,bool>> e,SearchParam? s=null,CancellationToken ct=default)=>Count(ExpressionTranslator.Translate(e,s),ct);


    /// <inheritdoc />
    public async Task<T> Save(T m,CancellationToken ct=default)
    {
        var r=await Save(new[]
        {
            m
        }
        ,1,ct);

        return r[0];

    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> Save(IEnumerable<T> models,int? batch=null,CancellationToken ct=default)
    {

        var items=models.ToList();

        if(items.Count==0)return items;

        Prepare(items);

        var bs=BatchResolver.Resolve(batch,options.Batch.Save);

        var meta=metadata.GetMetadata<T>();


        await tx.Execute(async()=>
        {
            var current=tx.Current!;
foreach(var group in items.GroupBy(x=>x.DataState))
            {
                if(group.Key==DataState.Unchanged)continue;
foreach(var chunk in group.Chunk(bs))
                {
                    var list=(IReadOnlyList<T>)chunk;
if(group.Key==DataState.New)await provider.Insert(list,current,ct);
else if(group.Key==DataState.Changed)await provider.Update(list,current,ct);
else await provider.Delete(list,meta.HardDelete,current,ct);
if (group.Key is DataState.New or DataState.Changed)
                    {
                        await extensions.Save(list, current, ct);
                    }

                    if (options.AuditTrail.Enabled && meta.AuditEnabled)
                    {
                        await audit.Write(group.Key.ToString(), list, current, ct);
                    }

                }
            }
        }
        ,ct);


        foreach(var x in items)x.DataState=DataState.Unchanged;

        return items;


    }

    /// <inheritdoc />
    public string GenerateDebugQuery(SearchParam? s=null,int skip=0,int limit=100)=>provider.GenerateDebugQuery<T>(s?.Clone()??new(),skip,limit);


    private void Prepare(IEnumerable<T> items)
    {
        var now=DateTime.UtcNow;

        var t=options.MultiTenancy.Enabled?tenant.GetTenantCode():null;

        if(options.MultiTenancy.Enabled&&string.IsNullOrWhiteSpace(t))throw new InvalidOperationException("Tenant required.");

        foreach(var x in items)
        {
            if(options.MultiTenancy.Enabled)x.TenantCode=t;

            if(x.DataState==DataState.New)
            {
                if(string.IsNullOrWhiteSpace(x.Code))x.Code=codes.Generate<T>();

                if(x.CreatedAt==default)x.CreatedAt=now;

                x.CreatedBy??=user.GetUserCode();

            }
            else
            {
                x.UpdatedAt=now;

                x.UpdatedBy=user.GetUserCode();

                if(x.DataState==DataState.Removed&&!metadata.GetMetadata<T>().HardDelete)x.DeletedAt=now;

            }
        }
    }

    private void ApplyDefaults(IEnumerable<T> items)
    {
        var m=metadata.GetMetadata<T>();

        foreach(var x in items)
        {
            x.DataState=DataState.Unchanged;

            foreach(var c in m.Columns.Where(c=>c.DefaultOnReturn))c.Property.SetValue(x,c.PropertyType.IsValueType?Activator.CreateInstance(c.PropertyType):null);

        }
    }

}
