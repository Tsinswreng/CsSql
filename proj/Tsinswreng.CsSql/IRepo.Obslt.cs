namespace Tsinswreng.CsSql;

using Tsinswreng.CsPage;
using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;

[Doc($@"
#Sum[Repository interface for database common operations on entities]
#TParams([Entity type],[Entity ID type])
")]
public partial interface IRepo<TEntity, TId>{
	[Obsolete]
	public Task<Func<
		IEnumerable<TEntity>
		,CT
		,Task<nil>
	>> FnInsertMany(
		IDbFnCtx Ctx
		,CT Ct
	);

	[Obsolete]
	public Task<Func<
		IAsyncEnumerable<TEntity>
		,CT
		,Task<nil>
	>> FnInsertAsyE(
		IDbFnCtx Ctx
		,CT Ct
	);


	[Obsolete]
	public Task<Func<
		TEntity
		,CT
		,Task<nil>
	>> FnInsertOne(
		IDbFnCtx Ctx
		,CT Ct
	);

	[Obsolete]
	public Task<Func<
		CT
		,Task<u64>
	>> FnCount(
		IDbFnCtx Ctx
		,CT Ct
	);

	[Obsolete]
	public Task<Func<
		TId
		,CT
		,Task<TEntity?>
	>> FnSlctOneById(
		IDbFnCtx Ctx
		,CT Ct
	);
	
	[Obsolete]
	public Task<Func<
		IPageQry
		,CT, Task<IPageAsyE<IDictionary<str, obj?>>>
	>> FnPageAllDict(IDbFnCtx Ctx, CT Ct);


	[Obsolete]
	public Task<Func<
		IPageQry
		,CT, Task<IPageAsyE<TEntity>>
	>> FnPageAll(IDbFnCtx Ctx, CT Ct);


	[Obsolete]
	public Task<Func<
		TId
		,TEntity
		,CT, Task<nil>
	>> FnUpdByIdOld(
		IDbFnCtx Ctx
		,IEnumerable<str>? FieldsToUpdate
		,CT Ct
	);


	[Obsolete]
	public Task<Func<
		IEnumerable<Id_Dict<TId>>
		,CT
		,Task<nil>
	>> FnUpdManyByIdOld(
		IDbFnCtx Ctx
		,IEnumerable<str> FieldsToUpdate
		,CT Ct
	);


	[Obsolete]
	public Task<Func<
		TEntity
		,CT, Task<nil>
	>> FnUpdOneById(
		IDbFnCtx Ctx
		,IEnumerable<str>? UpperFieldsToUpdate
		,CT Ct
	);


	[Obsolete]
	public Task<Func<
		IEnumerable<TEntity>
		,CT
		,Task<nil>
	>> FnUpdManyById(
		IDbFnCtx Ctx
		,IEnumerable<str>? UpperFieldsToUpdate
		,CT Ct
	);


	[Obsolete]
	public Task<Func<
		IAsyncEnumerable<TEntity>
		,CT
		,Task<nil>
	>> FnAsyEUpdManyById(
		IDbFnCtx Ctx
		,IEnumerable<str>? UpperFieldsToUpdate
		,CT Ct
	);


	[Obsolete]
	public Task<Func<
		IEnumerable<obj?>
		,CT
		,Task<nil>
	>> FnSoftDelManyByKeys(
		IDbFnCtx Ctx
		,str KeyNameInCode
		,u64 CountPerBatch
		,CT Ct
	);


	[Obsolete]
	public Task<Func<
		IEnumerable<TKey>
		,CT
		,Task<nil>
	>> FnSoftDelManyByKeys<TKey>(
		IDbFnCtx Ctx
		,str KeyNameInCode
		,u64 CountPerBatch
		,CT Ct
	);


	/// 不預編譯。適用于況芝 在事務中 初建表後即添數據
	[Obsolete]
	public Task<Func<
		IEnumerable<TEntity>
		,CT
		,Task<nil>
	>> FnInsertManyNoPrepare(
		IDbFnCtx Ctx
		,CT ct
	);


	[Obsolete]
	public Task<Func<
		TId
		,obj?
		,CT
		,Task<nil>
	>> FnUpdOneColById(
		IDbFnCtx Ctx
		,str Col
		,CT Ct
	);


	[Obsolete]
	public Task<Func<
		IList<TVal>
		,CT
		,IAsyncEnumerable<IStr_Any>
	>> FnScltAllByColInVals<TVal>(
		IDbFnCtx Ctx
		,ITable Tbl
		,str CodeCol
		,OptQry? OptQry
		,CT Ct
	);
	
	[Obsolete]
	public Task<Func<
		IList<TCol>
		,CT
		,IAsyncEnumerable<TEntity2>
	>> FnScltAllByColInVals<TEntity2, TCol>(
		IDbFnCtx Ctx
		,ITable Tbl
		,str CodeCol
		,OptQry? OptQry
		,CT Ct
	)
		where TEntity2 : class, new();

}
