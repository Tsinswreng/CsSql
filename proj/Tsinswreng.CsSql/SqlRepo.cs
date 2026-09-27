namespace Tsinswreng.CsSql;

using System.Data;
using System.Runtime.CompilerServices;


using Tsinswreng.CsCore;
using Tsinswreng.CsTools;
using Tsinswreng.CsPage;
using System.Collections;
using System.Diagnostics;
using Str_Any = System.Collections.Generic.Dictionary<str, obj?>;
using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;
using Tsinswreng.CsRefl;

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
	[Doc($@"型別元資料來源；實體與聚合的成員按名讀寫都經它")]
	public ITypeInfoSrc TypeInfoSrc{get;set;}

	/// 倉儲：給表管理器、SQL 命令工廠，與型別元資料來源。
	public SqlRepo(
		ITblMgr TblMgr
		,ISqlCmdMkr SqlCmdMkr
		,ITypeInfoSrc TypeInfoSrc
	){
		this.TypeInfoSrc = TypeInfoSrc;
		this.TblMgr = TblMgr;
		this.SqlCmdMkr = SqlCmdMkr;
	}

	public ITable<TEntity> T => TblMgr.GetTbl<TEntity>();

	private IAsyncEnumerable<TEntity?> GetManyInIdCore(//TODO寫法不對
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,bool WithDel
		,CT Ct
	){
		// IN 無序語義(忽略不存在的 Id、返回序與 Db 無關):批原語(IN 段批大小)切塊,
		// 每批拼一條 `SELECT * FROM t WHERE CodeId IN (@_0,...)`(+ 軟刪過濾),Build+Prepare+AsyE1d 扁平讀全部匹配行。
		return SqlFlow.Batches<TId, TEntity?>(Ids, async(BatIds, Ct2)=>{
			if(BatIds.Count == 0){
				return Array.Empty<TEntity?>();
			}
			var Mk = T.SqlSplicer().Select("*").FromT()
				.WhereIn(T.CodeIdName, BatIds.Select(x=>(obj?)x).ToList());
			if(!WithDel && T.SoftDelCol is not null){
				Mk.And(T.SoftDelCol.FnSqlIsNonDel());
			}
			var Et = Mk.Build();
			var Cmd = await SqlCmdMkr.Prepare(Ctx, Et.Sql, Ct2);
			Ctx.AddToDispose(Cmd);
			var Ans = new List<TEntity?>();
			await foreach(var Row in Cmd.RawArgs(Et.Args.ToDict()).AsyE1d(Ct2).WithCancellation(Ct2)){
				Ans.Add(T.DbDictToEntity<TEntity>(Row));
			}
			return Ans;
		}, Ct, SqlFlow.DfltInBatchSize(TblMgr.DbSrcType));
	}

	private async Task<IList<TEntity?>> BatGetByIdCore(
		IDbFnCtx Ctx, IList<TId> Ids
		,bool WithDel
		,CT Ct
	){
		if(Ids.Count == 0){
			return [];
		}
		var Mk = T.SqlSplicer().Select("*").FromT().Where1()
			.And().Bool(T.CodeIdName, "=", x=>x.Many(Ids));
		if(!WithDel && T.SoftDelCol is not null){
			Mk.And(T.SoftDelCol.FnSqlIsNonDel());
		}
		// Build + Get1d：N 份等值查一命令、Args 對序後綴；執行端逐結果集回讀、空槽補 null（位置對齊）
		var Ans = await SqlCmdMkr.Get1d(Ctx, T, Mk.Build(), Ct)
			.ToListAsync(Ct);
		return Ans;
	}

	private async IAsyncEnumerable<TEntity> GetAllCore(
		IDbFnCtx Ctx
		,bool WithDel
		,[EnumeratorCancellation] CT Ct
	){
		// 全表查無批次:語句部拼 `SELECT * FROM t WHERE 1=1`(+ 軟刪過濾),Build 產一條無參查,
		// 執行端 Prepare+RawArgs+AsyE1d 扁平讀全部行(與 Run/Get1d 同款薄組合,不切批)
		var Mk = T.SqlSplicer().Select("*").FromT().Where1();
		if(!WithDel && T.SoftDelCol is not null){
			Mk.And(T.SoftDelCol.FnSqlIsNonDel());
		}
		var Et = Mk.Build();
		var Cmd = await SqlCmdMkr.Prepare(Ctx, Et.Sql, Ct);
		Ctx.AddToDispose(Cmd);
		await foreach(var Row in Cmd.RawArgs(Et.Args.ToDict()).AsyE1d(Ct).WithCancellation(Ct)){
			yield return T.DbDictToEntity<TEntity>(Row);
		}
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
		// 流式入口：批原語(IN 段批大小)切塊 → IList 批核心（Build+Get1d 位置對齊）
		return SqlFlow.Batches<TId, TEntity?>(Ids, async(BatIds, Ct2)=>{
			return await BatGetByIdCore(Ctx, BatIds, false, Ct2);
		}, Ct, SqlFlow.DfltInBatchSize(TblMgr.DbSrcType));
	}

	// ■ 批一級形狀(IList 版 OrdGetByIdWithDel,含軟刪):整個 List 一次等值批量查回,位置對齊。
	// 函數邊界 = 批邊界:傳多大的 List 就查多大的批(參數規模的兜底分段由執行層負責);
	// 語義承諾:出參與入參一一對應、重複 Id 出重複實體、查無補 null、空列表直接返回空。
	public async Task<IList<TEntity?>> OrdGetByIdWithDel(
		IDbFnCtx Ctx, IList<TId> Ids, CT Ct
	){
		return await BatGetByIdCore(Ctx, Ids, true, Ct);
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

		// ■ 批內核心(一批 = 一次同構批量 INSERT):吃 IList,SQL 走 SqlSplicer 語句部——
	// InsertIntoT() 拼 `INSERT INTO t`、Vals 語句部拼 `(列...) VALUES (@列...)` 並為每列註冊 Many binder
	// (值序列 = 整批該列值),Build 按公共長度展開 N 份、參數對序後綴全程唯一,執行端一句 Run 落庫。
	// 「一批」的粒度:進來的 List 有多長就拼多長、一次寫完;批大小/切批是調用方(原語)的職責。
	private async Task<nil> BatOrdAddCore(IDbFnCtx Ctx, IList<TEntity> BatEnts, CT Ct){
		var Flds = T.Columns.Keys.ToList();
		var Rows = BatEnts.Select(e => T.EntityToCodeDict(e)).ToList();

		var Sql = T.SqlSplicer()
			.InsertIntoT()
			.Vals(Flds, Rows);

		await SqlCmdMkr.Run(Ctx, Sql.Build(), Ct);
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

	// ■ 同構批量 UPDATE 批核心(吃 IList 一批):UpdateT() 拼 `UPDATE t`、Set 語句部拼 `SET a=@a, b=@b`
	// 並為每列註冊 Many binder(值序列 = 整批該列)、Where1/And/Bool 按主鍵等值定位——SQL 長相就是裸 SQL 形狀;
	// 執行端一句 Run 落庫。「一批」的粒度:List 有多長就拼多長;批大小/切批是調用方(原語)的職責。
	private async Task<nil> BatUpdCore(IDbFnCtx Ctx, IList<TEntity> BatchEnts, IList<str> fieldsToUpdate, CT Ct){
		if(BatchEnts.Count == 0){
			return NIL;
		}
		var Rows = BatchEnts.Select(e => T.EntityToCodeDict(e)).ToList();
		var Ids = Rows.Select(r => r.TryGetValue(T.CodeIdName, out var id) ? id : null).ToList();

		var Sql = T.SqlSplicer()
			.UpdateT()
			.Set(fieldsToUpdate, Rows)
			.Where1().And().Bool(T.CodeIdName, "=", x=>x.Many(Ids));

		await SqlCmdMkr.Run(Ctx, Sql.Build(), Ct);
		return NIL;
	}

	public async Task<IRespBatUpd> OrdUpd(IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct){
		var fieldsToUpdate = T.Columns.Keys.Where(x=>x != T.CodeIdName).ToList();
		if(fieldsToUpdate.Count == 0){
			return new RespUpd();
		}
		await SqlFlow.BatchesInOnly(Ents, (BatEnts, Ct2)=> BatUpdCore(Ctx, BatEnts, fieldsToUpdate, Ct2), Ct, SqlFlow.DfltBatchSize(TblMgr.DbSrcType));
		return new RespUpd();
	}

	// ■ IList 版 OrdUpd:函數只管一批——整批一次同構 UPDATE(批大小/切批是調用方/原語的職責)。
	public async Task<IRespBatUpd> OrdUpd(IDbFnCtx Ctx, IList<TEntity> Ents, CT Ct){
		var fieldsToUpdate = T.Columns.Keys.Where(x=>x != T.CodeIdName).ToList();
		if(fieldsToUpdate.Count == 0 || Ents.Count == 0){
			return new RespUpd();
		}
		await BatUpdCore(Ctx, Ents, fieldsToUpdate, Ct);
		return new RespUpd();
	}

	// ■ 批內核心(一批 Update by Db Dict):吃 IList,SQL 走 SqlSplicer 語句部——
	// UpdEach(CodeIdName, Ids, DbDicts) 庫內逐對拼 `UPDATE t SET 列=@u_{i}_{j}... WHERE CodeId=@id_{i}`
	// (對間 ';' 拼一命令):支持異構字典(每對 Dict 鍵集可不同)、空對跳過、主鍵列不出現在 SET、
	// One binder 綁值(Db 層直放/主鍵按列 Upper→Raw),執行端一句 Run 落庫。
	// 「一批」的粒度:進來的 List 有多長就拼多長、一次寫完;批大小/切批是調用方(原語)的職責。
	private async Task<nil> BatOrdUpdByDbDictCore(
		IDbFnCtx Ctx, IList<(IStr_Any Dict, TId Id)> BatchItems, CT Ct
	){
		var Ids = BatchItems.Select(x => (obj?)x.Id).ToList();
		var DbDicts = BatchItems.Select(x => x.Dict).ToList();
		var Sql = T.SqlSplicer().UpdEach(T.CodeIdName, Ids, DbDicts);

		// 全部為空對(沒有可更新的列):空操作返回(不發 SQL)
		if(Sql.Segs.Count == 0){
			return NIL;
		}

		await SqlCmdMkr.Run(Ctx, Sql.Build(), Ct);
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

	// ■ IList 版 OrdUpdByCodeDict:Ids 與 CodeDicts 成對、整批一次異構 UPDATE(複用 IList 版 OrdUpdByDbDict)。
	public Task<IRespBatUpd> OrdUpdByCodeDict(
		IDbFnCtx Ctx
		,IList<TId> Ids
		,IList<IStr_Any> CodeDicts
		,CT Ct
	){
		var DbDicts = CodeDicts.Select(x=>T.ToDbDict(x)).ToList();
		return OrdUpdByDbDict(Ctx, Ids, DbDicts, Ct);
	}

	// ■ 軟刪 IN 批核心(吃 IList 一批):SQL 走 SqlSplicer 語句部——
	// UpdateT() 拼 `UPDATE t`、Set(軟刪列, raw值) 拼 `SET DelCol = @DelCol`(軟刪值 = SoftDelCol.FnDelete(null)
	// 已是 Db 層 raw)、WhereIn(CodeId, Ids) 拼 `WHERE Id IN (@_0,...)`(每 Id 按列 Upper→Raw 綁 One)。
	// 函數只管一批 IList(函數邊界 = 批邊界);空批直接返回。
	private async Task<nil> BatSoftDelInCore(IDbFnCtx Ctx, IList<TId> Ids, CT Ct){
		if(Ids.Count == 0){
			return NIL;
		}
		if(T.SoftDelCol is null){
			throw new Exception("SoftDeleteCol is null");
		}

		var Sql = T.SqlSplicer()
			.UpdateT()
			.Set(T.SoftDelCol.CodeColName, T.SoftDelCol.FnDelete(null))
			.WhereIn(T.CodeIdName, Ids.Select(x => (obj?)x).ToList());

		await SqlCmdMkr.Run(Ctx, Sql.Build(), Ct);
		return NIL;
	}

	public async Task<ISoftDelInId> SoftDelInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	){
		if(T.SoftDelCol is null){
			throw new Exception("SoftDeleteCol is null");
		}
		await SqlFlow.BatchesInOnly(Ids, (Ids, Ct2)=> BatSoftDelInCore(Ctx, Ids, Ct2), Ct, SqlFlow.DfltInBatchSize(TblMgr.DbSrcType));
		return new SoftDelInId();
	}

	// ■ IList 版 SoftDelInId:函數只管一批——整批一次 IN 軟刪(批大小/切批是調用方/原語的職責)。
	public async Task<ISoftDelInId> SoftDelInId(
		IDbFnCtx Ctx, IList<TId> Ids, CT Ct
	){
		if(Ids.Count > 0){
			await BatSoftDelInCore(Ctx, Ids, Ct);
		}
		return new SoftDelInId();
	}

	public async Task<IHardDelInId> HardDelInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	){
		await SqlFlow.BatchesInOnly(Ids, (Ids, Ct2)=> BatHardDelInCore(Ctx, Ids, Ct2), Ct, SqlFlow.DfltInBatchSize(TblMgr.DbSrcType));
		return new HardDelInId();
	}

	// ■ 硬刪 IN 批核心(吃 IList 一批):DelFromT() 拼 `DELETE FROM t` + WhereIn(...) 拼 `WHERE Id IN (@_0,...)`。
	private async Task<nil> BatHardDelInCore(IDbFnCtx Ctx, IList<TId> Ids, CT Ct){
		if(Ids.Count == 0){
			return NIL;
		}

		var Sql = T.SqlSplicer()
			.DelFromT()
			.WhereIn(T.CodeIdName, Ids.Select(x => (obj?)x).ToList());

		await SqlCmdMkr.Run(Ctx, Sql.Build(), Ct);
		return NIL;
	}

	public async Task<IBatSoftDel> OrdSoftDelById(IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct){
		if(T.SoftDelCol is null){
			throw new Exception("SoftDeleteCol is null");
		}
		await SqlFlow.BatchesInOnly(Ids, (Ids, Ct2)=> BatSoftDelInCore(Ctx, Ids, Ct2), Ct, SqlFlow.DfltBatchSize(TblMgr.DbSrcType));
		return new BatSoftDel();
	}

	public async Task<IBatHardDel> OrdHardDelById(IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct){
		await SqlFlow.BatchesInOnly(Ids, (Ids, Ct2)=> BatHardDelInCore(Ctx, Ids, Ct2), Ct, SqlFlow.DfltBatchSize(TblMgr.DbSrcType));
		return new BatHardDel();
	}

	public async Task<IRespBatAddAgg> OrdAddAgg<TAgg>(IDbFnCtx Ctx, IAsyncEnumerable<TAgg> NewAgg, CT Ct) {
		u64 batchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;
		await SqlFlow.BatchesInOnly(NewAgg, (BatchAgg, Ct2)=> BatOrdAddAggCore<TAgg>(Ctx, BatchAgg, Ct2), Ct, batchSize);
		return new RespBatAddAgg();
	}

	// ■ IList 版 OrdAddAgg:函數只管一批——整批聚合級聯插入一次完成。
	public async Task<IRespBatAddAgg> OrdAddAgg<TAgg>(IDbFnCtx Ctx, IList<TAgg> Aggs, CT Ct) {
		if(Aggs.Count > 0){
			await BatOrdAddAggCore<TAgg>(Ctx, Aggs, Ct);
		}
		return new RespBatAddAgg();
	}

	// ■ 聚合級聯插入批核心(吃 IList 一批):校驗聚合註冊與訪問器後、根與每個 include 資產各拼一次同構批量 INSERT。
	// 「一批」的粒度:進來的 List 有多長就拼多長、一次寫完;批大小/切批是調用方(原語)的職責。
	private async Task<nil> BatOrdAddAggCore<TAgg>(
		IDbFnCtx Ctx, IList<TAgg> BatchAgg, CT Ct
	){
		if(BatchAgg.Count == 0){
			return NIL;
		}
		var aggReg = TblMgr.GetAgg<TAgg>();
		if(aggReg.RootEntityType != typeof(TEntity)){
			throw new Exception($"Agg root type mismatch. Agg={typeof(TAgg)}, ExpectedRoot={typeof(TEntity)}, RegisteredRoot={aggReg.RootEntityType}");
		}
		if(aggReg.RootIdType != typeof(TId)){
			throw new Exception($"Agg root id type mismatch. Agg={typeof(TAgg)}, ExpectedId={typeof(TId)}, RegisteredId={aggReg.RootIdType}");
		}
		// 聚合型別的元資料：下面要按名讀它的成員，故先確認來源認得它。
		if(!TypeInfoSrc.TryGetInfo(typeof(TAgg), out var aggInfo) || aggInfo is null){
			throw new Exception($"No {nameof(ITypeInfo)} for aggregate type: {typeof(TAgg)}");
		}

		var includeTypeInclude = new Dictionary<Type, IAggIncludeReg>();
		foreach(var include in aggReg.Includes){
			if(includeTypeInclude.ContainsKey(include.EntityType)){
				throw new Exception($"BatAddAgg requires unique include entity type. Agg={typeof(TAgg)}, IncludeType={include.EntityType}");
			}
			includeTypeInclude[include.EntityType] = include;
		}

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

		var roots = new List<TEntity>(BatchAgg.Count);
		var includeRows = new Dictionary<Type, IList<obj>>();
		foreach(var include in aggReg.Includes){
			includeRows[include.EntityType] = new List<obj>();
		}

		foreach(var agg in BatchAgg){
			if(agg is null){
				throw new Exception($"Aggregate item is null. Agg={typeof(TAgg)}");
			}
			var aggObj = (obj)agg;

			TEntity? rootEnt = null;
			var oneToOneSeen = new HashSet<Type>();
			// 按成員序讀聚合實例的每個可讀成員，值裏挑出根實體與各 include。
			foreach(var key in aggInfo.ReadableNames){
				if(!TypeInfoSrc.TryGet(typeof(TAgg), aggObj, key, out var val) || val is null){
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
	}

	private async Task<nil> BatDelAggByIdCore<TAgg>(
		IDbFnCtx Ctx
		,IAsyncEnumerable<TId> Ids
		,bool SoftDelete
		,CT Ct
	){
		// 流式版:批原語(IN 段批大小)切塊 → IList 批核心。
		await SqlFlow.BatchesInOnly(Ids, (BatchIds, Ct2)=> BatDelAggByIdListCore<TAgg>(Ctx, BatchIds, SoftDelete, Ct2), Ct, SqlFlow.DfltInBatchSize(TblMgr.DbSrcType));
		return NIL;
	}

	// ■ 聚合級聯刪批核心(吃 IList 一批):根 + 每個 include 資產各一次 IN 刪除(硬刪 DELETE / 軟刪 UPDATE)。
	// 「一批」的粒度:進來的 List 有多長就拼多長、一次寫完;批大小/切批是調用方(原語)的職責。
	private async Task<nil> BatDelAggByIdListCore<TAgg>(
		IDbFnCtx Ctx
		,IList<TId> BatchIds
		,bool SoftDelete
		,CT Ct
	){
		if(BatchIds.Count == 0){
			return NIL;
		}
		var aggReg = TblMgr.GetAgg<TAgg>();
		if(aggReg.RootEntityType != typeof(TEntity)){
			throw new Exception($"Agg root type mismatch. Agg={typeof(TAgg)}, ExpectedRoot={typeof(TEntity)}, RegisteredRoot={aggReg.RootEntityType}");
		}
		if(aggReg.RootIdType != typeof(TId)){
			throw new Exception($"Agg root id type mismatch. Agg={typeof(TAgg)}, ExpectedId={typeof(TId)}, RegisteredId={aggReg.RootIdType}");
		}

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

		var cnt = (u64)BatchIds.Count;
		{
			var cmd = await GetRootCmd(cnt, Ct);
			var arg = MkArg(T, T.CodeIdName, BatchIds);
			await cmd.RawArgs(arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
		}

		foreach(var include in aggReg.Includes){
			var cmd = await GetIncludeCmd(include, cnt, Ct);
			var arg = MkArg(include.Tbl, include.FKeyCodeCol, BatchIds);
			await cmd.RawArgs(arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
		}

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

	// ■ IList 版:函數只管一批——整批聚合級聯軟刪一次完成(IN 語義)。
	public async Task<IRespSoftDelAggInId> SoftDelAggInId<TAgg>(IDbFnCtx Ctx, IList<TId> Ids, CT Ct) {
		if(Ids.Count > 0){
			await BatDelAggByIdListCore<TAgg>(Ctx, Ids, true, Ct);
		}
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
	
	// ■ 存在批量查批核心(吃 IList 一批):與 BatGetByIdCore 同構(Build+Get1d 位置對齊),每槽 bool。
	private async Task<IList<bool>> BatExistsByIdCore(
		IDbFnCtx Ctx, IList<TId> Ids
		,bool WithDel
		,CT Ct
	){
		if(Ids.Count == 0){
			return [];
		}
		var Mk = T.SqlSplicer().Select("*").FromT().Where1()
		.And().Bool(T.CodeIdName, "=", x=>x.Many(Ids));
		if(!WithDel && T.SoftDelCol is not null){
			Mk.And(T.SoftDelCol.FnSqlIsNonDel());
		}
		var Ans = new List<bool>(Ids.Count);
		await foreach(var Row in SqlCmdMkr.Get1d(Ctx, Mk.Build(), Ct).WithCancellation(Ct)){
			Ans.Add(Row is not null);
		}
		return Ans;
	}

	// 流式存在查:批原語(IN 段批大小)切塊 → IList 批核心(位置對齊 bool 流)。
	public IAsyncEnumerable<bool> OrdExistsById(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	){
		return SqlFlow.Batches<TId, bool>(Ids, async(BatIds, Ct2)=>{
			return await BatExistsByIdCore(Ctx, BatIds, false, Ct2);
		}, Ct, SqlFlow.DfltInBatchSize(TblMgr.DbSrcType));
	}

	public IAsyncEnumerable<bool> OrdExistsByIdWithDel(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	){
		return SqlFlow.Batches<TId, bool>(Ids, async(BatIds, Ct2)=>{
			return await BatExistsByIdCore(Ctx, BatIds, true, Ct2);
		}, Ct, SqlFlow.DfltInBatchSize(TblMgr.DbSrcType));
	}
	
	public async Task<IRespBatUpsert> OrdUpsert(
		IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct
	){
		// Upsert = 批內逐元素查存在(含軟刪行)→ 分插入/更新兩堆 → 分別走同構批量寫。
		// 批大小用寫入策略;存在判定用等值批量查(位置對齊),不重發 IN。
		await SqlFlow.BatchesInOnly<TEntity>(Ents, (EntList, Ct2)=> BatOrdUpsertCore(Ctx, EntList, Ct2), Ct, SqlFlow.DfltBatchSize(TblMgr.DbSrcType));
		return new RespBatUpsert();
	}

	// ■ Upsert 批核心(吃 IList 一批):逐元素查存在(含軟刪行)→ 分插入/更新兩堆 → 各一次同構批量寫。
	// 「一批」的粒度:進來的 List 有多長就拼多長、一次寫完;批大小/切批是調用方(原語)的職責。
	private async Task<nil> BatOrdUpsertCore(IDbFnCtx Ctx, IList<TEntity> EntList, CT Ct){
		if(EntList.Count == 0){
			return NIL;
		}
		var Ids = EntList.Select(x => {
			var idObj = T.GetEntityId(x);
			if(idObj is not TId id){
				throw new Exception($"Entity id type mismatch. Entity={typeof(TEntity)}, Id={idObj?.GetType()}, Expected={typeof(TId)}");
			}
			return id;
		}).ToList();
		// Upsert 以主鍵是否已存在為準，軟刪行也必須算存在(避免撞主鍵唯一約束誤插)
		var Exists = await BatExistsByIdCore(Ctx, Ids, true, Ct);
		var toInsert = new List<TEntity>();
		var toUpdate = new List<TEntity>();
		for(i32 i = 0; i < EntList.Count; i++){
			(Exists[i] ? toUpdate : toInsert).Add(EntList[i]);
		}
		if(toInsert.Count > 0){
			await BatOrdAddCore(Ctx, toInsert, Ct);
		}
		if(toUpdate.Count > 0){
			var Flds = T.Columns.Keys.Where(x=>x != T.CodeIdName).ToList();
			await BatUpdCore(Ctx, toUpdate, Flds, Ct);
		}
		return NIL;
	}

	// ■ IList 版 OrdUpsert:函數只管一批——整批一次完成(批大小/切批是調用方/原語的職責)。
	public async Task<IRespBatUpsert> OrdUpsert(
		IDbFnCtx Ctx, IList<TEntity> Ents, CT Ct
	){
		if(Ents.Count > 0){
			await BatOrdUpsertCore(Ctx, Ents, Ct);
		}
		return new RespBatUpsert();
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
		// 聚合型別的元資料：下面要按名讀它的成員，故先確認來源認得它。
		if(!TypeInfoSrc.TryGetInfo(typeof(TAgg), out var aggInfo) || aggInfo is null){
			throw new Exception($"No {nameof(ITypeInfo)} for aggregate type: {typeof(TAgg)}");
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
				// 按成員序讀聚合實例的每個可讀成員，值裏挑出根實體與各 include。
				foreach(var key in aggInfo.ReadableNames){
					if(!TypeInfoSrc.TryGet(typeof(TAgg), aggObj, key, out var val) || val is null){
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
