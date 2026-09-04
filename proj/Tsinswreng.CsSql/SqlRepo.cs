namespace Tsinswreng.CsSql;

using System.Data;


using Tsinswreng.CsCore;
using Tsinswreng.CsTools;
using Tsinswreng.CsPage;
using System.Collections;
using System.Diagnostics;
using Str_Any = System.Collections.Generic.Dictionary<str, obj?>;
using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;
using Tsinswreng.Srefl;

//using T = Bo_Word;
//TODO 拆分ⁿ使更通用化
//TODO 分頁
public partial class SqlRepo<
	TEntity, TId
>
	:IRepo<TEntity, TId>
	where TEntity: class, new()
{

	public ITblMgr TblMgr{get;set;}
	public ISqlCmdMkr SqlCmdMkr{get;set;}
	public IPropAccessorReg PropAccessorReg{get;set;}

	public SqlRepo(
		ITblMgr TblMgr
		,ISqlCmdMkr SqlCmdMkr
		,IPropAccessorReg PropAccessorReg
	){
		this.PropAccessorReg = PropAccessorReg;
		this.TblMgr = TblMgr;
		this.SqlCmdMkr = SqlCmdMkr;
	}

	public ITable<TEntity> T => TblMgr.GetTbl<TEntity>();

	/// <summary>
	/// Soft-delete filter SQL segment. when <paramref name="WithDel"/> is true, include deleted rows.
	/// </summary>
	private str MkNonDelFilterSql(bool WithDel){
		if(WithDel){
			return "";
		}
		return "\n" + T.AndSqlIsNonDel();
	}

	private IAsyncEnumerable<TEntity?> GetManyInIdCore(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,bool WithDel
		,CT Ct
	){
		IList<IParam> Params = [];
		var sqlD = FnSqlDuplicator.Mk((Cnt)=>{
			Params = T.NumParams(Cnt);
			return
$"""
SELECT * FROM {T.Qt(T.DbTblName)}
WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", Params)}){MkNonDelFilterSql(WithDel)}
""";
		});
		var bat = SqlCmdMkr.AutoBatch<TId, IAsyncEnumerable<TEntity?>>(
			Ctx, sqlD,
			async(z, Ids, Ct)=>{
				var Args = ArgDict.Mk(T).AddManyT(Params, Ids, T.CodeIdName);
				var RawDicts = z.SqlCmd.Args(Args).AsyE1d(Ct);
				return RawDicts.Select(x=>T.DbDictToEntity(x));
			}
		);

		async IAsyncEnumerable<TEntity?> Run(){
			await using var Bat = bat;
			await foreach(var id in Ids.WithCancellation(Ct)){
				var oneBatch = await Bat.Add(id, Ct);
				if(oneBatch is null){
					continue;
				}
				await foreach(var item in oneBatch.WithCancellation(Ct)){
					yield return item;
				}
			}
			var tailBatch = await Bat.End(Ct);
			if(tailBatch is not null){
				await foreach(var item in tailBatch.WithCancellation(Ct)){
					yield return item;
				}
			}
		}

		return Run();
	}

	private IAsyncEnumerable<TEntity?> BatGetByIdCore(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,bool WithDel
		,CT Ct
	){
		var Sql = T.SqlSplicer().Select("*").From().Where1()
		.And().Bool(T.CodeIdName, "=", x=>x.Many(Ids));
		if(!WithDel && T.SoftDelCol is not null){
			Sql.And(T.SoftDelCol.FnSqlIsNonDel());
		}
		var dicts = SqlCmdMkr.RunDupliSql(Ctx, Sql, Ct);
		return dicts.Select(x=>x is null ? null : T.DbDictToEntity<TEntity>(x));
	}

	// ■ _2 對標（SqlMkr 重寫）：同構批量等值查、位置對齊返回。
	// 對照：原 BatGetByIdCore 用 SqlSplicer + Boolean Many binder + RunDupliSql；
	// 新寫法 AndEqEach 收整批 IList（函數邊界 = 批邊界，切批交上游 SqlFlow），
	// Build 產出 N 條等值語句 ';' 拼一命令，泛型 Get1d 逐結果集回讀——外層 IList 對齊入參、空槽 null、
	// 實體轉換（DbDictToEntity）由執行端內部吞掉（T 若是 ITable<TEntity> 時 TEntity 自動推斷）。
	private async Task<IList<TEntity?>> BatGetByIdCore_2(
		IDbFnCtx Ctx, IList<TId> Ids
		,bool WithDel
		,CT Ct
	){
		var Mk = T.SqlMkr().Select("*").From().Where1()
			.AndEqEach(T.CodeIdName, Ids);
		if(!WithDel && T.SoftDelCol is not null){
			Mk.AndSqlIsNonDel();
		}
		var Ans = await SqlCmdMkr.Get1d(Ctx, T, Mk.Build(), Ct)
			.ToListAsync(Ct);
		return Ans;
	}

	private IAsyncEnumerable<TEntity> GetAllCore(
		IDbFnCtx Ctx
		,bool WithDel
		,CT Ct
	){
		async IAsyncEnumerable<TEntity> Run(){
			var sql =
$"""
SELECT * FROM {T.Qt(T.DbTblName)}
WHERE 1=1{MkNonDelFilterSql(WithDel)}
""";
			var cmd = await SqlCmdMkr.MkCmd(Ctx, sql, Ct);
			Ctx.AddToDispose(cmd);
			var rows = cmd
				.AsyE1d(Ct)
				.Select(x=>T.DbDictToEntity<TEntity>(x));
			await foreach(var row in rows.WithCancellation(Ct)){
				yield return row;
			}
		}

		return Run();
	}

	/// 统一生成聚合 include 读取器。
	/// `WithDel=false` 时，按 ORM 默认语义排除软删除资产；`WithDel=true` 时则保留。
	private async Task<Func<IList<TKey>, CT, IAsyncEnumerable<IStr_Any?>>> FnScltAggIncludeByColInVals<TKey>(
		IDbFnCtx Ctx
		,ITable Tbl
		,str CodeCol
		,OptQry? OptQry
		,bool WithDel
		,CT Ct
	)
	{
		var TIncludeTbl = Tbl;
		OptQry ??= new OptQry();
		var numParams = TIncludeTbl.NumParamsEndStart(OptQry.InParamCnt - 1);
		var nonDelSql = !WithDel && TIncludeTbl.SoftDelCol is not null
			? "\nAND " + TIncludeTbl.SqlIsNonDel()
			: "";
		var sql =
$"""
SELECT * FROM {TIncludeTbl.Qt(TIncludeTbl.DbTblName)}
WHERE 1=1
AND {TIncludeTbl.QtCol(CodeCol)} IN ({str.Join(",", numParams)}){nonDelSql}
""";
		var sqlCmd = await SqlCmdMkr.Prepare(Ctx, sql, Ct);
		return (Args, Ct)=>{
			if(Args.Count < numParams.Count){
				throw new Exception("Args.Count < numParams.Count");
			}
			var arg = ArgDict.Mk(TIncludeTbl)
				.AddManyT(numParams, Args);
			return Ctx.RunCmd(sqlCmd, arg).AsyE1d(Ct);
		};
	}

	private IAsyncEnumerable<TAgg> GetAllAggCore<TAgg>(
		IDbFnCtx Ctx
		,bool WithDel
		,CT Ct
	){
		var aggReg = TblMgr.GetAgg<TAgg>();
		if(aggReg.RootEntityType != typeof(TEntity)){
			throw new Exception($"Agg root type mismatch. Agg={typeof(TAgg)}, ExpectedRoot={typeof(TEntity)}, RegisteredRoot={aggReg.RootEntityType}");
		}
		if(aggReg.RootIdType != typeof(TId)){
			throw new Exception($"Agg root id type mismatch. Agg={typeof(TAgg)}, ExpectedId={typeof(TId)}, RegisteredId={aggReg.RootIdType}");
		}

		u64 inBatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 50ul : 500ul;

		async Task<AggQryCtx> LoadAggQryCtx(IList<TId> rootIds, CT Ct){
			var qryCtx = new AggQryCtx();
			if(rootIds.Count == 0){
				return qryCtx;
			}

			var optQry = new OptQry{ InParamCnt = (u64)rootIds.Count };
			foreach(var include in aggReg.Includes){
				var slctByIn = await FnScltAggIncludeByColInVals<TId>(
					Ctx,
					include.Tbl,
					include.FKeyCodeCol,
					optQry,
					WithDel,
					Ct
				);
				var dbAsy = slctByIn(rootIds, Ct);
				await foreach(var dbDict in dbAsy.WithCancellation(Ct)){
					var codeDict = include.Tbl.ToCodeDict(dbDict);
					var entity = include.FnNewEntityObj();
					include.Tbl.AssignEntityByCodeDict(include.EntityType, entity, codeDict);
					var keyObj = include.FnFKeyToRootIdObj(entity);
					if(keyObj is null){
						continue;
					}
					if(include.RelKind == EAggRelKind.OneToOne
						&& qryCtx.GetOne(include.EntityType, keyObj) is not null
					){
						throw new Exception($"OneToOne include got duplicate rows. Agg={typeof(TAgg)}, Include={include.EntityType}, Key={keyObj}");
					}
					qryCtx.Add(include.EntityType, keyObj, entity);
				}
			}
			return qryCtx;
		}

		async IAsyncEnumerable<TAgg> Run(){
			var rootsAsy = GetAllCore(Ctx, WithDel, Ct);
			var rootBatch = new List<TEntity>((i32)inBatchSize);
			var idBatch = new List<TId>((i32)inBatchSize);

			async IAsyncEnumerable<TAgg> FlushBatch(IList<TEntity> roots, IList<TId> ids){
				var qryCtx = await LoadAggQryCtx(ids, Ct);
				foreach(var root in roots){
					var aggObj = aggReg.FnAssembleAggObj(root, qryCtx);
					yield return (TAgg)aggObj;
				}
			}

			await foreach(var root in rootsAsy.WithCancellation(Ct)){
				var keyObj = aggReg.FnGetIdFromRootObj(root);
				if(keyObj is null){
					continue;
				}
				if(keyObj is not TId key){
					throw new Exception($"Agg root key type mismatch. Agg={typeof(TAgg)}, Root={typeof(TEntity)}, Key={keyObj.GetType()}, ExpectedKey={typeof(TId)}");
				}
				rootBatch.Add(root);
				idBatch.Add(key);
				if((u64)rootBatch.Count < inBatchSize){
					continue;
				}

				await foreach(var agg in FlushBatch(rootBatch, idBatch).WithCancellation(Ct)){
					yield return agg;
				}
				rootBatch = new List<TEntity>((i32)inBatchSize);
				idBatch = new List<TId>((i32)inBatchSize);
			}

			if(rootBatch.Count > 0){
				await foreach(var agg in FlushBatch(rootBatch, idBatch).WithCancellation(Ct)){
					yield return agg;
				}
			}
		}

		return Run();
	}

	// ■ 一批聚合查核心(吃 IList)——「一批」的定義:一次完整聚合查的 Id 規模。
// 語義承諾:按入參 OrderedBatchIds 位置對齊組裝聚合(查無補 null、重複 Id 出重複聚合);
// 本方法只負責「這一批怎麼查齊並組裝」,不負責把流切批——切批由調用方(Batches 原語)決定。
// 由原 BatGetAggByIdCore 的閉包 HandleOneBatch 提升而來(樣板 ④),行為不變。
	private async Task<IList<TAgg?>> HandleOneBatch<TAgg>(
		IDbFnCtx Ctx
		,bool WithDel
		,IList<TId> OrderedBatchIds
		,CT Ct
	)
		where TAgg: class
	{
		// 防禦性校驗:聚合註冊表的根類型必須與當前 Repo 的實體/主鍵一致,不一致是註冊期錯誤、立即拋出
		var aggReg = TblMgr.GetAgg<TAgg>();
		if(aggReg.RootEntityType != typeof(TEntity)){
			throw new Exception($"Agg root type mismatch. Agg={typeof(TAgg)}, ExpectedRoot={typeof(TEntity)}, RegisteredRoot={aggReg.RootEntityType}");
		}
		if(aggReg.RootIdType != typeof(TId)){
			throw new Exception($"Agg root id type mismatch. Agg={typeof(TAgg)}, ExpectedId={typeof(TId)}, RegisteredId={aggReg.RootIdType}");
		}

		async IAsyncEnumerable<TId> ToAsyncIds(IEnumerable<TId> Src){
			foreach(var id in Src){
				yield return id;
			}
		}

		// step 1:根實體 IN 查(查無的行自然缺席),建立 id→root 字典與去重後的根 id 集合
		// (用字典是為了 step 3 按入參順序快速回查;查無的 id 不進字典,step 3 補 null)
		var rootsAsy = GetManyInIdCore(Ctx, ToAsyncIds(OrderedBatchIds), WithDel, Ct);
		var rootById = new Dictionary<object, TEntity>();
		var rootIdSet = new HashSet<TId>();
		await foreach(var root in rootsAsy.WithCancellation(Ct)){
			if(root is null){
				continue;
			}
			var keyObj = aggReg.FnGetIdFromRootObj(root);
			if(keyObj is null){
				continue;
			}
			if(keyObj is not TId key){
				throw new Exception($"Agg root key type mismatch. Agg={typeof(TAgg)}, Root={typeof(TEntity)}, Key={keyObj.GetType()}, ExpectedKey={typeof(TId)}");
			}
			rootById[key] = root;
			rootIdSet.Add(key);
		}

		// step 2:include 資產查——只針對 step 1 查到的根(去重集合),逐個 include 表按 FKey IN 取資產
		// OneToOne 資產若同一根查出兩行,視為數據不一致、立即拋出(不返回半吊子聚合)
		var qryCtx = new AggQryCtx();
		if(rootIdSet.Count > 0){
			var rootIds = rootIdSet.ToList();
			var optQry = new OptQry{ InParamCnt = (u64)rootIds.Count };
			foreach(var include in aggReg.Includes){
				var slctByIn = await FnScltAggIncludeByColInVals<TId>(
					Ctx,
					include.Tbl,
					include.FKeyCodeCol,
					optQry,
					WithDel,
					Ct
				);
				var dbAsy = slctByIn(rootIds, Ct);
				await foreach(var dbDict in dbAsy.WithCancellation(Ct)){
					var codeDict = include.Tbl.ToCodeDict(dbDict);
					var entity = include.FnNewEntityObj();
					include.Tbl.AssignEntityByCodeDict(include.EntityType, entity, codeDict);
					var keyObj = include.FnFKeyToRootIdObj(entity);
					if(keyObj is null){
						continue;
					}
					if(include.RelKind == EAggRelKind.OneToOne
						&& qryCtx.GetOne(include.EntityType, keyObj) is not null
					){
						throw new Exception($"OneToOne include got duplicate rows. Agg={typeof(TAgg)}, Include={include.EntityType}, Key={keyObj}");
					}
					qryCtx.Add(include.EntityType, keyObj, entity);
				}
			}
		}

		// step 3:按入參順序組裝——查無的補 null,保證出參與入參位置一一對應(Ord 語義)
		var ans = new List<TAgg?>(OrderedBatchIds.Count);
		foreach(var id in OrderedBatchIds){
			if(!rootById.TryGetValue(id!, out var root)){
				ans.Add(null);
				continue;
			}
			var agg = (TAgg)aggReg.FnAssembleAggObj(root, qryCtx);
			ans.Add(agg);
		}
		return ans;
	}

	// 流式版核心:原手搓分批(Run 循環)整個換成批原語——批大小用 IN 段策略;
	// 每批 = 一次完成「根+全部資產」的聚合查(見 HandleOneBatch),批間出參順序承襲入參批順序。
	private IAsyncEnumerable<TAgg?> BatGetAggByIdCore<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,bool WithDel
		,CT Ct
	)
		where TAgg: class
	{
		return SqlFlow.Batches<TId, TAgg?>(Ids, async(BatIds, Ct2)=>{
			return await HandleOneBatch<TAgg>(Ctx, WithDel, BatIds, Ct2);
		}, Ct, SqlFlow.DfltInBatchSize(TblMgr.DbSrcType));
	}
	
	public IAsyncEnumerable<TEntity?> GetInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	){
		return GetManyInIdCore(Ctx, Ids, false, Ct);
	}

	public IAsyncEnumerable<TEntity?> GetInIdWithDel(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	){
		return GetManyInIdCore(Ctx, Ids, true, Ct);
	}

	public IAsyncEnumerable<TEntity?> GetManyInIds(
		IDbFnCtx Ctx, IEnumerable<TId> Ids
		,CT Ct
	){
		async IAsyncEnumerable<TId> ToAsyE(){
			foreach(var id in Ids){
				yield return id;
			}
		}
		return GetManyInIdCore(Ctx, ToAsyE(), false, Ct);
	}

	public IAsyncEnumerable<TEntity?> OrdGetById(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	){
		return BatGetByIdCore(Ctx, Ids, false, Ct);
	}

	// ■ 批一級形狀(IList 版 OrdGetByIdWithDel,含軟刪):整個 List 一次 IN 查回,位置對齊。
	// 函數邊界 = 批邊界:傳多大的 List 就查多大的 IN(參數規模的兜底分段由執行層負責);
	// 語義承諾:出參與入參一一對應、重複 Id 出重複實體、查無補 null、空列表直接返回空。
	public async Task<IList<TEntity?>> OrdGetByIdWithDel(
		IDbFnCtx Ctx, IList<TId> Ids, CT Ct
	){
		var ans = new List<TEntity?>(Ids.Count);

		// 空列表短路:不發 SQL,直接返回(IN () 在部分 DB 上不合法,故必須提前返回)
		if(Ids.Count == 0){
			return ans;
		}

		// step 1:拼一次 IN 查詢(Many 走同步重載,把整個 List 展成一個 IN 條件)
		var Sql = T.SqlSplicer().Select("*").From().Where1()
		.And().Bool(T.CodeIdName, "=", x=>x.Many(Ids));
		// WithDel=true:含軟刪,故不加非刪過濾。

		// step 2:執行並按入參順序收結果——RunDupliSql 承諾「查無的入參位置補 null」,故直接逐行攤進 ans
		var dicts = SqlCmdMkr.RunDupliSql(Ctx, Sql, Ct);
		await foreach(var dict in dicts.WithCancellation(Ct)){
			ans.Add(dict is null ? null : T.DbDictToEntity<TEntity>(dict));
		}
		return ans;
	}

	// ■ 流式版 OrdGetByIdWithDel:批原語(IN 段批大小)把來源流切塊,逐塊調 IList 版;
	// 出參流與入參流位置一一對應(每個回調內部保持 Ord 補 null 語義,批與批之間保持批序)。
	public IAsyncEnumerable<TEntity?> OrdGetByIdWithDel(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	){
		return SqlFlow.Batches<TId, TEntity?>(Ids, async(BatIds, Ct2)=>{
			return await OrdGetByIdWithDel(Ctx, BatIds, Ct2);
		}, Ct, SqlFlow.DfltInBatchSize(TblMgr.DbSrcType));
	}

	public IAsyncEnumerable<TEntity> GetAll(
		IDbFnCtx Ctx, CT Ct
	){
		return GetAllCore(Ctx, false, Ct);
	}

	public IAsyncEnumerable<TEntity> GetAllWithDel(
		IDbFnCtx Ctx, CT Ct
	){
		return GetAllCore(Ctx, true, Ct);
	}
	
	public IAsyncEnumerable<TAgg> GetAllAgg<TAgg>(
		IDbFnCtx Ctx, CT Ct
	){
		return GetAllAggCore<TAgg>(Ctx, false, Ct);
	}

	public IAsyncEnumerable<TAgg> GetAllAggWithDel<TAgg>(
		IDbFnCtx Ctx, CT Ct
	){
		return GetAllAggCore<TAgg>(Ctx, true, Ct);
	}

	public IAsyncEnumerable<TAgg?> OrdGetAggById<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	)
		where TAgg: class
	{
		return BatGetAggByIdCore<TAgg>(Ctx, Ids, false, Ct);
	}

	// ■ 批一級形狀(IList 版 OrdGetAggByIdWithDel):交給「一批聚合查核心」整批一次完成(含軟刪)。
	// 函數邊界 = 批邊界:內部零分批,傳多大的 List 就做多大的聚合查(IN 段規模由核心/執行層兜底)。
	// 語義:位置一一對應、查無補 null(見 HandleOneBatch 的三步與 step 3 的組裝規則)。
	public async Task<IList<TAgg?>> OrdGetAggByIdWithDel<TAgg>(
		IDbFnCtx Ctx, IList<TId> Ids, CT Ct
	)
		where TAgg: class
	{
		return await HandleOneBatch<TAgg>(Ctx, true, Ids, Ct);
	}

	// 流式版 OrdGetAggByIdWithDel:經 BatGetAggByIdCore(Batches 原語切批)逐批完成聚合查。
	public IAsyncEnumerable<TAgg?> OrdGetAggByIdWithDel<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	)
		where TAgg: class
	{
		return BatGetAggByIdCore<TAgg>(Ctx, Ids, true, Ct);
	}




	

	public delegate Task<IDictionary<TKey, IList<TPo>>> TFnIncludeEntitysByKeys<TKey, TPo>(
		ITable Tbl, Func<TPo, TKey> FnMemb, IEnumerable<TKey?> Keys, CT Ct
	);

/*
Func<
		ITable, Func<TPo, TKey>, IEnumerable<TKey>
		,CT, Task<IDictionary<TKey, IList<TPo>>>
	>
 */
	protected async Task<TFnIncludeEntitysByKeys<TKey, TPo>> FnIncludeEntitysByKeys<TPo, TKey>(
		IDbFnCtx Ctx
		,ITable Tbl
		,str CodeCol
		,OptQry? OptQry
		,CT Ct
	)where TPo: new(){
		var WithDel = OptQry?.IncludeDeleted ?? false;
		var fn = await FnScltAggIncludeByColInVals<TKey>(
			Ctx,
			Tbl,
			CodeCol,
			OptQry,
			WithDel,
			Ct
		);
		return async(Tbl, Memb, Keys, Ct)=>{
			IList<TKey> KeyList = Keys.Where(x=>x is not null).ToList()!;
			var poPage = fn(KeyList, Ct);
			var dicts = await poPage.ToListAsync(Ct);
			var pos = dicts
				.Where(x=>x is not null)
				.Select(x=>Tbl.DbDictToEntity<TPo>(x!));
			IDictionary<TKey, IList<TPo>> posByKey = pos.GroupBy(Memb).ToDictionary(g=>g.Key, g=>(IList<TPo>)g.ToList());
			return posByKey;
		};
	}

	public async Task<IDictionary<TKey, IList<TPo>>> IncludeEntitysByKeys<TPo, TKey>(
		IDbFnCtx Ctx
		,str CodeCol
		,OptQry? OptQry
		,IEnumerable<TKey?> Keys
		,Func<TPo, TKey> FnMemb
		,ITable Tbl
		,CT Ct
	)where TPo: new(){
		return await IncludeEntitysByKeysCore(
			Ctx,
			CodeCol,
			OptQry,
			Keys,
			FnMemb,
			Tbl,
			Ct
		);
	}

	public async Task<IDictionary<TKey, IList<TPo>>> IncludeEntitysByKeys<TPo, TKey>(
		IDbFnCtx Ctx
		,str CodeCol
		,OptQry? OptQry
		,IEnumerable<TKey?> Keys
		,Func<TPo, TKey> FnMemb
		,ITable<TPo> Tbl
		,CT Ct
	)where TPo: new(){
		return await IncludeEntitysByKeysCore(
			Ctx,
			CodeCol,
			OptQry,
			Keys,
			FnMemb,
			Tbl,
			Ct
		);
	}

	/// 執行兩個 IncludeEntitysByKeys overload 的共用流程。
	/// 呼叫者只需提供 key；查詢參數數量由實際的非 null key 數自動決定。
	private async Task<IDictionary<TKey, IList<TPo>>> IncludeEntitysByKeysCore<TPo, TKey>(
		IDbFnCtx Ctx
		,str CodeCol
		,OptQry? OptQry
		,IEnumerable<TKey?> Keys
		,Func<TPo, TKey> FnMemb
		,ITable Tbl
		,CT Ct
	)where TPo: new(){
		// 先過濾並固定輸入，避免可迭代集合被多次消費，也讓 SQL 參數數量準確匹配。
		IList<TKey> KeyList = Keys.Where(X=>X is not null).ToList()!;
		if(KeyList.Count == 0){
			return new Dictionary<TKey, IList<TPo>>();
		}

		// record copy 保留 IncludeDeleted 等選項，只覆蓋由本函數負責推導的參數數量。
		var EffectiveOptQry = (OptQry ?? new OptQry()) with{
			InParamCnt = (u64)KeyList.Count,
		};
		var Fn = await FnIncludeEntitysByKeys<TPo, TKey>(
			Ctx,
			Tbl,
			CodeCol,
			EffectiveOptQry,
			Ct
		);
		return await Fn(Tbl, FnMemb, KeyList, Ct);
	}

		// ■ 批內核心(一批 = 一次同構批量 INSERT):自原 BatchCollector 回調體原樣提升,吃 IList(樣板 ①)。
	// 「一批」的粒度語義:進來的 List 有多長,就拼多長的同構批量 SQL、一次命令寫完。
	// 它不知道批大小/切批——那是調用方(原語)的職責;它只承諾「把我拿到的這批原子地寫完」。
	// SQL 不走手拼:多值組 INSERT 子句由庫工具 `InsertManyClause` 生成
	// (參數名自動帶 `__組號` 後綴,如 @Word__0/@Word__1),綁參用同源的 `NumFieldParam` 對齊。
	private async Task<nil> BatOrdAddCore(IDbFnCtx Ctx, IList<TEntity> BatEnts, CT Ct){
		var Cnt = (u64)BatEnts.Count;

		// 拼 SQL:一行多值組 INSERT(庫生成子句,不再手拼 VALUES 循環)
		var Sql = $"INSERT INTO {T.Qt(T.DbTblName)} {T.InsertManyClause(T.Columns.Keys, Cnt)}";

		// 綁參:與 InsertManyClause 的佔位(`NumFieldParam(field, i)` = `field__i`)同源,按名對齊
		var Arg = new Dictionary<str, obj?>();
		for(i32 i = 0; i < BatEnts.Count; i++){
			var Ent = BatEnts[i];
			var DbDict = T.ToDbDict(T.EntityToCodeDict(Ent));
			foreach(var (k, v) in DbDict){
				Arg[T.NumFieldParam(k, (u64)i).Name] = v;
			}
		}

		// 每批新建命令:reader 消費完會 Dispose 命令(AsyE2d 的 DisposableList),跨批復用緩存命令在 pg 上會崩
		var Cmd = await SqlCmdMkr.Prepare(Ctx, Sql, Ct);
		Ctx.AddToDispose(Cmd);
		await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
		return NIL;
	}

	// ■ _2 對標（SqlMkr 重寫）：同構批量 INSERT。
	// 對照：原 BatOrdAddCore 手拼 InsertManyClause + NumFieldParam 逐格綁參；
	// 新寫法 Insert 一次聲明列集、AddRows 收整批 CodeDict（函數只管一批），
	// 列引用/引號/Upper→Raw/參數後綴全部由 SqlMkr 內部完成，執行端一句 Run 落庫。
	private async Task<nil> BatOrdAddCore_2(IDbFnCtx Ctx, IList<TEntity> BatEnts, CT Ct){
		var Rows = BatEnts.Select(x=>T.EntityToCodeDict(x)).ToList();
		var EtArg = T.SqlMkr().Insert(T.Columns.Keys).AddRows(Rows).Build();
		await SqlCmdMkr.Run(Ctx, EtArg, Ct);
		return NIL;
	}

	// ■ 批一級形狀(IList 版 OrdAdd):函數邊界 = 批邊界——把傳進來的整個 List 一次同構批量寫完。
	// 語義承諾:返回 = 本批全部生效(約束衝突直接拋);空列表視為無操作、返回成功。
	public async Task<IRespBatInsert> OrdAdd(IDbFnCtx Ctx, IList<TEntity> Ents, CT Ct){
		if(Ents.Count > 0){
			await BatOrdAddCore(Ctx, Ents, Ct);
		}
		return new RespBatInsert();
	}

	// ■ 流式版 OrdAdd:批原語(批大小 = 庫默認 sqlite 1 / pg 500)把來源流切塊,
	// 逐塊交給批內核心(Hence 每個事務內等價於原 BatchCollector 行為:sqlite 一單元一寫、pg 一單元 500 寫)。
	public async Task<IRespBatInsert> OrdAdd(IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct){
		await SqlFlow.BatchesInOnly(Ents, (BatEnts, Ct2)=> BatOrdAddCore(Ctx, BatEnts, Ct2), Ct, SqlFlow.DfltBatchSize(TblMgr.DbSrcType));
		return new RespBatInsert();
	}

	public async Task<IRespBatUpd> OrdUpd(IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct){
		var fieldsToUpdate = T.Columns.Keys.Where(x=>x != T.CodeIdName).ToList();
		if(fieldsToUpdate.Count == 0){
			return new RespUpd();
		}

		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;

		str MkSql(u64 Cnt){
			var Stmts = new List<str>((i32)Cnt);
			foreach(var i in Enumerable.Range(0, (i32)Cnt)){
				var Idx = (u64)i;
				var Clause = str.Join(", ", fieldsToUpdate.Select(x=>$"{T.QtCol(x)} = {T.NumFieldParam(x, Idx)}"));
				var PId = T.NumFieldParam(T.CodeIdName, Idx);
				Stmts.Add($"UPDATE {T.Qt(T.DbTblName)} SET {Clause} WHERE {T.QtCol(T.CodeIdName)} = {PId}");
			}
			return str.Join(";\n", Stmts);
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			// 每批新建命令:reader 消費完會 Dispose 命令(AsyE2d 的 DisposableList),跨批復用緩存命令在 pg 上會崩
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			return Cmd;
		}

		await using var Batch = new BatchCollector<TEntity, nil>(async(BatchEnts, Ct)=>{
			var Cnt = (u64)BatchEnts.Count;
			var Arg = new Dictionary<str, obj?>();
			for(i32 i = 0; i < BatchEnts.Count; i++){
				var Ent = BatchEnts[i];
				var DbDict = T.ToDbDict(T.EntityToCodeDict(Ent));
				foreach(var Col in fieldsToUpdate){
					if(DbDict.TryGetValue(Col, out var Val)){
						Arg[T.NumFieldParam(Col, (u64)i).Name] = Val;
					}
				}
				Arg[T.NumFieldParam(T.CodeIdName, (u64)i).Name] = DbDict[T.CodeIdName];
			}
			var Cmd = await GetCmd(Cnt, Ct);
			await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}, BatchSize);

		await foreach(var Ent in Ents.WithCancellation(Ct)){
			await Batch.Add(Ent, Ct);
		}
		await Batch.End(Ct);

		return new RespUpd();
	}

	// ■ 批內核心(一批 Update by Db Dict):自原 BatchCollector 回調體原樣提升,吃 IList(樣板 ③)。
	// 入參是成對的 (Dict, Id) 列表;「一批」= 把每對拼成一條 UPDATE,同一次命令執行完。
	//
	// ★ 支持異構字典:每對 Dict 的鍵集(要更新的列集)可以互不相同——
	//   例:[{A,B},{A},{B,C}] 是合法的,每行按自己有的列生成 SET 子句、缺的列不動。
	//   這是有意保留的語義(原實現即如此,業務上批量更新「列集不一」的場景需要它)。
	//
	// ★ 爲甚麼本方法必須逐對手拼 SQL、不能走 AutoBatch + FnSqlDuplicator 的模板重複:
	//   - AutoBatch/Duplicator 的模型是「同一條 SQL 模板按批大小重複 N 份」,只支持同構
	//     (每份的 SET 列集必須一致);
	//   - 模板化時,生成 SQL 的規則只被告知「本批有幾個元素」(Size),不知道每份要更新哪些列;
	//   - 異構下每條 UPDATE 的 SET 子句列集不同,只有等整批元素到手、逐對看過 Dict 鍵集
	//     才能拼出本批的 SQL——因此「收集、切批、尾批」等一批的骨架交給原語/BatchCollector,
	//     而「本批 SQL 文本 + 參數」這部分必須手拼,這是異構語義的必然形狀,不是重複造輪子。
	//   - SET 段庫工具有 `ITable.UpdateClause(fields)`,但它生成的是無序前綴的 `col = @col`,
	//     定位在「單條語句的 SET」;本方法把 N 對合併進同一個多語句命令,參數名必須帶對序
	//     前綴(u_{i}_ / id_{i})才能避免跨對衝突 → UpdateClause 佔不上去。列引用仍用庫的
	//     QtCol、參數名用 Prm,不是裸拼字符串。
	//   若日後業務確認「同構」即可(每批鍵集一致),可整段換成 AutoBatch + 模板 duplicator 簡化。
	//
	// 語義承諾:數據列為空的對被跳過;寫不寫一律拚入同一命令(原子落地或整體拋錯)。
	private async Task<nil> BatOrdUpdByDbDictCore(
		IDbFnCtx Ctx, IList<(IStr_Any Dict, TId Id)> BatchItems, CT Ct
	){
		var dbIdColName = T.DbColName(T.CodeIdName);

		async Task<ISqlCmd> GetCmd(str Sql, CT Ct){
			// 每批新建命令:reader 消費完會 Dispose 命令,跨批復用緩存命令在 pg 上會崩
			var Cmd = await SqlCmdMkr.Prepare(Ctx, Sql, Ct);
			Ctx.AddToDispose(Cmd);
			return Cmd;
		}

		var Stmts = new List<str>(BatchItems.Count);
		var Arg = new Dictionary<str, obj?>();

		// step 1:逐對拼 UPDATE——每對一個參數前綴(u_{i}_ / id_{i}),避免跨對參數名衝突
		// (庫的 UpdateClause 無對序前綴、只夠單條語句;異構又阻斷模板偏移,理由見方法頭註)
		for(i32 i = 0; i < BatchItems.Count; i++){
			var (DbDict, Id) = BatchItems[i];
			var SetSegs = new List<str>();
			i32 j = 0;
			// 只取數據列:主鍵列(無論 Db 拼法還是代碼拼法)不允許出現在 SET 裏
			foreach(var (DbColName, RawVal) in DbDict){
				if(DbColName == dbIdColName || DbColName == T.CodeIdName){
					continue;
				}
				var P = T.Prm($"u_{i}_{j}");
				SetSegs.Add($"{T.Qt(DbColName)} = {P}");
				Arg[P.Name] = RawVal;
				j++;
			}

			// 全空 Dict(沒有可更新列):整對跳過,不產出 UPDATE 也不報錯
			if(SetSegs.Count == 0){
				continue;
			}

			var PId = T.Prm($"id_{i}");
			Arg[PId.Name] = T.UpperToRaw(Id, T.CodeIdName);
			var Clause = str.Join(", ", SetSegs);
			Stmts.Add($"UPDATE {T.Qt(T.DbTblName)} SET {Clause} WHERE {T.QtCol(T.CodeIdName)} = {PId}");
		}

		// 沒有可更新的對:空操作返回(不發 SQL)
		if(Stmts.Count == 0){
			return NIL;
		}

		// step 2:全部 UPDATE 拼成一條命令,一次執行(本批在此落地)
		var Sql = str.Join(";\n", Stmts);
		var Cmd2 = await GetCmd(Sql, Ct);
		await Cmd2.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
		return NIL;
	}

	// ■ _2 對標（SqlMkr 重寫）：異構字典 UPDATE。
	// 對照：原 BatOrdUpdByDbDictCore 手拼 `u_{i}_{j}`/`id_{i}` 前綴逐對拼 SET 段（異構語義的必然形狀）；
	// 新寫法 Update().AddRows 收整批 (Ids, CodeDicts)，異構列集仍逐對自帶（鍵集各異），
	// 對序前綴/SET 段/WHERE 段/Upper→Raw 全部由 SqlMkr 內部吸收，執行端一句 Run 落庫。
	private async Task<nil> BatOrdUpdByDbDictCore_2(
		IDbFnCtx Ctx, IList<(IStr_Any Dict, TId Id)> BatchItems, CT Ct
	){
		var Ids = new List<obj?>(BatchItems.Count);
		var Dicts = new List<IStr_Any>(BatchItems.Count);
		foreach(var (Dict, Id) in BatchItems){
			Ids.Add(Id);
			Dicts.Add(Dict);
		}
		var EtArg = T.SqlMkr().Update().AddRows(T.CodeIdName, Ids, Dicts).Build();
		await SqlCmdMkr.Run(Ctx, EtArg, Ct);
		return NIL;
	}

	// ■ 批一級形狀(IList 版 OrdUpdByDbDict):Ids 與 Dicts 成對,整批一次 UPDATE 寫完。
	// 語義承諾:
	// - Ids 與 Dicts 長度不等即拋 ArgumentException(不執行任何 UPDATE);
	// - 支持異構字典:每對 Dict 的鍵集可不同,各行按自己有的列更新(詳見 BatOrdUpdByDbDictCore 頭註);
	// - 批大小/切批不在此層(交由原語/調用方)。
	public async Task<IRespBatUpd> OrdUpdByDbDict(
		IDbFnCtx Ctx, IList<TId> Ids, IList<IStr_Any> Dicts, CT Ct
	){
		// 入參一致性校驗:兩列表必須逐位對應,否則拒絕執行
		if(Ids.Count != Dicts.Count){
			throw new ArgumentException("Dicts count must equal to Ids count");
		}

		// 成對組裝後交給批內核心(逐對拼 SQL 的細節在核心內)
		var Pairs = new List<(IStr_Any Dict, TId Id)>(Ids.Count);
		for(i32 i = 0; i < Ids.Count; i++){
			Pairs.Add((Dicts[i], Ids[i]));
		}
		if(Pairs.Count > 0){
			await BatOrdUpdByDbDictCore(Ctx, Pairs, Ct);
		}
		return new RespUpd();
	}

	// ■ 流式版 OrdUpdByDbDict:兩條來源流先按位 zip 成對(長度不等即拋,防錯位),
	// 再交給批原語切塊、逐塊調批內核心——雙流成對的語義在 zip 層保證,原語只管切批。
	public async Task<IRespBatUpd> OrdUpdByDbDict(
		IDbFnCtx Ctx
		,IAsyncEnumerable<TId> Ids
		,IAsyncEnumerable<IStr_Any> Dicts
		,CT Ct
	){
		await using var DictEtor = Dicts.GetAsyncEnumerator(Ct);
		await using var IdEtor = Ids.GetAsyncEnumerator(Ct);

		// 惰性 zip:每次同時推進兩個枚舉器,任一流先結束即視為長度不等、拋錯
		async IAsyncEnumerable<(IStr_Any Dict, TId Id)> Zip(){
			while(true){
				var HasDict = await DictEtor.MoveNextAsync();
				var HasId = await IdEtor.MoveNextAsync();
				if(HasDict != HasId){
					throw new ArgumentException("Dicts count must equal to Ids count");
				}
				if(!HasDict){
					break;
				}
				yield return (DictEtor.Current, IdEtor.Current);
			}
		}

		await SqlFlow.BatchesInOnly(Zip(), (Items, Ct2)=> BatOrdUpdByDbDictCore(Ctx, Items, Ct2), Ct, SqlFlow.DfltBatchSize(TblMgr.DbSrcType));
		return new RespUpd();
	}
	
	public Task<IRespBatUpd> OrdUpdByCodeDict(
		IDbFnCtx Ctx
		,IAsyncEnumerable<TId> Ids
		,IAsyncEnumerable<IStr_Any> Dicts
		,CT Ct
	){
		var DbDicts = Dicts.Select(x=>T.ToDbDict(x));
		return OrdUpdByDbDict(Ctx, Ids, DbDicts, Ct);
	}

	public async Task<ISoftDelInId> SoftDelInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	){
		if(T.SoftDelCol is null){
			throw new Exception("SoftDeleteCol is null");
		}
		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 50ul : 500ul; //TODO
		var valToSet = T.SoftDelCol.FnDelete(null);

		str MkSql(u64 Cnt){
			var IdParams = T.NumParams(Cnt).ToList();
			var PSoft = T.Prm("__SoftDelVal");
			return $"UPDATE {T.Qt(T.DbTblName)} SET {T.QtCol(T.SoftDelCol.CodeColName)} = {PSoft} WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", IdParams)})";
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			// 每批新建命令:reader 消費完會 Dispose 命令(AsyE2d 的 DisposableList),跨批復用緩存命令在 pg 上會崩
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			return Cmd;
		}

		await using var Batch = new BatchCollector<TId, nil>(async(BatchIds, Ct)=>{
			var Cnt = (u64)BatchIds.Count;
			var IdParams = T.NumParams(Cnt).ToList();
			var Arg = ArgDict.Mk(T)
				.AddManyT(IdParams, BatchIds, T.CodeIdName)
				.AddRaw(T.Prm("__SoftDelVal"), valToSet)
				.ToDict();
			var Cmd = await GetCmd(Cnt, Ct);
			await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}, BatchSize);

		await foreach(var Id in Ids.WithCancellation(Ct)){
			await Batch.Add(Id, Ct);
		}
		await Batch.End(Ct);
		return new SoftDelInId();
	}

	// ■ _2 對標（SqlMkr 重寫）：軟刪 IN。
	// 對照：原 SoftDelInId 手拼 `UPDATE ... SET {SoftDelCol} = @__SoftDelVal WHERE Id IN (...)`，
	//       批大小(sqlite 50/pg 500)由庫層寫死在此方法內；
	// 新寫法 Delete().SoftIn 一語收口：軟刪值取 SoftDelCol.FnDelete(null) 在 SqlMkr 內部完成，
	//       函數只管一批 IList（函數邊界 = 批邊界），批大小改由最源頭調用方（SqlFlow）決定，此處不再有 BatchCollector。
	public async Task<nil> SoftDelInId_2(
		IDbFnCtx Ctx, IList<TId> Ids, CT Ct
	){
		if(Ids.Count == 0){
			return NIL;
		}
		var EtArg = T.SqlMkr().Delete().SoftIn(T.CodeIdName, Ids).Build();
		await SqlCmdMkr.Run(Ctx, EtArg, Ct);
		return NIL;
	}

	public async Task<IHardDelInId> HardDelInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	){
		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 50ul : 500ul;
		str MkSql(u64 Cnt){
			var IdParams = T.NumParams(Cnt).ToList();
			return $"DELETE FROM {T.Qt(T.DbTblName)} WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", IdParams)})";
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			// 每批新建命令:reader 消費完會 Dispose 命令(AsyE2d 的 DisposableList),跨批復用緩存命令在 pg 上會崩
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			return Cmd;
		}

		await using var Batch = new BatchCollector<TId, nil>(async(BatchIds, Ct)=>{
			var Cnt = (u64)BatchIds.Count;
			var IdParams = T.NumParams(Cnt).ToList();
			var Arg = ArgDict.Mk(T).AddManyT(IdParams, BatchIds, T.CodeIdName).ToDict();
			var Cmd = await GetCmd(Cnt, Ct);
			await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}, BatchSize);

		await foreach(var Id in Ids.WithCancellation(Ct)){
			await Batch.Add(Id, Ct);
		}
		await Batch.End(Ct);
		return new HardDelInId();
	}

	public async Task<IBatSoftDel> OrdSoftDelById(IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct){
		if(T.SoftDelCol is null){
			throw new Exception("SoftDeleteCol is null");
		}

		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;
		var valToSet = T.SoftDelCol.FnDelete(null);

		str MkSql(u64 Cnt){
			var IdParams = T.NumParams(Cnt).ToList();
			var PSoft = T.Prm("__SoftDelVal");
			return $"UPDATE {T.Qt(T.DbTblName)} SET {T.QtCol(T.SoftDelCol.CodeColName)} = {PSoft} WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", IdParams)})";
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			// 每批新建命令:reader 消費完會 Dispose 命令(AsyE2d 的 DisposableList),跨批復用緩存命令在 pg 上會崩
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			return Cmd;
		}

		await using var Batch = new BatchCollector<TId, nil>(async(BatchIds, Ct)=>{
			var Cnt = (u64)BatchIds.Count;
			var Arg = new Dictionary<str, obj?>();
			var IdParams = T.NumParams(Cnt).ToList();
			Arg[T.Prm("__SoftDelVal").Name] = valToSet;
			for(i32 i = 0; i < BatchIds.Count; i++){
				Arg[IdParams[i].Name] = T.UpperToRaw(BatchIds[i], T.CodeIdName);
			}
			var Cmd = await GetCmd(Cnt, Ct);
			await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}, BatchSize);

		await foreach(var Id in Ids.WithCancellation(Ct)){
			await Batch.Add(Id, Ct);
		}
		await Batch.End(Ct);

		return new BatSoftDel();
	}

	public async Task<IBatHardDel> OrdHardDelById(IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct){
		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;

		str MkSql(u64 Cnt){
			var IdParams = T.NumParams(Cnt).ToList();
			return $"DELETE FROM {T.Qt(T.DbTblName)} WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", IdParams)})";
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			// 每批新建命令:reader 消費完會 Dispose 命令(AsyE2d 的 DisposableList),跨批復用緩存命令在 pg 上會崩
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			return Cmd;
		}

		await using var Batch = new BatchCollector<TId, nil>(async(BatchIds, Ct)=>{
			var Cnt = (u64)BatchIds.Count;
			var Arg = new Dictionary<str, obj?>();
			var IdParams = T.NumParams(Cnt).ToList();
			for(i32 i = 0; i < BatchIds.Count; i++){
				Arg[IdParams[i].Name] = T.UpperToRaw(BatchIds[i], T.CodeIdName);
			}
			var Cmd = await GetCmd(Cnt, Ct);
			await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}, BatchSize);

		await foreach(var Id in Ids.WithCancellation(Ct)){
			await Batch.Add(Id, Ct);
		}
		await Batch.End(Ct);

		return new BatHardDel();
	}

	public async Task<IRespBatAddAgg> OrdAddAgg<TAgg>(IDbFnCtx Ctx, IAsyncEnumerable<TAgg> NewAgg, CT Ct) {
		var aggReg = TblMgr.GetAgg<TAgg>();
		if(aggReg.RootEntityType != typeof(TEntity)){
			throw new Exception($"Agg root type mismatch. Agg={typeof(TAgg)}, ExpectedRoot={typeof(TEntity)}, RegisteredRoot={aggReg.RootEntityType}");
		}
		if(aggReg.RootIdType != typeof(TId)){
			throw new Exception($"Agg root id type mismatch. Agg={typeof(TAgg)}, ExpectedId={typeof(TId)}, RegisteredId={aggReg.RootIdType}");
		}
		if(!PropAccessorReg.Type_PropAccessor.TryGetValue(typeof(TAgg), out var aggAccessor)){
			throw new Exception($"No {nameof(IPropAccessor)} registered for aggregate type: {typeof(TAgg)}");
		}

		var includeTypeInclude = new Dictionary<Type, IAggIncludeReg>();
		foreach(var include in aggReg.Includes){
			if(includeTypeInclude.ContainsKey(include.EntityType)){
				throw new Exception($"BatAddAgg requires unique include entity type. Agg={typeof(TAgg)}, IncludeType={include.EntityType}");
			}
			includeTypeInclude[include.EntityType] = include;
		}

		u64 batchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;
		var rootCols = T.Columns.Keys.ToList();
		var includeColsByType = new Dictionary<Type, IList<str>>();

		str MkInsertSql(ITable tbl, IList<str> cols, u64 cnt){
			var stmts = new List<str>((i32)cnt);
			foreach(var i in Enumerable.Range(0, (i32)cnt)){
				var idx = (u64)i;
				var fields = str.Join(", ", cols.Select(x=>tbl.QtCol(x)));
				var values = str.Join(", ", cols.Select(x=>tbl.NumFieldParam(x, idx).ToString()));
				stmts.Add($"INSERT INTO {tbl.Qt(tbl.DbTblName)} ({fields}) VALUES ({values})");
			}
			return str.Join(";\n", stmts);
		}

		async Task<ISqlCmd> GetRootCmd(u64 cnt, CT Ct){
			// 每批新建命令:reader 消費完會 Dispose 命令,跨批復用緩存命令在 pg 上會崩
			var cmd = await SqlCmdMkr.Prepare(Ctx, MkInsertSql(T, rootCols, cnt), Ct);
			Ctx.AddToDispose(cmd);
			return cmd;
		}

		async Task<ISqlCmd> GetIncludeCmd(IAggIncludeReg include, u64 cnt, CT Ct){
			if(!includeColsByType.TryGetValue(include.EntityType, out var cols)){
				cols = include.Tbl.Columns.Keys.ToList();
				includeColsByType[include.EntityType] = cols;
			}
			// 每批新建命令:reader 消費完會 Dispose 命令,跨批復用緩存命令在 pg 上會崩
			var cmd = await SqlCmdMkr.Prepare(Ctx, MkInsertSql(include.Tbl, cols, cnt), Ct);
			Ctx.AddToDispose(cmd);
			return cmd;
		}

		async Task<nil> InsertRoots(IList<TEntity> roots, CT Ct){
			if(roots.Count == 0){
				return NIL;
			}
			var cnt = (u64)roots.Count;
			var arg = new Dictionary<str, obj?>();
			for(i32 i = 0; i < roots.Count; i++){
				var ent = roots[i];
				var dbDict = T.ToDbDict(T.EntityToCodeDict(ent, typeof(TEntity)));
				foreach(var (k, v) in dbDict){
					arg[T.NumFieldParam(k, (u64)i).Name] = v;
				}
			}
			var cmd = await GetRootCmd(cnt, Ct);
			await cmd.RawArgs(arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}

		async Task<nil> InsertInclude(IAggIncludeReg include, IList<obj> ents, CT Ct){
			if(ents.Count == 0){
				return NIL;
			}
			if(!includeColsByType.TryGetValue(include.EntityType, out var cols)){
				cols = include.Tbl.Columns.Keys.ToList();
				includeColsByType[include.EntityType] = cols;
			}

			var cnt = (u64)ents.Count;
			var arg = new Dictionary<str, obj?>();
			for(i32 i = 0; i < ents.Count; i++){
				var ent = ents[i];
				var dbDict = include.Tbl.ToDbDict(include.Tbl.EntityToCodeDict(ent, include.EntityType));
				foreach(var (k, v) in dbDict){
					arg[include.Tbl.NumFieldParam(k, (u64)i).Name] = v;
				}
			}
			var cmd = await GetIncludeCmd(include, cnt, Ct);
			await cmd.RawArgs(arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}

		await using var batch = new BatchCollector<TAgg, nil>(async(batchAgg, Ct)=>{
			var roots = new List<TEntity>(batchAgg.Count);
			var includeRows = new Dictionary<Type, IList<obj>>();
			foreach(var include in aggReg.Includes){
				includeRows[include.EntityType] = new List<obj>();
			}

			foreach(var agg in batchAgg){
				if(agg is null){
					throw new Exception($"Aggregate item is null. Agg={typeof(TAgg)}");
				}
				var aggObj = (obj)agg;

				TEntity? rootEnt = null;
				var oneToOneSeen = new HashSet<Type>();
				foreach(var key in aggAccessor.GetGetterNames(aggObj)){
					if(!aggAccessor.TryGet(aggObj, key, out var val) || val is null){
						continue;
					}

					if(rootEnt is null && aggReg.RootEntityType.IsAssignableFrom(val.GetType())){
						if(val is not TEntity castRoot){
							throw new Exception($"Aggregate root value type mismatch. Agg={typeof(TAgg)}, Root={typeof(TEntity)}, ValueType={val.GetType()}");
						}
						rootEnt = castRoot;
						continue;
					}

					if(val is IEnumerable enumerable && val is not string){
						foreach(var item in enumerable){
							if(item is null){
								continue;
							}
							var itemType = item.GetType();
							var include = aggReg.Includes.FirstOrDefault(x=>x.EntityType.IsAssignableFrom(itemType));
							if(include is null){
								continue;
							}
							if(include.RelKind == EAggRelKind.OneToOne && oneToOneSeen.Contains(include.EntityType)){
								throw new Exception($"OneToOne include got multiple values in same aggregate. Agg={typeof(TAgg)}, Include={include.EntityType}");
							}
							includeRows[include.EntityType].Add(item);
							oneToOneSeen.Add(include.EntityType);
						}
						continue;
					}

					var valType = val.GetType();
					var includeOne = aggReg.Includes.FirstOrDefault(x=>x.EntityType.IsAssignableFrom(valType));
					if(includeOne is null){
						continue;
					}
					if(includeOne.RelKind == EAggRelKind.OneToOne && oneToOneSeen.Contains(includeOne.EntityType)){
						throw new Exception($"OneToOne include got multiple values in same aggregate. Agg={typeof(TAgg)}, Include={includeOne.EntityType}");
					}
					includeRows[includeOne.EntityType].Add(val);
					oneToOneSeen.Add(includeOne.EntityType);
				}

				if(rootEnt is null){
					throw new Exception($"No root entity found in aggregate object. Agg={typeof(TAgg)}, Root={typeof(TEntity)}");
				}
				roots.Add(rootEnt);
			}

			await InsertRoots(roots, Ct);
			foreach(var (includeType, rows) in includeRows){
				if(rows.Count == 0){
					continue;
				}
				if(!includeTypeInclude.TryGetValue(includeType, out var include)){
					continue;
				}
				await InsertInclude(include, rows, Ct);
			}

			return NIL;
		}, batchSize);

		await foreach(var agg in NewAgg.WithCancellation(Ct)){
			await batch.Add(agg, Ct);
		}
		await batch.End(Ct);

		return new RespBatAddAgg();
	}

	private async Task<nil> BatDelAggByIdCore<TAgg>(
		IDbFnCtx Ctx
		,IAsyncEnumerable<TId> Ids
		,bool SoftDelete
		,CT Ct
	){
		var aggReg = TblMgr.GetAgg<TAgg>();
		if(aggReg.RootEntityType != typeof(TEntity)){
			throw new Exception($"Agg root type mismatch. Agg={typeof(TAgg)}, ExpectedRoot={typeof(TEntity)}, RegisteredRoot={aggReg.RootEntityType}");
		}
		if(aggReg.RootIdType != typeof(TId)){
			throw new Exception($"Agg root id type mismatch. Agg={typeof(TAgg)}, ExpectedId={typeof(TId)}, RegisteredId={aggReg.RootIdType}");
		}

		u64 batchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 50ul : 500ul;

		str MkDelSql(ITable tbl, str codeCol, u64 cnt, bool softDelete){
			var idParams = tbl.NumParams(cnt).ToList();
			if(!softDelete){
				return $"DELETE FROM {tbl.Qt(tbl.DbTblName)} WHERE {tbl.QtCol(codeCol)} IN ({str.Join(", ", idParams)})";
			}
			if(tbl.SoftDelCol is null){
				throw new Exception($"SoftDeleteCol is null. Tbl={tbl.DbTblName}, EntityType={tbl.CodeEntityType}");
			}
			var pSoft = tbl.Prm("__SoftDelVal");
			return $"UPDATE {tbl.Qt(tbl.DbTblName)} SET {tbl.QtCol(tbl.SoftDelCol.CodeColName)} = {pSoft} WHERE {tbl.QtCol(codeCol)} IN ({str.Join(", ", idParams)})";
		}

		async Task<ISqlCmd> GetRootCmd(u64 cnt, CT Ct){
			// 每批新建命令:reader 消費完會 Dispose 命令,跨批復用緩存命令在 pg 上會崩
			var cmd = await SqlCmdMkr.Prepare(Ctx, MkDelSql(T, T.CodeIdName, cnt, SoftDelete), Ct);
			Ctx.AddToDispose(cmd);
			return cmd;
		}

		async Task<ISqlCmd> GetIncludeCmd(IAggIncludeReg include, u64 cnt, CT Ct){
			// 每批新建命令:reader 消費完會 Dispose 命令,跨批復用緩存命令在 pg 上會崩
			var cmd = await SqlCmdMkr.Prepare(Ctx, MkDelSql(include.Tbl, include.FKeyCodeCol, cnt, SoftDelete), Ct);
			Ctx.AddToDispose(cmd);
			return cmd;
		}

		IDictionary<str, obj?> MkArg(ITable tbl, str codeCol, IList<TId> batchIds){
			var cnt = (u64)batchIds.Count;
			var idParams = tbl.NumParams(cnt).ToList();
			var arg = ArgDict.Mk(tbl).AddManyT(idParams, batchIds, codeCol).ToDict();
			if(SoftDelete){
				if(tbl.SoftDelCol is null){
					throw new Exception($"SoftDeleteCol is null. Tbl={tbl.DbTblName}, EntityType={tbl.CodeEntityType}");
				}
				arg[tbl.Prm("__SoftDelVal").Name] = tbl.SoftDelCol.FnDelete(null);
			}
			return arg;
		}

		await using var batch = new BatchCollector<TId, nil>(async(batchIds, Ct)=>{
			if(batchIds.Count == 0){
				return NIL;
			}
			var cnt = (u64)batchIds.Count;

			{
				var cmd = await GetRootCmd(cnt, Ct);
				var arg = MkArg(T, T.CodeIdName, batchIds);
				await cmd.RawArgs(arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			}

			foreach(var include in aggReg.Includes){
				var cmd = await GetIncludeCmd(include, cnt, Ct);
				var arg = MkArg(include.Tbl, include.FKeyCodeCol, batchIds);
				await cmd.RawArgs(arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			}

			return NIL;
		}, batchSize);

		await foreach(var id in Ids.WithCancellation(Ct)){
			await batch.Add(id, Ct);
		}
		await batch.End(Ct);
		return NIL;
	}

	public async Task<IRespHardDelAggInId> HardDelAggInId<TAgg>(IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct) {
		await BatDelAggByIdCore<TAgg>(Ctx, Ids, false, Ct);
		return new RespHardDelAggInId();
	}

	public async Task<IRespSoftDelAggInId> SoftDelAggInId<TAgg>(IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct) {
		await BatDelAggByIdCore<TAgg>(Ctx, Ids, true, Ct);
		return new RespSoftDelAggInId();
	}
	
	public Task<IRespBatUpdAgg> OrdHardUpdAgg<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TAgg> Agg, CT Ct
	){
		return BatUpdAggCore(Ctx, Agg, false, Ct);
	}

	public Task<IRespBatUpdAgg> OrdSoftUpdAgg<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TAgg> Agg, CT Ct
	){
		return BatUpdAggCore(Ctx, Agg, true, Ct);
	}
	
	private IAsyncEnumerable<bool> BatExistsByIdCore(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,bool WithDel
		,CT Ct
	){
		var Sql = T.SqlSplicer().Select("*").From().Where1()
		.And().Bool(T.CodeIdName, "=", x=>x.Many(Ids));
		if(!WithDel && T.SoftDelCol is not null){
			Sql.And(T.SoftDelCol.FnSqlIsNonDel());
		}
		var dicts = SqlCmdMkr.RunDupliSql(Ctx, Sql, Ct);
		return dicts.Select(x=>x is not null);
	}

	public IAsyncEnumerable<bool> OrdExistsById(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	){
		return BatExistsByIdCore(Ctx, Ids, false, Ct);
	}

	public IAsyncEnumerable<bool> OrdExistsByIdWithDel(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	){
		return BatExistsByIdCore(Ctx, Ids, true, Ct);
	}
	
	public async Task<IRespBatUpsert> OrdUpsert(
		IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct
	){
		var batchSize = T.DbStuff.DfltOptBatch.DupliSqlBatchSize;
		var batch = new BatchCollector<TEntity, nil>(async(EntList, Ct)=>{
			var ids = EntList.Select(x=>(TId)T.GetEntityId(x)!).ToAsyncEnumerable();
			// Upsert 以主鍵是否已存在為準，軟刪行也必須算存在。
			// 否則同 Id 的軟刪資料會被誤判為「需插入」，最終撞上主鍵唯一約束。
			var existList = BatExistsByIdCore(Ctx, ids, true, Ct);
			var toInsert = new List<TEntity>();
			var toUpdate = new List<TEntity>();
			await foreach(var (i,isExist) in existList.Index()){
				var ent = EntList[i];
				if(isExist){
					toUpdate.Add(ent);
				}else{
					toInsert.Add(ent);
				}
			}
			await OrdAdd(Ctx, ToolAsyE.ToAsyE(toInsert), Ct);
			await OrdUpd(Ctx, ToolAsyE.ToAsyE(toUpdate), Ct);
			return NIL;
		},batchSize);
		await batch.ConsumeAll(Ents, Ct);
		return new RespBatUpsert();
	}
	
	Task<IRespBatUpsert> BatUpsertOld(
		IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct
	){
		u64 batchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;

		async IAsyncEnumerable<TEntity> ToAsyE(IEnumerable<TEntity> src){
			foreach(var one in src){
				yield return one;
			}
		}

		async IAsyncEnumerable<TId> ToAsyEId(IEnumerable<TId> src){
			foreach(var one in src){
				yield return one;
			}
		}

		async Task<IList<bool>> ExistsOneBatch(IList<TId> batchIds, CT Ct){
			if(batchIds.Count == 0){
				return [];
			}
			var ans = new List<bool>(batchIds.Count);
			await foreach(var one in OrdExistsById(Ctx, ToAsyEId(batchIds), Ct).WithCancellation(Ct)){
				ans.Add(one);
			}
			if(ans.Count != batchIds.Count){
				throw new Exception($"{nameof(OrdExistsById)} result count mismatch. Expect={batchIds.Count}, Got={ans.Count}");
			}
			return ans;
		}

		async Task<nil> HandleOneBatch(IList<TEntity> batchEnts, CT Ct){
			if(batchEnts.Count == 0){
				return NIL;
			}

			var ids = new List<TId>(batchEnts.Count);
			foreach(var ent in batchEnts){
				var codeDict = T.EntityToCodeDict(ent, typeof(TEntity));
				if(!codeDict.TryGetValue(T.CodeIdName, out var idObj) || idObj is null){
					throw new Exception($"Entity id is null or missing. Entity={typeof(TEntity)}, IdField={T.CodeIdName}");
				}
				if(idObj is not TId id){
					throw new Exception($"Entity id type mismatch. Entity={typeof(TEntity)}, IdField={T.CodeIdName}, IdType={idObj.GetType()}, Expected={typeof(TId)}");
				}
				ids.Add(id);
			}

			// Upsert 要以主鍵是否存在為準（包含已軟刪資料），避免插入時主鍵衝突。
			var existsFlags = await ExistsOneBatch(ids, Ct);
			var toInsert = new List<TEntity>();
			var toUpdate = new List<TEntity>();
			for(i32 i = 0; i < batchEnts.Count; i++){
				if(existsFlags[i]){
					toUpdate.Add(batchEnts[i]);
				}else{
					toInsert.Add(batchEnts[i]);
				}
			}

			if(toInsert.Count > 0){
				await OrdAdd(Ctx, ToAsyE(toInsert), Ct);
			}
			if(toUpdate.Count > 0){
				await OrdUpd(Ctx, ToAsyE(toUpdate), Ct);
			}

			return NIL;
		}

		return Fn();

		async Task<IRespBatUpsert> Fn(){
			await using var batch = new BatchCollector<TEntity, nil>(HandleOneBatch, batchSize);
			await foreach(var ent in Ents.WithCancellation(Ct)){
				await batch.Add(ent, Ct);
			}
			await batch.End(Ct);
			return new RespBatUpsert();
		}
	}

	private async Task<IRespBatUpdAgg> BatUpdAggCore<TAgg>(
		IDbFnCtx Ctx
		,IAsyncEnumerable<TAgg> Agg
		,bool SoftDeleteMissing
		,CT Ct
	){
		var aggReg = TblMgr.GetAgg<TAgg>();
		if(aggReg.RootEntityType != typeof(TEntity)){
			throw new Exception($"Agg root type mismatch. Agg={typeof(TAgg)}, ExpectedRoot={typeof(TEntity)}, RegisteredRoot={aggReg.RootEntityType}");
		}
		if(aggReg.RootIdType != typeof(TId)){
			throw new Exception($"Agg root id type mismatch. Agg={typeof(TAgg)}, ExpectedId={typeof(TId)}, RegisteredId={aggReg.RootIdType}");
		}
		if(!PropAccessorReg.Type_PropAccessor.TryGetValue(typeof(TAgg), out var aggAccessor)){
			throw new Exception($"No {nameof(IPropAccessor)} registered for aggregate type: {typeof(TAgg)}");
		}

		var includeTypeInclude = new Dictionary<Type, IAggIncludeReg>();
		foreach(var include in aggReg.Includes){
			if(includeTypeInclude.ContainsKey(include.EntityType)){
				throw new Exception($"BatUpdAgg requires unique include entity type. Agg={typeof(TAgg)}, IncludeType={include.EntityType}");
			}
			includeTypeInclude[include.EntityType] = include;
		}

		u64 batchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;
		var includeColsByType = new Dictionary<Type, IList<str>>();
		var includeInsertCmdByTypeCnt = new Dictionary<(Type, u64), ISqlCmd>();
		var includeDelByFKeyCmdByKey = new Dictionary<(Type, bool, u64), ISqlCmd>();
		var includeDelByIdHardCmdByKey = new Dictionary<(Type, u64), ISqlCmd>();

		async IAsyncEnumerable<TItem> ToAsyE<TItem>(IEnumerable<TItem> src){
			foreach(var one in src){
				yield return one;
			}
		}

		str MkInsertSql(ITable tbl, IList<str> cols, u64 cnt){
			var stmts = new List<str>((i32)cnt);
			foreach(var i in Enumerable.Range(0, (i32)cnt)){
				var idx = (u64)i;
				var fields = str.Join(", ", cols.Select(x=>tbl.QtCol(x)));
				var values = str.Join(", ", cols.Select(x=>tbl.NumFieldParam(x, idx).ToString()));
				stmts.Add($"INSERT INTO {tbl.Qt(tbl.DbTblName)} ({fields}) VALUES ({values})");
			}
			return str.Join(";\n", stmts);
		}

		async Task<ISqlCmd> GetIncludeInsertCmd(IAggIncludeReg include, u64 cnt, CT Ct){
			var key = (include.EntityType, cnt);
			if(includeInsertCmdByTypeCnt.TryGetValue(key, out var got)){
				return got;
			}
			if(!includeColsByType.TryGetValue(include.EntityType, out var cols)){
				cols = include.Tbl.Columns.Keys.ToList();
				includeColsByType[include.EntityType] = cols;
			}
			var cmd = await SqlCmdMkr.Prepare(Ctx, MkInsertSql(include.Tbl, cols, cnt), Ct);
			Ctx.AddToDispose(cmd);
			includeInsertCmdByTypeCnt[key] = cmd;
			return cmd;
		}

		async Task<nil> InsertIncludeRows(IAggIncludeReg include, IList<obj> rows, CT Ct){
			if(rows.Count == 0){
				return NIL;
			}
			if(!includeColsByType.TryGetValue(include.EntityType, out var cols)){
				cols = include.Tbl.Columns.Keys.ToList();
				includeColsByType[include.EntityType] = cols;
			}

			var cnt = (u64)rows.Count;
			var arg = new Dictionary<str, obj?>();
			for(i32 i = 0; i < rows.Count; i++){
				var ent = rows[i];
				var dbDict = include.Tbl.ToDbDict(include.Tbl.EntityToCodeDict(ent, include.EntityType));
				foreach(var (k, v) in dbDict){
					arg[include.Tbl.NumFieldParam(k, (u64)i).Name] = v;
				}
			}
			var cmd = await GetIncludeInsertCmd(include, cnt, Ct);
			await cmd.RawArgs(arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}

		str MkDelByColSql(ITable tbl, str codeCol, u64 cnt, bool softDelete){
			var idParams = tbl.NumParams(cnt).ToList();
			if(!softDelete){
				return $"DELETE FROM {tbl.Qt(tbl.DbTblName)} WHERE {tbl.QtCol(codeCol)} IN ({str.Join(", ", idParams)})";
			}
			if(tbl.SoftDelCol is null){
				throw new Exception($"SoftDeleteCol is null. Tbl={tbl.DbTblName}, EntityType={tbl.CodeEntityType}");
			}
			var pSoft = tbl.Prm("__SoftDelVal");
			return $"UPDATE {tbl.Qt(tbl.DbTblName)} SET {tbl.QtCol(tbl.SoftDelCol.CodeColName)} = {pSoft} WHERE {tbl.QtCol(codeCol)} IN ({str.Join(", ", idParams)})";
		}

		async Task<ISqlCmd> GetIncludeDelByFKeyCmd(IAggIncludeReg include, u64 cnt, bool softDelete, CT Ct){
			var key = (include.EntityType, softDelete, cnt);
			if(includeDelByFKeyCmdByKey.TryGetValue(key, out var got)){
				return got;
			}
			var sql = MkDelByColSql(include.Tbl, include.FKeyCodeCol, cnt, softDelete);
			var cmd = await SqlCmdMkr.Prepare(Ctx, sql, Ct);
			Ctx.AddToDispose(cmd);
			includeDelByFKeyCmdByKey[key] = cmd;
			return cmd;
		}

		async Task<ISqlCmd> GetIncludeDelByIdHardCmd(IAggIncludeReg include, u64 cnt, CT Ct){
			var key = (include.EntityType, cnt);
			if(includeDelByIdHardCmdByKey.TryGetValue(key, out var got)){
				return got;
			}
			var sql = MkDelByColSql(include.Tbl, include.Tbl.CodeIdName, cnt, false);
			var cmd = await SqlCmdMkr.Prepare(Ctx, sql, Ct);
			Ctx.AddToDispose(cmd);
			includeDelByIdHardCmdByKey[key] = cmd;
			return cmd;
		}

		IDictionary<str, obj?> MkDelArg(ITable tbl, str codeCol, IList<obj> vals, bool softDelete){
			var cnt = (u64)vals.Count;
			var params_ = tbl.NumParams(cnt).ToList();
			var arg = ArgDict.Mk(tbl).AddManyT(params_, vals, codeCol).ToDict();
			if(softDelete){
				if(tbl.SoftDelCol is null){
					throw new Exception($"SoftDeleteCol is null. Tbl={tbl.DbTblName}, EntityType={tbl.CodeEntityType}");
				}
				arg[tbl.Prm("__SoftDelVal").Name] = tbl.SoftDelCol.FnDelete(null);
			}
			return arg;
		}

		await using var batch = new BatchCollector<TAgg, nil>(async(batchAgg, Ct)=>{
			var roots = new List<TEntity>(batchAgg.Count);
			var rootIds = new List<TId>(batchAgg.Count);
			var rootIdsObj = new List<obj>(batchAgg.Count);
			var includeRows = new Dictionary<Type, IList<obj>>();
			var includeIds = new Dictionary<Type, IList<obj>>();
			foreach(var include in aggReg.Includes){
				includeRows[include.EntityType] = new List<obj>();
				includeIds[include.EntityType] = new List<obj>();
			}

			foreach(var agg in batchAgg){
				if(agg is null){
					throw new Exception($"Aggregate item is null. Agg={typeof(TAgg)}");
				}
				var aggObj = (obj)agg;

				TEntity? rootEnt = null;
				foreach(var key in aggAccessor.GetGetterNames(aggObj)){
					if(!aggAccessor.TryGet(aggObj, key, out var val) || val is null){
						continue;
					}

					if(rootEnt is null && aggReg.RootEntityType.IsAssignableFrom(val.GetType())){
						if(val is not TEntity castRoot){
							throw new Exception($"Aggregate root value type mismatch. Agg={typeof(TAgg)}, Root={typeof(TEntity)}, ValueType={val.GetType()}");
						}
						rootEnt = castRoot;
						continue;
					}

					if(val is IEnumerable enumerable && val is not string){
						foreach(var item in enumerable){
							if(item is null){
								continue;
							}
							var itemType = item.GetType();
							var include = aggReg.Includes.FirstOrDefault(x=>x.EntityType.IsAssignableFrom(itemType));
							if(include is null){
								continue;
							}
							includeRows[include.EntityType].Add(item);
							var codeDict = include.Tbl.EntityToCodeDict(item, include.EntityType);
							if(codeDict.TryGetValue(include.Tbl.CodeIdName, out var idVal) && idVal is not null){
								includeIds[include.EntityType].Add(idVal);
							}
						}
						continue;
					}

					var includeOne = aggReg.Includes.FirstOrDefault(x=>x.EntityType.IsAssignableFrom(val.GetType()));
					if(includeOne is null){
						continue;
					}
					includeRows[includeOne.EntityType].Add(val);
					var codeDictOne = includeOne.Tbl.EntityToCodeDict(val, includeOne.EntityType);
					if(codeDictOne.TryGetValue(includeOne.Tbl.CodeIdName, out var idValOne) && idValOne is not null){
						includeIds[includeOne.EntityType].Add(idValOne);
					}
				}

				if(rootEnt is null){
					throw new Exception($"No root entity found in aggregate object. Agg={typeof(TAgg)}, Root={typeof(TEntity)}");
				}
				roots.Add(rootEnt);
				var rootIdObj = aggReg.FnGetIdFromRootObj(rootEnt);
				if(rootIdObj is not TId rootId){
					throw new Exception($"Agg root key type mismatch. Agg={typeof(TAgg)}, Root={typeof(TEntity)}, Key={rootIdObj?.GetType()}, ExpectedKey={typeof(TId)}");
				}
				rootIds.Add(rootId);
				rootIdsObj.Add(rootId!);
			}

			if(roots.Count == 0){
				return NIL;
			}

			await OrdUpd(Ctx, ToAsyE(roots), Ct);

			foreach(var include in aggReg.Includes){
				var includeType = include.EntityType;

				if(SoftDeleteMissing){
					var idsToInsert = includeIds[includeType];
					if(idsToInsert.Count > 0){
						var cmdDelSameIdHard = await GetIncludeDelByIdHardCmd(include, (u64)idsToInsert.Count, Ct);
						var argDelSameIdHard = MkDelArg(include.Tbl, include.Tbl.CodeIdName, idsToInsert, false);
						await cmdDelSameIdHard.RawArgs(argDelSameIdHard).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
					}
					var cmdDelOldSoft = await GetIncludeDelByFKeyCmd(include, (u64)rootIdsObj.Count, true, Ct);
					var argDelOldSoft = MkDelArg(include.Tbl, include.FKeyCodeCol, rootIdsObj, true);
					await cmdDelOldSoft.RawArgs(argDelOldSoft).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
				}else{
					var cmdDelOldHard = await GetIncludeDelByFKeyCmd(include, (u64)rootIdsObj.Count, false, Ct);
					var argDelOldHard = MkDelArg(include.Tbl, include.FKeyCodeCol, rootIdsObj, false);
					await cmdDelOldHard.RawArgs(argDelOldHard).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
				}

				var rows = includeRows[includeType];
				await InsertIncludeRows(include, rows, Ct);
			}

			return NIL;
		}, batchSize);

		await foreach(var one in Agg.WithCancellation(Ct)){
			await batch.Add(one, Ct);
		}
		await batch.End(Ct);
		return new RespBatUpdAgg();
	}
}
