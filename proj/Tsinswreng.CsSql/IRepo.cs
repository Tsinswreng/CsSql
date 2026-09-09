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
- Update -> Upd  (do not use `Set` because set should map to upsert)
- Delete -> Del

- NOT support auto increment id for insert operation.
- support database-generated id: mark the column with `IsDbGenerated`(via `ColMkr.DbGenerated()`),
  then INSERT skips that column and generated ids are returned in `IRespBatInsert<TId>.GeneratedIds`.
- throw exception if insert or update fails.
- update will match the primary key of the entity as benchmark.

for `Get` operation, defaultly
soft deleted data are not included.

only read operation have counterpart of ReadXxx and `ReadXxxWithDel`
")]
public partial interface IRepo<TEntity, TId>{
	public IAsyncEnumerable<TEntity?> GetInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	);

	[Doc(@$"using `Id IN (...)` Clause,
	which would ignore unexisted Id and returned list may be unordered.
	")]
	public IAsyncEnumerable<TEntity?> GetInIdWithDel(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	);

	public IAsyncEnumerable<TEntity?> OrdGetById(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	);
	
	[Doc(@$"
	#Examples([
	fn(ctx, [existingId, nonExistingId, existingId])
	-> [true, false, true]
	])
	")]
	public IAsyncEnumerable<bool> OrdExistsById(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	);
	
	public IAsyncEnumerable<bool> OrdExistsByIdWithDel(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	);
	

	[Doc(@$"Got Entities are corresponding to the given Ids. if not found, the place will be null.")]
	public IAsyncEnumerable<TEntity?> OrdGetByIdWithDel(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	);

	public IAsyncEnumerable<TEntity> GetAll(
		IDbFnCtx Ctx, CT Ct
	);
	
	public IAsyncEnumerable<TEntity> GetAllWithDel(
		IDbFnCtx Ctx, CT Ct
	);
	
	
	[Doc($$"""
	should throw exception if conflict (e.g constraint violation) etc.
	#Desc[
	//TODO 自增ID, (id)=>{} 回調可在Ctx中註冊 未實現
	]
	""")]
	public Task<IRespBatInsert> OrdAdd(
		IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct
	);
	
	[Doc(@$"by the primary key of the entity,
	so you don't need to provide the entity id independantly.
	should throw exception if conflict (e.g constraint violation) etc.
	")]
	public Task<IRespBatUpd> OrdUpd(
		IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct
	);
	
	
	[Doc(@$"this will not use `UPSERT` sql, but manually insert or update in code.
	soft deleted rows are included to determine whether the data exists or not.
	")]
	public Task<IRespBatUpsert> OrdUpsert(
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
	public Task<IRespBatUpd> OrdUpdByDbDict(
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
	public Task<IRespBatUpd> OrdUpdByCodeDict(
		IDbFnCtx Ctx
		,IAsyncEnumerable<TId> Ids
		,IAsyncEnumerable<IStr_Any> Dicts
		,CT Ct
	);
	
	public Task<IBatSoftDel> OrdSoftDelById(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	);
	
	public Task<IBatHardDel> OrdHardDelById(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	);
	
	public Task<ISoftDelInId> SoftDelInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	);
	
	public Task<IHardDelInId> HardDelInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	);
	
	#region Agg
	
	public Task<IRespBatAddAgg> OrdAddAgg<TAgg>(
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
	
	[Doc(@$"
	Batch select aggregate roots by ids; aggregate metadata should be registered in ITblMgr.AddAgg().
	When method name does NOT contain `WithDel`, both aggregate root and included assets must exclude soft-deleted rows.
	")]
	public IAsyncEnumerable<TAgg?> OrdGetAggById<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	)where TAgg: class;
	
	[Doc(@$"
	Batch select aggregate roots by ids and include soft-deleted rows.
	When method name contains `WithDel`, both aggregate root and included assets are allowed to contain soft-deleted rows.
	")]
	public IAsyncEnumerable<TAgg?> OrdGetAggByIdWithDel<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	)where TAgg: class;
	
	
	[Doc(@$"Hard Delete Both Root and its related assets")]
	public Task<IRespHardDelAggInId> HardDelAggInId<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	);
	
	
	[Doc(@$"Soft Delete Both Root and its related assets,
	if you only need to soft del the root, use {nameof(OrdSoftDelById)} for the root
	")]
	public Task<IRespSoftDelAggInId> SoftDelAggInId<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	);
	
	[Doc(@$"Batch Update Aggregates. make db's data the same as passed-in data
		for each agg, after update,
		use `{nameof(OrdGetAggByIdWithDel)}` will return the updated agg
		as what I passed to `{nameof(OrdHardUpdAgg)}`.
		`Hard` means hard delete one-to-many assets that new agg doesn't have.
	")]
	public Task<IRespBatUpdAgg> OrdHardUpdAgg<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TAgg> Agg, CT Ct
	);
	
	[Doc(@$"Batch Update Aggregates. make db's data the same as passed-in data
		for each agg, after update,
		use `{nameof(OrdGetAggByIdWithDel)}` will return the updated agg
		as what I passed to `{nameof(OrdHardUpdAgg)}`.
		`Soft` means Soft delete one-to-many assets that new agg doesn't have.
	")]
	public Task<IRespBatUpdAgg> OrdSoftUpdAgg<TAgg>(
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

	// -------- 批一級形狀(IList 版):與上方同名 IAsyncEnumerable 版一一對應、語義一致--------
	// 設計:函數邊界 = 批邊界——吃 IList 的函數內部零業務分批,傳多少元素就執行多少
	// (同構批量拼 N 組 SQL / IN 同理),一批的規模上限由執行層(庫原語的默認策略)負責兜底;
	// 流衹出現在調用方來源側,由批原語(SqlFlow.Batches / BatchesInOnly)在調用點切流後逐批調入本段方法。

	#region 樣板:寫·最簡(同構批量 INSERT)

	/// IList 版 OrdAdd:把 List 內的全部實體一次性拼進同構批量 INSERT 並執行完畢。
	/// 語義承諾:
	/// - 函數返回 = 本批全部生效(約束/衝突等異常直接向上拋,不留半批返回的狀態);
	/// - 空列表是合法的無操作(不發 SQL、直接返回成功)。
	[Doc(@$"IList 版:`{nameof(OrdAdd)}` 的批一級形狀。本批全部執行完畢才返回(衝突/約束違反即拋)。")]
	public Task<IRespBatInsert> OrdAdd(
		IDbFnCtx Ctx, IList<TEntity> Ents, CT Ct
	);

	#endregion

	#region 樣板:查·最簡(IN + 位置對齊)

	/// IList 版 OrdGetByIdWithDel:按入參 Id 列表做一次 IN 查詢,含軟刪行。
	/// 語義承諾(與流式版一致):
	/// - 出參 List 與入參 Ids 一一對應(位置對齊):重複的 Id 返回重複的實體;
	/// - 查無的 Id 對應位置補 null;
	/// - 空列表返回空列表(不發 SQL)。
	[Doc(@$"IList 版:`{nameof(OrdGetByIdWithDel)}` 的批一級形狀、含軟刪。
	與入參位置一一對應、查無補 null。")]
	public Task<IList<TEntity?>> OrdGetByIdWithDel(
		IDbFnCtx Ctx, IList<TId> Ids, CT Ct
	);

	#endregion

	#region 樣板:雙參數流(UPDATE by Db Dict,Ids / Dicts 成對)

	/// IList 版 OrdUpdByDbDict:Ids 與 Dicts 成對做 UPDATE(每對一條,拼進同一次命令執行完畢)。
	/// 語義承諾:
	/// - Ids 與 Dicts 長度必須相等,否則拋 ArgumentException(不執行任何 UPDATE);
	/// - 【支持異構字典】每對 Dict 的鍵集可以互不相同——各行只更新自己字典裏出現的列,
	///   缺的列保持原樣。正因如此,本方法的 SQL 無法模板化重複(AutoBatch/FnSqlDuplicator
	///   只適用於「各份 SET 列集一致」的同構批量),實現必須逐對手拼,這是異構語義的必然形狀;
	/// - Dict 以「Db 列名 → 原值」形式給入(列名帶 Db 風格或代碼風格都可,UPPER 轉換由實現處理);
	/// - 空的 Dict(沒有可更新的列)那一對被跳過,不影響其它對。
	[Doc(@$"IList 版:`{nameof(OrdUpdByDbDict)}` 的批一級形狀。Ids 與 Dicts 個數須相等。
	支持異構字典(各行更新的列集可不同)。")]
	public Task<IRespBatUpd> OrdUpdByDbDict(
		IDbFnCtx Ctx, IList<TId> Ids, IList<IStr_Any> Dicts, CT Ct
	);

	#endregion

	#region 樣板:Agg 讀(根 + include 資產,位置對齊)

	/// IList 版 OrdGetAggByIdWithDel:按入參 Id 列表一次裝配聚合(根實體 + 全部 include 資產),含軟刪。
	/// 語義承諾(與流式版一致):
	/// - 出參 List 與入參 Ids 一一對應(位置對齊),查無的 Id 對應位置補 null;
	/// - 每個 Id 只返回一個聚合實例;OneToOne include 若查出重複行會拋異常(數據不一致)。
	[Doc(@$"IList 版:`{nameof(OrdGetAggByIdWithDel)}` 的批一級形狀,含軟刪。位置一一對應、查無補 null。")]
	public Task<IList<TAgg?>> OrdGetAggByIdWithDel<TAgg>(
		IDbFnCtx Ctx, IList<TId> Ids, CT Ct
	)where TAgg: class;

	#endregion

	#region 同構批量 UPDATE(整行覆蓋,含主鍵以外的全部列)

	/// IList 版 OrdUpd:把 List 內全部實體一次性拼進同構批量 UPDATE(按主鍵定位、覆蓋主鍵以外全部列)。
	/// 語義承諾(與流式版一致):本批全部執行完畢才返回;空列表是合法無操作(不發 SQL)。
	[Doc(@$"IList 版:`{nameof(OrdUpd)}` 的批一級形狀。本批全部執行完畢才返回(衝突/約束違反即拋)。")]
	public Task<IRespBatUpd> OrdUpd(
		IDbFnCtx Ctx, IList<TEntity> Ents, CT Ct
	);

	#endregion

	#region Upsert(查存在分插入/更新兩堆)

	/// IList 版 OrdUpsert:一次同構批量完 upsert(逐元素查存在、含軟刪行,分插入/更新兩堆後各走同構批量寫)。
	/// 語義承諾(與流式版一致):以主鍵是否已存在為準(含軟刪行算存在);本批全部執行完畢才返回。
	[Doc(@$"IList 版:`{nameof(OrdUpsert)}` 的批一級形狀。存在判定含軟刪行,本批全部執行完畢才返回。")]
	public Task<IRespBatUpsert> OrdUpsert(
		IDbFnCtx Ctx, IList<TEntity> Ents, CT Ct
	);

	#endregion

	#region UPDATE by Code Dict(Ids / 字典成對)

	/// IList 版 OrdUpdByCodeDict:Ids 與 CodeDicts(代碼風格字典)成對做 UPDATE。
	/// 內部把 CodeDict 轉換成 DbDict 後、複用 {nameof(OrdUpdByDbDict)} 的異構批量語義(同行只更新自己字典裏出現的列)。
	/// 語義承諾(與流式版一致):Ids 與 Dicts 長度必須相等,否則拋 ArgumentException(不執行任何 UPDATE)。
	[Doc(@$"IList 版:`{nameof(OrdUpdByCodeDict)}` 的批一級形狀。Ids 與 Dicts 個數須相等、支持異構字典。")]
	public Task<IRespBatUpd> OrdUpdByCodeDict(
		IDbFnCtx Ctx, IList<TId> Ids, IList<IStr_Any> CodeDicts, CT Ct
	);

	#endregion

	#region 軟刪(單表,IN 語義)

	/// IList 版 SoftDelInId:按入參 Id 列表一次軟刪(單表 UPDATE 軟刪列)。IN 語義:無序、忽略不存在的 Id、可重複。
	/// 語義承諾(與流式版一致):本批全部執行完畢才返回;空列表是合法無操作。
	[Doc(@$"IList 版:`{nameof(SoftDelInId)}` 的批一級形狀。IN 語義(無序/忽略不存在/可重複),本批一次執行完畢。")]
	public Task<ISoftDelInId> SoftDelInId(
		IDbFnCtx Ctx, IList<TId> Ids, CT Ct
	);

	#endregion

	#region 寫向 Agg:聚合級聯插入

	/// IList 版 OrdAddAgg:把 List 內全部聚合一次級聯插入(根 + 全部 include 資產,各自同構批量)。
	/// 語義承諾(與流式版一致):本批全部執行完畢才返回;空列表是合法無操作。
	[Doc(@$"IList 版:`{nameof(OrdAddAgg)}` 的批一級形狀。根與 include 資產各一批同構插入、本批全部執行完畢才返回。")]
	public Task<IRespBatAddAgg> OrdAddAgg<TAgg>(
		IDbFnCtx Ctx, IList<TAgg> Aggs, CT Ct
	);

	#endregion

	#region 寫向 Agg:聚合軟刪(根 + 全部 include 資產聯動,IN 語義)

	/// IList 版 SoftDelAggInId:按入參 Id 列表一次軟刪聚合(根 + 全部 include 資產各自 UPDATE 軟刪列)。IN 語義。
	/// 語義承諾(與流式版一致):本批全部執行完畢才返回;空列表是合法無操作。
	[Doc(@$"IList 版:`{nameof(SoftDelAggInId)}` 的批一級形狀。根與 include 資產聯動軟刪、本批一次執行完畢。")]
	public Task<IRespSoftDelAggInId> SoftDelAggInId<TAgg>(
		IDbFnCtx Ctx, IList<TId> Ids, CT Ct
	);

	#endregion

}



public class IRespBatInsert{
	
}

public class RespBatInsert:IRespBatInsert{}


public class IRespBatUpd{
	
}

public class RespUpd:IRespBatUpd{
	
}

public class IRespBatUpsert{
	
}

public class RespBatUpsert: IRespBatUpsert{
	
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
