//此文件中的API已廢棄
namespace Tsinswreng.CsSql;

using Tsinswreng.CsPage;
using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;

[Doc(@$"
Common Repository Interface for SQL Database,
provides basic CRUD operations.

naming rules:
- Insert -> Add
- Select -> Get
- Update -> Upd
- Delete -> Del

- NOT support auto increment id for insert operation.
- throw exception if insert or update fails.
- update will match the primary key of the entity as benchmark.

for `Get` operation, defaultly
soft deleted data are not included.
")]
public partial interface IRepo<TEntity, TId>{
	public IAsyncEnumerable<TEntity?> GetManyInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	);

	[Doc(@$"using `Id IN (...)` Clause,
	which would ignore unexisted Id and returned list may be unordered.
	")]
	public IAsyncEnumerable<TEntity?> GetManyInIdWithDel(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	);

	public IAsyncEnumerable<TEntity?> BatGetById(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	);

	[Doc(@$"Got Entities are corresponding to the given Ids. if not found, the place will be null.")]
	public IAsyncEnumerable<TEntity?> BatGetByIdWithDel(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	);

	public IAsyncEnumerable<TEntity> GetAll(
		IDbFnCtx Ctx, CT Ct
	);
	
	public IAsyncEnumerable<TEntity> GetAllWithDel(
		IDbFnCtx Ctx, CT Ct
	);
	
	
	[Doc(@$"
	should throw exception if conflict (e.g constraint violation) etc.
	")]
	public Task<IRespBatInsert> BatAdd(
		IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct
	);
	
	[Doc(@$"by the primary key of the entity,
	so you don't need to provide the entity id independantly.
	should throw exception if conflict (e.g constraint violation) etc.
	")]
	public Task<IRespBatUpd> BatUpd(
		IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct
	);
	
	[Doc(@$"
	#Params(
		[],
		[Dicts, Db Col Map to Raw Value, support;
		dicts with different key structure are allowed],
		[Ids, its count must equal to Dicts count],
		[],
	)
	#Descr[should throw exception if conflict (e.g constraint violation) etc.]
	#Examples([
	```cs
	
	```
	])
	")]
	public Task<IRespBatUpd> BatUpdByDbDict(
		IDbFnCtx Ctx
		,IAsyncEnumerable<TId> Ids
		,IAsyncEnumerable<IStr_Any> Dicts
		,CT Ct
	);
	
	[Doc(@$"
	#Params(
		[],
		[Dicts, Code Col(Entity Field) Map to Upper Value(Entity member), support;
		dicts with different key structure are allowed],
		[Ids, its count must equal to Dicts count],
		[],
	)
	#Descr[should throw exception if conflict (e.g constraint violation) etc.]
	#Examples([
	```cs
	
	```
	])
	")]
	public Task<IRespBatUpd> BatUpdByCodeDict(
		IDbFnCtx Ctx
		,IAsyncEnumerable<TId> Ids
		,IAsyncEnumerable<IStr_Any> Dicts
		,CT Ct
	);
	
	public Task<IBatSoftDel> BatSoftDelById(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	);
	
	public Task<IBatHardDel> BatHardDelById(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	);
	
	public Task<ISoftDelInId> SoftDelInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	);
	
	public Task<IHardDelInId> HardDelInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	);
	
	#region Agg
	
	public Task<IRespBatAddAgg> BatAddAgg<TAgg>(
		IDbFnCtx Ctx
		,IAsyncEnumerable<TAgg> NewAgg
		,CT Ct
	);
	public IAsyncEnumerable<TAgg> GetAllAgg<TAgg>(
		IDbFnCtx Ctx, CT Ct
	);
	public IAsyncEnumerable<TAgg> GetAllAggWithDel<TAgg>(
		IDbFnCtx Ctx, CT Ct
	);
	
	[Doc(@$"Batch select aggregate roots by ids; aggregate metadata should be registered in ITblMgr.AddAgg().")]
	public IAsyncEnumerable<TAgg?> BatGetAggById<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	)where TAgg: class;
	
	public IAsyncEnumerable<TAgg?> BatGetAggByIdWithDel<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	)where TAgg: class;
	
	
	[Doc(@$"Hard Delete Both Root and its related assets")]
	public Task<IRespHardDelAggInId> HardDelAggInId<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	);
	
	
	[Doc(@$"Soft Delete Both Root and its related assets,
	if you only need to soft del the root, use {nameof(BatSoftDelById)} for the root
	")]
	public Task<IRespSoftDelAggInId> SoftDelAggInId<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	);
	
	[Doc(@$"Batch Update Aggregates. make db's data the same as passed-in data
		for each agg, after update,
		use `{nameof(BatGetAggByIdWithDel)}` will return the updated agg
		as what I passed to `{nameof(BatHardUpdAgg)}`.
		`Hard` means hard delete one-to-many assets that new agg doesn't have.
	")]
	public Task<IRespBatUpdAgg> BatHardUpdAgg<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TAgg> Agg, CT Ct
	);
	
	[Doc(@$"Batch Update Aggregates. make db's data the same as passed-in data
		for each agg, after update,
		use `{nameof(BatGetAggByIdWithDel)}` will return the updated agg
		as what I passed to `{nameof(BatHardUpdAgg)}`.
		`Soft` means Soft delete one-to-many assets that new agg doesn't have.
	")]
	public Task<IRespBatUpdAgg> BatSoftUpdAgg<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TAgg> Agg, CT Ct
	);
	
	
	[Doc(@$"
	assume you have MainEntity and AssetEntity, Each MainEntity Has Many AssetEntity,
	when you want to selete multi MainEntity with their respective AssetEntity,
	use this to avoid N+1 Query
	#Params(
		[],
		[logical Forein Key],
		[Options],
		[All Keys. we use `IN` inside to avoid N+1 Query
		null will be filtered off by code before being sent to db],
		[main entity member selector],
		[Table],
		[],
	)
	#Rtn[Dict of Key map to multi OneToMany entitys]
	")]
	public Task<IDictionary<TKey, IList<TPo>>> IncludeEntitysByKeys<TPo, TKey>(
		IDbFnCtx Ctx
		,str CodeCol
		,OptQry? OptQry
		,IEnumerable<TKey?> Keys
		,Func<TPo, TKey> FnMemb
		,ITable Tbl
		,CT Ct
	)where TPo: new();


	public Task<IDictionary<TKey, IList<TPo>>> IncludeEntitysByKeys<TPo, TKey>(
		IDbFnCtx Ctx
		,str CodeCol
		,OptQry? OptQry
		,IEnumerable<TKey> Keys
		,Func<TPo, TKey> FnMemb
		,ITable<TPo> Tbl //帶泛型
		,CT Ct
	)where TPo: new();
	
	#endregion Agg

}



public class IRespBatInsert{
	
}

public class RespBatInsert:IRespBatInsert{}


public class IRespBatUpd{
	
}
public class RespUpd:IRespBatUpd{
	
}

public class IBatSoftDel{}

public class BatSoftDel:IBatSoftDel{}

public class IBatHardDel{}

public class BatHardDel:IBatHardDel{}

public class IHardDelInId{
	
}

public class HardDelInId:IHardDelInId{}


public class ISoftDelInId{}

public class SoftDelInId:ISoftDelInId{}

public class IRespBatAddAgg{}
public class RespBatAddAgg:IRespBatAddAgg{}

public class IRespBatUpdAgg{}
public class RespBatUpdAgg:IRespBatUpdAgg{}


public class IRespHardDelAggInId{}
public class RespHardDelAggInId:IRespHardDelAggInId{}

public class IRespSoftDelAggInId{}
public class RespSoftDelAggInId:IRespSoftDelAggInId{}
