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
				var slctByIn = await FnScltAllByColInVals<TId>(Ctx, include.Tbl, include.FKeyCodeCol, optQry, Ct);
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

	private IAsyncEnumerable<TAgg?> BatGetAggByIdCore<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,bool WithDel
		,CT Ct
	)
		where TAgg: class
	{
		async IAsyncEnumerable<TId> ToAsyncIds(IEnumerable<TId> Src){
			foreach(var id in Src){
				yield return id;
			}
		}

		var aggReg = TblMgr.GetAgg<TAgg>();
		if(aggReg.RootEntityType != typeof(TEntity)){
			throw new Exception($"Agg root type mismatch. Agg={typeof(TAgg)}, ExpectedRoot={typeof(TEntity)}, RegisteredRoot={aggReg.RootEntityType}");
		}
		if(aggReg.RootIdType != typeof(TId)){
			throw new Exception($"Agg root id type mismatch. Agg={typeof(TAgg)}, ExpectedId={typeof(TId)}, RegisteredId={aggReg.RootIdType}");
		}

		u64 InBatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 50ul : 500ul;

		async Task<IList<TAgg?>> HandleOneBatch(IList<TId> OrderedBatchIds, CT Ct){
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

			var qryCtx = new AggQryCtx();
			if(rootIdSet.Count > 0){
				var rootIds = rootIdSet.ToList();
				var optQry = new OptQry{ InParamCnt = (u64)rootIds.Count };
				foreach(var include in aggReg.Includes){
					var slctByIn = await FnScltAllByColInVals<TId>(Ctx, include.Tbl, include.FKeyCodeCol, optQry, Ct);
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

		async IAsyncEnumerable<TAgg?> Fn(IAsyncEnumerable<TAgg?> Src){
			await foreach(var item in Src.WithCancellation(Ct)){
				yield return item;
			}
		}

		async IAsyncEnumerable<TAgg?> Run(){
			var batchIds = new List<TId>((i32)InBatchSize);
			await foreach(var id in Ids.WithCancellation(Ct)){
				batchIds.Add(id);
				if((u64)batchIds.Count < InBatchSize){
					continue;
				}
				var batchAns = await HandleOneBatch(batchIds, Ct);
				foreach(var item in batchAns){
					yield return item;
				}
				batchIds = new List<TId>((i32)InBatchSize);
			}

			if(batchIds.Count > 0){
				var batchAns = await HandleOneBatch(batchIds, Ct);
				foreach(var item in batchAns){
					yield return item;
				}
			}
		}

		return Fn(Run());
	}
	
	public IAsyncEnumerable<TEntity?> GetManyInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	){
		return GetManyInIdCore(Ctx, Ids, false, Ct);
	}

	public IAsyncEnumerable<TEntity?> GetManyInIdWithDel(
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

	public IAsyncEnumerable<TEntity?> BatGetById(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	){
		return BatGetByIdCore(Ctx, Ids, false, Ct);
	}

	public IAsyncEnumerable<TEntity?> BatGetByIdWithDel(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	){
		return BatGetByIdCore(Ctx, Ids, true, Ct);
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

	public IAsyncEnumerable<TAgg?> BatGetAggById<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	)
		where TAgg: class
	{
		return BatGetAggByIdCore<TAgg>(Ctx, Ids, false, Ct);
	}

	public IAsyncEnumerable<TAgg?> BatGetAggByIdWithDel<TAgg>(
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
		var fn = await FnScltAllByColInVals<TKey>(Ctx, Tbl, CodeCol, OptQry, Ct);
		return async(Tbl, Memb, Keys, Ct)=>{
			IList<TKey> KeyList = Keys.Where(x=>x is not null).ToList()!;
			var poPage = fn(KeyList, Ct);
			var dicts = await poPage.ToListAsync(Ct);
			var pos = dicts.Select(x=>Tbl.DbDictToEntity<TPo>(x));
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
		//var keyList = Keys.AsOrToList();
		// 自動把OptQry之ParamCnt設成 Keys.Count?
		var fn = await FnIncludeEntitysByKeys<TPo, TKey>(Ctx, Tbl, CodeCol, OptQry, Ct);
		return await fn(Tbl, FnMemb, Keys, Ct);
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
		//var keyList = Keys.AsOrToList();
		// 自動把OptQry之ParamCnt設成 Keys.Count?
		var fn = await FnIncludeEntitysByKeys<TPo, TKey>(Ctx, Tbl, CodeCol, OptQry, Ct);
		return await fn(Tbl, FnMemb, Keys, Ct);
	}

	public async Task<IRespBatInsert> BatAdd(IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct){
		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;
		var Cols = T.Columns.Keys.ToList();
		var CmdByCnt = new Dictionary<u64, ISqlCmd>();

		str MkSql(u64 Cnt){
			var Stmts = new List<str>((i32)Cnt);
			foreach(var i in Enumerable.Range(0, (i32)Cnt)){
				var Idx = (u64)i;
				var Fields = str.Join(", ", Cols.Select(x=>T.QtCol(x)));
				var Values = str.Join(", ", Cols.Select(x=>T.NumFieldParam(x, Idx).ToString()));
				Stmts.Add($"INSERT INTO {T.Qt(T.DbTblName)} ({Fields}) VALUES ({Values})");
			}
			return str.Join(";\n", Stmts);
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			if(CmdByCnt.TryGetValue(Cnt, out var Got)){
				return Got;
			}
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			CmdByCnt[Cnt] = Cmd;
			return Cmd;
		}

		await using var Batch = new BatchCollector<TEntity, nil>(async(BatchEnts, Ct)=>{
			var Cnt = (u64)BatchEnts.Count;
			var Arg = new Dictionary<str, obj?>();
			for(i32 i = 0; i < BatchEnts.Count; i++){
				var Ent = BatchEnts[i];
				var DbDict = T.ToDbDict(T.EntityToCodeDict(Ent));
				foreach(var (k, v) in DbDict){
					Arg[T.NumFieldParam(k, (u64)i).Name] = v;
				}
			}
			var Cmd = await GetCmd(Cnt, Ct);
			await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}, BatchSize);

		await foreach(var Ent in Ents.WithCancellation(Ct)){
			await Batch.Add(Ent, Ct);
		}
		await Batch.End(Ct);

		return new RespBatInsert();
	}

	public async Task<IRespBatUpd> BatUpd(IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct){
		var fieldsToUpdate = T.Columns.Keys.Where(x=>x != T.CodeIdName).ToList();
		if(fieldsToUpdate.Count == 0){
			return new RespUpd();
		}

		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;
		var CmdByCnt = new Dictionary<u64, ISqlCmd>();

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
			if(CmdByCnt.TryGetValue(Cnt, out var Got)){
				return Got;
			}
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			CmdByCnt[Cnt] = Cmd;
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

	public async Task<IRespBatUpd> BatUpdByDbDict(
		IDbFnCtx Ctx
		,IAsyncEnumerable<TId> Ids
		,IAsyncEnumerable<IStr_Any> Dicts
		,CT Ct
	){
		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;
		var CmdBySql = new Dictionary<str, ISqlCmd>();
		var dbIdColName = T.DbCol(T.CodeIdName);

		async Task<ISqlCmd> GetCmd(str Sql, CT Ct){
			if(CmdBySql.TryGetValue(Sql, out var Got)){
				return Got;
			}
			var Cmd = await SqlCmdMkr.Prepare(Ctx, Sql, Ct);
			Ctx.AddToDispose(Cmd);
			CmdBySql[Sql] = Cmd;
			return Cmd;
		}

		await using var Batch = new BatchCollector<(IStr_Any Dict, TId Id), nil>(async(BatchItems, Ct)=>{
			var Stmts = new List<str>(BatchItems.Count);
			var Arg = new Dictionary<str, obj?>();

			for(i32 i = 0; i < BatchItems.Count; i++){
				var (DbDict, Id) = BatchItems[i];
				var SetSegs = new List<str>();
				i32 j = 0;
				foreach(var (DbColName, RawVal) in DbDict){
					if(DbColName == dbIdColName || DbColName == T.CodeIdName){
						continue;
					}
					var P = T.Prm($"u_{i}_{j}");
					SetSegs.Add($"{T.Qt(DbColName)} = {P}");
					Arg[P.Name] = RawVal;
					j++;
				}

				if(SetSegs.Count == 0){
					continue;
				}

				var PId = T.Prm($"id_{i}");
				Arg[PId.Name] = T.UpperToRaw(Id, T.CodeIdName);
				var Clause = str.Join(", ", SetSegs);
				Stmts.Add($"UPDATE {T.Qt(T.DbTblName)} SET {Clause} WHERE {T.QtCol(T.CodeIdName)} = {PId}");
			}

			if(Stmts.Count == 0){
				return NIL;
			}

			var Sql = str.Join(";\n", Stmts);
			var Cmd = await GetCmd(Sql, Ct);
			await Cmd.RawArgs(Arg).AsyE1d(Ct).FirstOrDefaultAsync(Ct);
			return NIL;
		}, BatchSize);

		await using var DictEtor = Dicts.GetAsyncEnumerator(Ct);
		await using var IdEtor = Ids.GetAsyncEnumerator(Ct);
		while(true){
			var HasDict = await DictEtor.MoveNextAsync();
			var HasId = await IdEtor.MoveNextAsync();
			if(HasDict != HasId){
				throw new ArgumentException("Dicts count must equal to Ids count");
			}
			if(!HasDict){
				break;
			}
			await Batch.Add((DictEtor.Current, IdEtor.Current), Ct);
		}
		await Batch.End(Ct);

		return new RespUpd();
	}
	
	public Task<IRespBatUpd> BatUpdByCodeDict(
		IDbFnCtx Ctx
		,IAsyncEnumerable<TId> Ids
		,IAsyncEnumerable<IStr_Any> Dicts
		,CT Ct
	){
		var DbDicts = Dicts.Select(x=>T.ToDbDict(x));
		return BatUpdByDbDict(Ctx, Ids, DbDicts, Ct);
	}

	public async Task<ISoftDelInId> SoftDelInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	){
		if(T.SoftDelCol is null){
			throw new Exception("SoftDeleteCol is null");
		}
		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 50ul : 500ul;
		var CmdByCnt = new Dictionary<u64, ISqlCmd>();
		var valToSet = T.SoftDelCol.FnDelete(null);

		str MkSql(u64 Cnt){
			var IdParams = T.NumParams(Cnt).ToList();
			var PSoft = T.Prm("__SoftDelVal");
			return $"UPDATE {T.Qt(T.DbTblName)} SET {T.QtCol(T.SoftDelCol.CodeColName)} = {PSoft} WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", IdParams)})";
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			if(CmdByCnt.TryGetValue(Cnt, out var Got)){
				return Got;
			}
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			CmdByCnt[Cnt] = Cmd;
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

	public async Task<IHardDelInId> HardDelInId(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct
	){
		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 50ul : 500ul;
		var CmdByCnt = new Dictionary<u64, ISqlCmd>();

		str MkSql(u64 Cnt){
			var IdParams = T.NumParams(Cnt).ToList();
			return $"DELETE FROM {T.Qt(T.DbTblName)} WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", IdParams)})";
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			if(CmdByCnt.TryGetValue(Cnt, out var Got)){
				return Got;
			}
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			CmdByCnt[Cnt] = Cmd;
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

	public async Task<IBatSoftDel> BatSoftDelById(IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct){
		if(T.SoftDelCol is null){
			throw new Exception("SoftDeleteCol is null");
		}

		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;
		var CmdByCnt = new Dictionary<u64, ISqlCmd>();
		var valToSet = T.SoftDelCol.FnDelete(null);

		str MkSql(u64 Cnt){
			var IdParams = T.NumParams(Cnt).ToList();
			var PSoft = T.Prm("__SoftDelVal");
			return $"UPDATE {T.Qt(T.DbTblName)} SET {T.QtCol(T.SoftDelCol.CodeColName)} = {PSoft} WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", IdParams)})";
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			if(CmdByCnt.TryGetValue(Cnt, out var Got)){
				return Got;
			}
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			CmdByCnt[Cnt] = Cmd;
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

	public async Task<IBatHardDel> BatHardDelById(IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids, CT Ct){
		u64 BatchSize = TblMgr.DbSrcType == EDbSrcType.Sqlite ? 1ul : 500ul;
		var CmdByCnt = new Dictionary<u64, ISqlCmd>();

		str MkSql(u64 Cnt){
			var IdParams = T.NumParams(Cnt).ToList();
			return $"DELETE FROM {T.Qt(T.DbTblName)} WHERE {T.QtCol(T.CodeIdName)} IN ({str.Join(", ", IdParams)})";
		}

		async Task<ISqlCmd> GetCmd(u64 Cnt, CT Ct){
			if(CmdByCnt.TryGetValue(Cnt, out var Got)){
				return Got;
			}
			var Cmd = await SqlCmdMkr.Prepare(Ctx, MkSql(Cnt), Ct);
			Ctx.AddToDispose(Cmd);
			CmdByCnt[Cnt] = Cmd;
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

	public async Task<IRespBatAddAgg> BatAddAgg<TAgg>(IDbFnCtx Ctx, IAsyncEnumerable<TAgg> NewAgg, CT Ct) {
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
		var rootCmdByCnt = new Dictionary<u64, ISqlCmd>();
		var includeColsByType = new Dictionary<Type, IList<str>>();
		var includeCmdByTypeCnt = new Dictionary<(Type, u64), ISqlCmd>();

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
			if(rootCmdByCnt.TryGetValue(cnt, out var got)){
				return got;
			}
			var cmd = await SqlCmdMkr.Prepare(Ctx, MkInsertSql(T, rootCols, cnt), Ct);
			Ctx.AddToDispose(cmd);
			rootCmdByCnt[cnt] = cmd;
			return cmd;
		}

		async Task<ISqlCmd> GetIncludeCmd(IAggIncludeReg include, u64 cnt, CT Ct){
			var key = (include.EntityType, cnt);
			if(includeCmdByTypeCnt.TryGetValue(key, out var got)){
				return got;
			}
			if(!includeColsByType.TryGetValue(include.EntityType, out var cols)){
				cols = include.Tbl.Columns.Keys.ToList();
				includeColsByType[include.EntityType] = cols;
			}
			var cmd = await SqlCmdMkr.Prepare(Ctx, MkInsertSql(include.Tbl, cols, cnt), Ct);
			Ctx.AddToDispose(cmd);
			includeCmdByTypeCnt[key] = cmd;
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
		var rootCmdByCnt = new Dictionary<u64, ISqlCmd>();
		var includeCmdByTypeCnt = new Dictionary<(Type, u64), ISqlCmd>();

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
			if(rootCmdByCnt.TryGetValue(cnt, out var got)){
				return got;
			}
			var cmd = await SqlCmdMkr.Prepare(Ctx, MkDelSql(T, T.CodeIdName, cnt, SoftDelete), Ct);
			Ctx.AddToDispose(cmd);
			rootCmdByCnt[cnt] = cmd;
			return cmd;
		}

		async Task<ISqlCmd> GetIncludeCmd(IAggIncludeReg include, u64 cnt, CT Ct){
			var key = (include.EntityType, cnt);
			if(includeCmdByTypeCnt.TryGetValue(key, out var got)){
				return got;
			}
			var cmd = await SqlCmdMkr.Prepare(Ctx, MkDelSql(include.Tbl, include.FKeyCodeCol, cnt, SoftDelete), Ct);
			Ctx.AddToDispose(cmd);
			includeCmdByTypeCnt[key] = cmd;
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
	
	public Task<IRespBatUpdAgg> BatHardUpdAgg<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TAgg> Agg, CT Ct
	){
		return BatUpdAggCore(Ctx, Agg, false, Ct);
	}

	public Task<IRespBatUpdAgg> BatSoftUpdAgg<TAgg>(
		IDbFnCtx Ctx, IAsyncEnumerable<TAgg> Agg, CT Ct
	){
		return BatUpdAggCore(Ctx, Agg, true, Ct);
	}
	
	public IAsyncEnumerable<bool> BatExistsById(
		IDbFnCtx Ctx, IAsyncEnumerable<TId> Ids
		,CT Ct
	){
		var Sql = T.SqlSplicer().Select("*").From().Where1()
		.And().Bool(T.CodeIdName, "=", x=>x.Many(Ids));
		if(T.SoftDelCol is not null){
			Sql.And(T.SoftDelCol.FnSqlIsNonDel());
		}
		var dicts = SqlCmdMkr.RunDupliSql(Ctx, Sql, Ct);
		return dicts.Select(x=>x is not null);
	}
	
	public async Task<IRespBatUpsert> BatUpsert(
		IDbFnCtx Ctx, IAsyncEnumerable<TEntity> Ents, CT Ct
	){
		var batchSize = T.DbStuff.DfltOptBatch.DupliSqlBatchSize;
		var batch = new BatchCollector<TEntity, nil>(async(EntList, Ct)=>{
			var ids = EntList.Select(x=>(TId)T.GetEntityId(x)!).ToAsyncEnumerable();
			var existList = BatExistsById(Ctx, ids, Ct);
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
			await BatAdd(Ctx, ToolAsyE.ToAsyE(toInsert), Ct);
			await BatUpd(Ctx, ToolAsyE.ToAsyE(toUpdate), Ct);
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
			await foreach(var one in BatExistsById(Ctx, ToAsyEId(batchIds), Ct).WithCancellation(Ct)){
				ans.Add(one);
			}
			if(ans.Count != batchIds.Count){
				throw new Exception($"{nameof(BatExistsById)} result count mismatch. Expect={batchIds.Count}, Got={ans.Count}");
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
				await BatAdd(Ctx, ToAsyE(toInsert), Ct);
			}
			if(toUpdate.Count > 0){
				await BatUpd(Ctx, ToAsyE(toUpdate), Ct);
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

			await BatUpd(Ctx, ToAsyE(roots), Ct);

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
